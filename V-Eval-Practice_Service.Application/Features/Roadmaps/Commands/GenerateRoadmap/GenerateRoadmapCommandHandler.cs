using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Graph;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;

/// <summary>
/// Handler điều phối sinh lộ trình học tập toàn diện GDPT 2018 (Lớp 10-12, 9 môn, 337+ dạng bài)
/// theo đúng mạch học tuần tự từ đầu đến cuối:
///   1. WEAK (Ưu tiên cải thiện ngay — các dạng bài đã kiểm tra &amp; còn yếu P(Lt) &lt; 70%)
///   2. UNASSESSED (Chưa đánh giá — nối tiếp lộ trình học tuần tự từ đầu đến cuối Lớp 10 -> 11 -> 12, Chương 1 -> Chương N, Bài 1 -> Bài M; khi làm bài test về sau đạt điểm OK sẽ tự động được đánh giá lại)
///   3. MASTERED (Đã kiểm tra đạt điểm OK — hiển thị ở sau cùng để không chắn ngang lộ trình học)
/// </summary>
public class GenerateRoadmapCommandHandler : IRequestHandler<GenerateRoadmapCommand, Result<GenerateRoadmapResponseDto>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IClassHeatmapRepository _heatmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly TarjanCycleDetector _tarjanDetector;
    private readonly TopologicalSorter _topologicalSorter;
    private readonly MilestoneBinder _milestoneBinder;
    private readonly ILogger<GenerateRoadmapCommandHandler> _logger;

    public GenerateRoadmapCommandHandler(
        IExamSubmissionRepository submissionRepository,
        ILearningProfileRepository learningProfileRepository,
        ILearningRoadmapRepository roadmapRepository,
        IClassHeatmapRepository heatmapRepository,
        IContentGrpcClient contentGrpcClient,
        IIdentityGrpcClient identityGrpcClient,
        TarjanCycleDetector tarjanDetector,
        TopologicalSorter topologicalSorter,
        MilestoneBinder milestoneBinder,
        ILogger<GenerateRoadmapCommandHandler> logger)
    {
        _submissionRepository = submissionRepository;
        _learningProfileRepository = learningProfileRepository;
        _roadmapRepository = roadmapRepository;
        _heatmapRepository = heatmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _identityGrpcClient = identityGrpcClient;
        _tarjanDetector = tarjanDetector;
        _topologicalSorter = topologicalSorter;
        _milestoneBinder = milestoneBinder;
        _logger = logger;
    }

    public async Task<Result<GenerateRoadmapResponseDto>> Handle(
        GenerateRoadmapCommand request,
        CancellationToken ct)
    {
        _logger.LogInformation("Bắt đầu sinh lộ trình toàn diện GDPT 2018 cho học sinh {StudentId} từ bài thi chẩn đoán {SubmissionId}",
            request.StudentId, request.DiagnosticSubmissionId);

        // =====================================================================
        // Bước 1: Trích xuất hồ sơ năng lực đầu vào (Core Flow 1 Transition)
        // =====================================================================
        var submission = await _submissionRepository.GetByIdAsync(request.DiagnosticSubmissionId, ct);

        if (submission == null)
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.NotFound("ExamSubmission.NotFound", $"Không tìm thấy bài thi chẩn đoán {request.DiagnosticSubmissionId}"));
        }

        if (submission.StudentId != request.StudentId)
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Forbidden("ExamSubmission.Forbidden", "Bạn không có quyền tạo lộ trình từ bài nộp của học sinh khác."));
        }

        if (submission.Status != "COMPLETED")
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Validation("ExamSubmission.NotCompleted",
                    $"Bài thi chẩn đoán đang ở trạng thái '{submission.Status}'. Chỉ được sinh lộ trình từ bài thi đã hoàn thành (COMPLETED)."));
        }

        int targetScore = 850;
        try
        {
            var profile = await _identityGrpcClient.GetStudentProfileAsync(request.StudentId, ct);
            if (profile.Exists && profile.TargetScore >= 400 && profile.TargetScore <= 1200)
            {
                targetScore = profile.TargetScore;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy TargetScore từ Identity gRPC cho học sinh {StudentId}, sử dụng mặc định 850.", request.StudentId);
        }

        var learningProfiles = await _learningProfileRepository.GetByStudentIdAsync(request.StudentId, ct);
        var skillMasteryPL0 = learningProfiles.ToDictionary(lp => lp.SkillId, lp => lp.MasteryScore);

        // Dữ liệu đánh giá trực tiếp đã từng làm (Diagnostic / MockExam / PLAR) trong ClassSkillHeatmaps
        var heatmaps = await _heatmapRepository.GetHeatmapsByStudentIdAsync(request.StudentId, ct);
        var heatmapBySkill = heatmaps
            .GroupBy(h => h.SkillId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        // =====================================================================
        // Bước 2: Nạp toàn bộ phổ kiến thức GDPT 2018 (tầng PATTERN — Lớp 10-12)
        // =====================================================================
        IReadOnlyList<SkillTreeNodeDto> fullSkillsTree;
        try
        {
            fullSkillsTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi gRPC GetSkillsTree sang Content Service.");
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("ContentService.GrpcUnavailable", "Không thể nạp Cây khung năng lực từ Content Service qua gRPC."));
        }

        if (fullSkillsTree == null || fullSkillsTree.Count == 0)
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("ContentService.EmptySkillsTree", "Danh sách kỹ năng từ Content Service rỗng."));
        }

        // Lấy TOÀN BỘ tầng dạng bài học tập thực tế (PATTERN) từ cây GDPT 2018 (bao phủ 9 môn & Lớp 10-12)
        var learningPatterns = fullSkillsTree
            .Where(s => string.Equals(s.Level, "PATTERN", StringComparison.OrdinalIgnoreCase)
                        && (s.QuestionCount > 0 || s.MaterialId.HasValue))
            .ToList();

        if (learningPatterns.Count == 0)
        {
            learningPatterns = fullSkillsTree
                .Where(s => string.Equals(s.Level, "PATTERN", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (learningPatterns.Count == 0)
        {
            learningPatterns = fullSkillsTree
                .Where(s => !string.Equals(s.Level, "CHAPTER", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (learningPatterns.Count == 0)
        {
            learningPatterns = fullSkillsTree.ToList();
        }

        // Phân tích kết quả TRỰC TIẾP từng kỹ năng trong bài Diagnostic 30 câu
        var diagnosticSkillStats = new Dictionary<Guid, (int Correct, int Total)>();
        var diagnosticDomainAccuracy = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(submission.ExamId, ct);
            if (answerKeys.Count > 0 && submission.Answers != null && submission.Answers.Count > 0)
            {
                var domainCorrect = new Dictionary<string, (int Correct, int Total)>(StringComparer.OrdinalIgnoreCase);
                foreach (var ans in submission.Answers)
                {
                    if (!answerKeys.TryGetValue(ans.QuestionId, out var key)) continue;
                    var dCode = !string.IsNullOrWhiteSpace(key.DomainCode) ? key.DomainCode : "DOM_MATH";
                    domainCorrect.TryGetValue(dCode, out var dCur);
                    domainCorrect[dCode] = (dCur.Correct + (ans.IsCorrect ? 1 : 0), dCur.Total + 1);

                    if (Guid.TryParse(key.SkillId, out var skId))
                    {
                        diagnosticSkillStats.TryGetValue(skId, out var sCur);
                        diagnosticSkillStats[skId] = (sCur.Correct + (ans.IsCorrect ? 1 : 0), sCur.Total + 1);
                    }
                }

                foreach (var (dCode, stat) in domainCorrect)
                {
                    diagnosticDomainAccuracy[dCode] = stat.Total > 0 ? (double)stat.Correct / stat.Total : 0.5;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy chi tiết đáp án Diagnostic {ExamId} để phân loại 3 vùng ưu tiên.", submission.ExamId);
        }

        // =====================================================================
        // Bước 3: Kiểm tra tính hợp lệ DAG trên toàn bộ đồ thị kỹ năng (Tarjan SCC)
        // =====================================================================
        var skillMap = learningPatterns.ToDictionary(s => s.SkillId);
        var selectedSet = skillMap.Keys.ToHashSet();
        var prerequisitesMap = learningPatterns.ToDictionary(
            s => s.SkillId,
            s => s.PrerequisiteIds.Where(pid => selectedSet.Contains(pid)).ToList());

        var adjacencyList = learningPatterns.ToDictionary(s => s.SkillId, _ => new List<Guid>());
        foreach (var (skillId, prereqs) in prerequisitesMap)
        {
            foreach (var prereq in prereqs)
            {
                if (adjacencyList.ContainsKey(prereq))
                {
                    adjacencyList[prereq].Add(skillId);
                }
            }
        }

        var cycles = _tarjanDetector.DetectCycles(adjacencyList);
        if (cycles.Count > 0)
        {
            var cycleDescription = string.Join(" | ", cycles.Select(c => string.Join(" -> ", c)));
            _logger.LogError("Phát hiện chu trình kín trong Cây khung năng lực: {Cycles}", cycleDescription);
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("SkillGraph.CycleDetected",
                    $"Phát hiện lỗi vòng lặp phụ thuộc trong Cây khung năng lực: {cycleDescription}"));
        }

        // =====================================================================
        // Bước 4: Phân tầng chính xác theo mạch học từ đầu đến cuối:
        //   1. WEAK (Ưu tiên cải thiện ngay)
        //   2. UNASSESSED (Chưa đánh giá — nối tiếp lộ trình học tuần tự từ đầu đến cuối Lớp 10 -> 11 -> 12, Chương 1..N, Bài 1..M)
        //   3. MASTERED (Đã làm bài test đạt điểm OK — hiển thị ở sau cùng)
        // =====================================================================
        var assessmentStatusBySkill = new Dictionary<Guid, string>();
        var measuredPltBySkill = new Dictionary<Guid, double>();

        var weakPatterns = new List<SkillTreeNodeDto>();
        var unassessedPatterns = new List<SkillTreeNodeDto>();
        var masteredPatterns = new List<SkillTreeNodeDto>();

        foreach (var skill in learningPatterns)
        {
            bool testedInDiagnostic = diagnosticSkillStats.TryGetValue(skill.SkillId, out var diagStat) && diagStat.Total > 0;
            bool testedInHeatmap = heatmapBySkill.TryGetValue(skill.SkillId, out var hmCell);

            if (testedInDiagnostic)
            {
                double directAcc = (double)diagStat.Correct / diagStat.Total;
                double plt = testedInHeatmap
                    ? hmCell!.PltValue
                    : (skillMasteryPL0.TryGetValue(skill.SkillId, out var p) ? p : Math.Clamp(directAcc, 0.08, 0.95));

                measuredPltBySkill[skill.SkillId] = Math.Round(plt, 4);

                if (directAcc < 0.70 || plt < 0.70)
                {
                    assessmentStatusBySkill[skill.SkillId] = "WEAK";
                    weakPatterns.Add(skill);
                }
                else
                {
                    assessmentStatusBySkill[skill.SkillId] = "MASTERED";
                    masteredPatterns.Add(skill);
                }
            }
            else if (testedInHeatmap)
            {
                double plt = hmCell!.PltValue;
                measuredPltBySkill[skill.SkillId] = Math.Round(plt, 4);

                if (plt < 0.70)
                {
                    assessmentStatusBySkill[skill.SkillId] = "WEAK";
                    weakPatterns.Add(skill);
                }
                else
                {
                    assessmentStatusBySkill[skill.SkillId] = "MASTERED";
                    masteredPatterns.Add(skill);
                }
            }
            else
            {
                // Chưa từng gặp câu hỏi của dạng bài này trong bài test -> giữ trong mạch lộ trình học từ đầu đến cuối
                assessmentStatusBySkill[skill.SkillId] = "UNASSESSED";
                measuredPltBySkill[skill.SkillId] = 0.0;
                unassessedPatterns.Add(skill);
            }
        }

        // Phần 1 (WEAK): Topological Sort ưu tiên yếu nhất & trọng số cao nhất
        var weakIdSet = weakPatterns.Select(s => s.SkillId).ToHashSet();
        var weakPrereqs = weakPatterns.ToDictionary(
            s => s.SkillId,
            s => s.PrerequisiteIds.Where(pid => weakIdSet.Contains(pid)).ToList());
        var weakMetadata = weakPatterns.ToDictionary(
            s => s.SkillId,
            s => new SkillSortMetadata(s.SkillId, measuredPltBySkill[s.SkillId], s.Weight, IsWeak: true));

        var sortedWeakIds = weakPatterns.Count > 0
            ? _topologicalSorter.Sort(weakPatterns.Select(s => s.SkillId).ToList(), weakPrereqs, weakMetadata)
            : new List<Guid>();

        // Phần 2 (UNASSESSED — nối tiếp lộ trình học từ đầu đến cuối):
        // Sắp xếp chuẩn trình tự SGK: Miền yếu trước -> Môn học -> Lớp 10 -> Lớp 11 -> Lớp 12 -> Chương 1..N (số tự nhiên) -> Bài 1..M (số tự nhiên) -> Tên dạng bài
        var sortedUnassessedIds = unassessedPatterns
            .OrderBy(s => diagnosticDomainAccuracy.GetValueOrDefault(s.DomainCode, 0.5))
            .ThenBy(s => GetSubjectSortOrder(s.SubjectName))
            .ThenBy(s => InferGradeLevel(s.ChapterName, s.LessonName, s.Name) is var g && g == 0 ? 99 : g)
            .ThenBy(s => ExtractChapterNumber(s.ChapterName))
            .ThenBy(s => s.ChapterName)
            .ThenBy(s => ExtractLessonNumber(s.LessonName))
            .ThenBy(s => s.LessonName)
            .ThenBy(s => s.Name)
            .Select(s => s.SkillId)
            .ToList();

        // Phần 3 (MASTERED — đã làm bài test đạt điểm OK, hiển thị ở sau cùng):
        var sortedMasteredIds = masteredPatterns
            .OrderBy(s => GetSubjectSortOrder(s.SubjectName))
            .ThenBy(s => InferGradeLevel(s.ChapterName, s.LessonName, s.Name) is var g && g == 0 ? 99 : g)
            .ThenBy(s => ExtractChapterNumber(s.ChapterName))
            .ThenBy(s => s.ChapterName)
            .ThenBy(s => ExtractLessonNumber(s.LessonName))
            .ThenBy(s => s.Name)
            .Select(s => s.SkillId)
            .ToList();

        var orderedSkillIds = new List<Guid>(learningPatterns.Count);
        orderedSkillIds.AddRange(sortedWeakIds);
        orderedSkillIds.AddRange(sortedUnassessedIds);
        orderedSkillIds.AddRange(sortedMasteredIds);

        // =====================================================================
        // Bước 5: Đóng gói toàn bộ chặng học với MaterialId thật & Mở khóa linh hoạt sau khi đóng học phí
        // =====================================================================
        var resourceBindings = new Dictionary<Guid, SkillResourceBinding>();
        foreach (var skillId in orderedSkillIds)
        {
            skillMap.TryGetValue(skillId, out var skInfo);
            resourceBindings[skillId] = new SkillResourceBinding(
                SkillId: skillId,
                MaterialId: skInfo?.MaterialId,
                QuizExamId: null
            );
        }

        var roadmapId = Guid.NewGuid();
        var prunedSkillIds = new HashSet<Guid>(); // Không cắt bỏ phổ kiến thức Lớp 10-12
        var nodes = _milestoneBinder.BindMilestones(
            roadmapId,
            orderedSkillIds,
            resourceBindings,
            prunedSkillIds
        );

        // Giữ chặng #1 là IN_PROGRESS (ưu tiên học ngay), các chặng còn lại ở trạng thái UNLOCKED để học sinh học liền mạch từ đầu đến cuối
        var nowUtc = DateTime.UtcNow;
        foreach (var node in nodes)
        {
            if (node.Status == "LOCKED")
            {
                node.Status = "UNLOCKED";
                node.UnlockedAt = nowUtc;
            }
        }

        var roadmap = new LearningRoadmap
        {
            RoadmapId = roadmapId,
            StudentId = request.StudentId,
            DiagnosticSubmissionId = request.DiagnosticSubmissionId,
            TargetScore = targetScore,
            TotalMilestones = nodes.Count,
            CompletedMilestones = 0,
            IsPruned = false,
            PrunedReason = null,
            Status = "ACTIVE",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            Nodes = nodes
        };

        // =====================================================================
        // Bước 6: Lưu trữ nguyên tử (Archive lộ trình cũ + Thêm lộ trình toàn diện mới)
        // =====================================================================
        try
        {
            await _roadmapRepository.ArchiveExistingActiveRoadmapsAsync(request.StudentId, ct);
            await _roadmapRepository.AddAsync(roadmap, ct);
            await _roadmapRepository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Khởi tạo thành công Roadmap toàn diện {RoadmapId} cho học sinh {StudentId}: Total={Total} (Weak={Weak} -> Unassessed={Unassessed} -> Mastered={Mastered})",
                roadmap.RoadmapId, request.StudentId, nodes.Count, sortedWeakIds.Count, sortedUnassessedIds.Count, sortedMasteredIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu trữ lộ trình học tập vào CSDL.");
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("Database.SaveFailed", "Không thể lưu trữ lộ trình học tập vào CSDL."));
        }

        var nodeDtos = roadmap.Nodes
            .OrderBy(n => n.StepOrder)
            .Select(n =>
            {
                var skillInfo = skillMap.GetValueOrDefault(n.SkillId);
                string assessStatus = assessmentStatusBySkill.GetValueOrDefault(n.SkillId, "UNASSESSED");
                double plt = measuredPltBySkill.GetValueOrDefault(n.SkillId, 0.0);
                int gradeLevel = InferGradeLevel(skillInfo?.ChapterName, skillInfo?.LessonName, skillInfo?.Name);

                return new RoadmapNodeSummaryDto(
                    n.NodeId,
                    n.SkillId,
                    skillInfo?.Name ?? "Kỹ năng chuyên đề",
                    skillInfo?.DomainId ?? Guid.Empty,
                    !string.IsNullOrWhiteSpace(skillInfo?.DomainName) ? skillInfo.DomainName : "Lĩnh vực chung",
                    n.StepOrder,
                    n.MaterialId ?? skillInfo?.MaterialId,
                    n.QuizExamId,
                    n.Status,
                    n.IsPruned,
                    n.UnlockedAt,
                    n.CompletedAt,
                    skillInfo?.DomainCode ?? "",
                    skillInfo?.SubjectName ?? "",
                    skillInfo?.ChapterName ?? "",
                    skillInfo?.LessonName ?? "",
                    skillInfo?.VideoUrl ?? "",
                    Math.Round(plt, 4),
                    n.Status == "IN_PROGRESS" ? "PREVIEW" : "",
                    assessStatus,
                    gradeLevel
                );
            })
            .ToList();

        var stageDtos = nodeDtos
            .GroupBy(n => new { n.DomainId, n.DomainName, n.DomainCode })
            .Select(g => new RoadmapStageDto(
                g.Key.DomainId,
                g.Key.DomainName,
                g.Count(),
                g.Count(n => n.Status == "COMPLETED"),
                g.OrderBy(n => n.StepOrder).ToList(),
                g.Key.DomainCode
            ))
            .ToList();

        var responseDto = new GenerateRoadmapResponseDto(
            roadmap.RoadmapId,
            roadmap.StudentId,
            roadmap.DiagnosticSubmissionId,
            roadmap.TargetScore,
            roadmap.TotalMilestones,
            roadmap.CompletedMilestones,
            roadmap.IsPruned,
            roadmap.PrunedReason,
            roadmap.Status,
            roadmap.CreatedAt,
            stageDtos,
            nodeDtos,
            submission.PlacementClass ?? "ACCELERATION"
        );

        return Result<GenerateRoadmapResponseDto>.Success(responseDto);
    }

    internal static int InferGradeLevel(string? chapterName, string? lessonName, string? skillName)
    {
        var text = $"{chapterName} {lessonName} {skillName}".ToLowerInvariant();
        if (text.Contains("lớp 10") || text.Contains("lop 10") || text.Contains("khối 10") || text.Contains("- 10)")) return 10;
        if (text.Contains("lớp 11") || text.Contains("lop 11") || text.Contains("khối 11") || text.Contains("- 11)")) return 11;
        if (text.Contains("lớp 12") || text.Contains("lop 12") || text.Contains("khối 12") || text.Contains("- 12)")) return 12;
        return 0;
    }

    internal static int ExtractChapterNumber(string? chapterName)
    {
        if (string.IsNullOrWhiteSpace(chapterName)) return 999;
        var m = Regex.Match(chapterName, @"Chương\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int num)) return num;
        return 999;
    }

    internal static int ExtractLessonNumber(string? lessonName)
    {
        if (string.IsNullOrWhiteSpace(lessonName)) return 999;
        var m = Regex.Match(lessonName, @"Bài\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int num)) return num;
        return 999;
    }

    internal static int GetSubjectSortOrder(string? subjectName) => (subjectName ?? string.Empty).Trim() switch
    {
        "Toán học" => 1,
        "Ngữ văn" => 2,
        "Tiếng Anh" => 3,
        "Vật lí" => 4,
        "Hóa học" => 5,
        "Sinh học" => 6,
        "Lịch sử" => 7,
        "Địa lí" => 8,
        "GDKT & Pháp luật" => 9,
        _ => 99
    };
}
