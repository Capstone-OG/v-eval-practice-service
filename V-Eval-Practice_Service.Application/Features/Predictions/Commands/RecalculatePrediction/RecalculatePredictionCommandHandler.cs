using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Predictions.Commands.RecalculatePrediction;

public class RecalculatePredictionCommandHandler : IRequestHandler<RecalculatePredictionCommand, Result<RecalculatePredictionResponseDto>>
{
    private readonly IScorePredictionRepository _predictionRepository;
    private readonly IMockExamRepository _mockExamRepository;
    private readonly IMistakeNotebookRepository _mistakeRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IContentGrpcClient _contentGrpcClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public RecalculatePredictionCommandHandler(
        IScorePredictionRepository predictionRepository,
        IMockExamRepository mockExamRepository,
        IMistakeNotebookRepository mistakeRepository,
        ILearningProfileRepository learningProfileRepository,
        IContentGrpcClient contentGrpcClient)
    {
        _predictionRepository = predictionRepository;
        _mockExamRepository = mockExamRepository;
        _mistakeRepository = mistakeRepository;
        _learningProfileRepository = learningProfileRepository;
        _contentGrpcClient = contentGrpcClient;
    }

    public async Task<Result<RecalculatePredictionResponseDto>> Handle(
        RecalculatePredictionCommand command,
        CancellationToken cancellationToken)
    {
        Guid studentId = command.Request?.StudentId.HasValue == true && command.Request.StudentId.Value != Guid.Empty
            ? command.Request.StudentId.Value
            : command.EffectiveStudentId;

        if (studentId == Guid.Empty)
        {
            return Result<RecalculatePredictionResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền header X-User-Id hoặc trường student_id)."));
        }

        var previousPrediction = await _predictionRepository.GetLatestByStudentIdAsync(studentId, cancellationToken);
        int targetScore = previousPrediction?.TargetScore ?? 850;

        string source = string.IsNullOrWhiteSpace(command.Request?.TriggerSource)
            ? "MOCK_EXAM"
            : command.Request.TriggerSource.Trim().ToUpperInvariant();

        // 1. Ước lượng điểm dự báo dựa trên bài nộp tham chiếu (ReferenceSubmission) hoặc bài thi mới nhất
        int predictedScore;
        if (command.Request?.ReferenceSubmissionId.HasValue == true && command.Request.ReferenceSubmissionId.Value != Guid.Empty)
        {
            var refSub = await _mockExamRepository.GetByIdAsync(command.Request.ReferenceSubmissionId.Value, cancellationToken);
            if (refSub != null && refSub.TotalQuestions > 0)
            {
                // Chuẩn hóa điểm bài thi sang thang điểm chuẩn ĐGNL 1200
                predictedScore = (int)Math.Round(((double)refSub.TotalCorrect / refSub.TotalQuestions) * 1200);
            }
            else
            {
                predictedScore = previousPrediction?.PredictedScore ?? 800;
            }
        }
        else
        {
            int baseScore = previousPrediction?.PredictedScore ?? 800;
            // Tự động gia tăng điểm dự báo khi học sinh đã hoàn thành các bài tập bù điểm trong toa thuốc
            if (previousPrediction != null && !string.IsNullOrWhiteSpace(previousPrediction.PrescriptionItems))
            {
                try
                {
                    var prevItems = JsonSerializer.Deserialize<List<PrescriptionItemDto>>(previousPrediction.PrescriptionItems, JsonOptions);
                    int remediedCount = prevItems?.Count(p => p.IsRemedied) ?? 0;
                    if (remediedCount > 0)
                    {
                        baseScore = Math.Min(1200, baseScore + (remediedCount * 15));
                    }
                }
                catch
                {
                    // Giữ nguyên baseScore nếu parse lỗi
                }
            }
            predictedScore = baseScore;
        }

        // 2. Tính sai số chuẩn đo lường (SEM) & khoảng tin cậy 95%
        double semScore = 24.5;
        double confidenceRate = 0.95;
        int minScore = Math.Max(0, (int)Math.Round(predictedScore - (1.96 * semScore)));
        int maxScore = Math.Min(1200, (int)Math.Round(predictedScore + (1.96 * semScore)));

        // 3. Tính thâm hụt điểm & trạng thái cân đối
        int deficitScore = Math.Max(0, targetScore - predictedScore);
        string status = deficitScore <= 0
            ? "ON_TRACK"
            : deficitScore <= 30
                ? "AT_RISK"
                : "CRITICAL";

        // 4. Bốc động Toa thuốc bù điểm bằng giải thuật Knapsack ROI từ dữ liệu học tập thực tế
        var newPrescriptions = await BuildDynamicPrescriptionsAsync(studentId, deficitScore, cancellationToken);

        var newPrediction = new ScorePrediction
        {
            PredictionId = Guid.NewGuid(),
            StudentId = studentId,
            PredictedScore = predictedScore,
            PredictedScoreMin = minScore,
            PredictedScoreMax = maxScore,
            ConfidenceRate = confidenceRate,
            TargetScore = targetScore,
            DeficitScore = deficitScore,
            SemScore = semScore,
            PrescriptionItems = JsonSerializer.Serialize(newPrescriptions, JsonOptions),
            CalculationSource = source,
            CreatedAt = DateTime.UtcNow
        };

        await _predictionRepository.AddAsync(newPrediction, cancellationToken);

        var response = new RecalculatePredictionResponseDto
        {
            PredictionId = newPrediction.PredictionId,
            StudentId = studentId,
            PredictedScore = predictedScore,
            ScoreRange = new ScoreRangeDto
            {
                Min = minScore,
                Max = maxScore
            },
            SemScore = semScore,
            ConfidenceRate = confidenceRate,
            TargetScore = targetScore,
            DeficitScore = deficitScore,
            Status = status,
            CalculationSource = source,
            PrescriptionItemsCount = newPrescriptions.Count,
            CreatedAt = newPrediction.CreatedAt
        };

        return Result<RecalculatePredictionResponseDto>.Success(response);
    }

    private async Task<List<PrescriptionItemDto>> BuildDynamicPrescriptionsAsync(
        Guid studentId,
        int deficitScore,
        CancellationToken ct)
    {
        var candidates = new List<PrescriptionItemDto>();

        // Nạp cây kỹ năng từ Content Service để lấy tên kỹ năng chính xác
        IReadOnlyList<SkillTreeNodeDto> skillsTree;
        try
        {
            skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
        }
        catch
        {
            skillsTree = new List<SkillTreeNodeDto>();
        }

        var skillDict = skillsTree.ToDictionary(s => s.SkillId, s => s.Name);

        // Nguồn 1: Quét Sổ tay lỗi sai (MistakeNotebooks) của chính học sinh
        var (unmasteredMistakes, _) = await _mistakeRepository.GetByFilterAsync(
            studentId, skillId: null, cognitiveErrorTag: null, isMastered: false, pageIndex: 1, pageSize: 20, ct: ct);

        if (unmasteredMistakes.Count > 0)
        {
            var mistakeGroups = unmasteredMistakes
                .GroupBy(m => new { m.SkillId, PatternId = !string.IsNullOrWhiteSpace(m.PatternId) ? m.PatternId : $"PAT_{m.SkillId.ToString()[..8]}" })
                .ToList();

            foreach (var group in mistakeGroups)
            {
                var first = group.First();
                double errorDiscount = (first.CognitiveErrorTag == "CARELESS" || first.CognitiveErrorTag == "MISREAD_QUESTION") ? 0.6 : 1.0;
                double studyHours = Math.Round(1.5 * errorDiscount, 1);
                double delta = Math.Round(20.0 + (group.Count() * 2.5), 1);
                double roi = Math.Round(delta / studyHours, 2);

                string skillName = skillDict.TryGetValue(first.SkillId, out var sName) ? sName : "Kỹ năng cần bổ trợ";
                string patternName = $"Khắc phục lỗi: {skillName}";

                candidates.Add(new PrescriptionItemDto
                {
                    PatternId = group.Key.PatternId,
                    PatternName = patternName,
                    SkillId = first.SkillId,
                    DeltaScore = delta,
                    StudyHoursNeeded = studyHours,
                    RoiScore = roi,
                    IsRemedied = false
                });
            }
        }

        // Nguồn 2: Nếu chưa đủ dạng bài từ Sổ tay, quét Hồ sơ năng lực (LearningProfiles) của học sinh
        if (candidates.Count < 2)
        {
            var learningProfiles = await _learningProfileRepository.GetByStudentIdAsync(studentId, ct);
            var weakProfiles = learningProfiles
                .Where(p => p.MasteryScore < 0.70 && !candidates.Exists(c => c.SkillId == p.SkillId))
                .OrderBy(p => p.MasteryScore)
                .Take(3)
                .ToList();

            foreach (var wp in weakProfiles)
            {
                string skillName = skillDict.TryGetValue(wp.SkillId, out var sName) ? sName : "Chuyên đề trọng tâm";
                double studyHours = 1.8;
                double delta = 25.0;
                double roi = Math.Round(delta / studyHours, 2);

                candidates.Add(new PrescriptionItemDto
                {
                    PatternId = $"PAT_SKILL_{wp.SkillId.ToString()[..8].ToUpperInvariant()}",
                    PatternName = $"Rèn luyện chuyên đề: {skillName}",
                    SkillId = wp.SkillId,
                    DeltaScore = delta,
                    StudyHoursNeeded = studyHours,
                    RoiScore = roi,
                    IsRemedied = false
                });
            }
        }

        // Nguồn 3: Fallback chuẩn nếu học sinh chưa có bất kỳ dữ liệu lỗi nào
        if (candidates.Count == 0)
        {
            candidates.Add(new PrescriptionItemDto
            {
                PatternId = "PAT_TOAN_LOGIC_01",
                PatternName = "Quy nạp logic mệnh đề điều kiện",
                SkillId = Guid.Parse("a0000001-0000-0000-0000-000000000007"),
                DeltaScore = 25.5,
                StudyHoursNeeded = 1.8,
                RoiScore = 14.16,
                IsRemedied = false
            });
            candidates.Add(new PrescriptionItemDto
            {
                PatternId = "PAT_HOA_PH_03",
                PatternName = "Tính pH dung dịch đệm axit yếu",
                SkillId = Guid.Parse("a0000001-0000-0000-0000-000000000010"),
                DeltaScore = 20.0,
                StudyHoursNeeded = 1.5,
                RoiScore = 13.33,
                IsRemedied = false
            });
        }

        // Sắp xếp theo ROI giảm dần (Greedy Knapsack) và lấy tối đa 2-3 dạng bài tối ưu nhất
        var selected = candidates
            .OrderByDescending(c => c.RoiScore)
            .Take(3)
            .ToList();

        int rank = 1;
        foreach (var item in selected)
        {
            item.PriorityRank = rank++;
        }

        return selected;
    }
}
