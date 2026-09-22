using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Commands.SubmitDiagnostic;

public class SubmitDiagnosticCommandHandler : IRequestHandler<SubmitDiagnosticCommand, Result<SubmitDiagnosticResponseDto>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IAiDiagnosticClient _aiDiagnosticClient;
    private readonly ILogger<SubmitDiagnosticCommandHandler> _logger;

    public SubmitDiagnosticCommandHandler(
        IExamSubmissionRepository submissionRepository,
        ILearningProfileRepository learningProfileRepository,
        IClassEnrollmentRepository classEnrollmentRepository,
        IIdentityGrpcClient identityGrpcClient,
        IContentGrpcClient contentGrpcClient,
        IAiDiagnosticClient aiDiagnosticClient,
        ILogger<SubmitDiagnosticCommandHandler> logger)
    {
        _submissionRepository = submissionRepository;
        _learningProfileRepository = learningProfileRepository;
        _classEnrollmentRepository = classEnrollmentRepository;
        _identityGrpcClient = identityGrpcClient;
        _contentGrpcClient = contentGrpcClient;
        _aiDiagnosticClient = aiDiagnosticClient;
        _logger = logger;
    }

    public async Task<Result<SubmitDiagnosticResponseDto>> Handle(
        SubmitDiagnosticCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing diagnostic submission for Student {StudentId}, Exam {ExamId}",
            request.StudentId, request.ExamId);

        // 1. Kiểm tra học sinh và điều kiện chọn Campus qua Identity gRPC
        var (exists, campusId, campusName, targetScore) = await _identityGrpcClient.GetStudentProfileAsync(request.StudentId, cancellationToken);
        if (!exists)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Student.NotFound", $"Không tìm thấy thông tin học sinh {request.StudentId} trong hệ thống."));
        }

        if (string.IsNullOrWhiteSpace(campusId))
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.Validation("Student.CampusRequired", "Học sinh cần chọn cơ sở đào tạo (Campus) trước khi thực hiện bài kiểm tra chẩn đoán năng lực."));
        }

        // 2. Lấy bảng đáp án và metadata từ Content Service gRPC
        var answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(request.ExamId, cancellationToken);
        if (answerKeys.Count == 0)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Exam.NotFound", $"Không tìm thấy bộ đề thi hoặc đề thi {request.ExamId} chưa có câu hỏi."));
        }

        // 3. Tiến hành chấm điểm và ghi nhận vi mô từng câu
        var studentAnswersMap = request.Answers.ToDictionary(a => a.QuestionId, a => a);
        var submissionAnswers = new List<SubmissionAnswer>();
        var questionResults = new List<QuestionResultDto>();

        int totalCorrect = 0;
        int totalTimeSpent = 0;

        var submissionId = Guid.NewGuid();

        foreach (var key in answerKeys.Values.OrderBy(k => k.QuestionOrder))
        {
            studentAnswersMap.TryGetValue(key.QuestionId, out var studentAns);

            string? selectedOption = studentAns?.SelectedOption?.Trim().ToUpperInvariant();
            int timeSpent = studentAns?.TimeSpentSeconds ?? 0;
            totalTimeSpent += timeSpent;

            bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                             string.Equals(selectedOption, key.CorrectOption?.Trim().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                totalCorrect++;
            }

            var answerEntity = new SubmissionAnswer
            {
                AnswerId = Guid.NewGuid(),
                SubmissionId = submissionId,
                QuestionId = key.QuestionId,
                SelectedOption = selectedOption,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent
            };
            submissionAnswers.Add(answerEntity);

            questionResults.Add(new QuestionResultDto
            {
                QuestionId = key.QuestionId,
                QuestionOrder = key.QuestionOrder,
                SelectedOption = selectedOption,
                CorrectOption = key.CorrectOption ?? string.Empty,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent,
                SkillId = key.SkillId,
                SkillName = key.SkillName,
                DomainId = key.DomainId,
                DomainName = key.DomainName,
                DifficultyLevel = key.DifficultyLevel
            });
        }

        int totalQuestions = answerKeys.Count;
        double accuracyPercentage = totalQuestions > 0
            ? Math.Round((totalCorrect / (double)totalQuestions) * 100, 2)
            : 0;

        // 4. Phân tích chẩn đoán theo Kỹ năng (Skill Breakdown & Weak Skills)
        var skillBreakdowns = questionResults
            .GroupBy(q => q.SkillId)
            .Select(g =>
            {
                var first = g.First();
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                bool isWeak = pct < 60.0;
                return new SkillDiagnosticDto
                {
                    SkillId = g.Key,
                    SkillName = first.SkillName,
                    DomainId = first.DomainId,
                    DomainName = first.DomainName,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct,
                    IsWeak = isWeak
                };
            })
            .ToList();

        var weakSkillIds = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => s.SkillId)
            .ToList();

        var weakSkills = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => new WeakSkillDto
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName,
                DomainName = s.DomainName,
                TotalQuestions = s.TotalQuestions,
                CorrectCount = s.CorrectCount,
                AccuracyPercentage = s.AccuracyPercentage
            })
            .ToList();

        // 5. Phân tích chẩn đoán theo Độ khó (Difficulty Breakdown)
        var difficultyBreakdowns = questionResults
            .GroupBy(q => q.DifficultyLevel)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                string levelName = g.Key switch
                {
                    1 => "Dễ (Nhận biết)",
                    2 => "Trung bình (Thông hiểu)",
                    3 => "Khó (Vận dụng)",
                    4 => "Rất khó (Vận dụng cao)",
                    _ => $"Mức {g.Key}"
                };

                return new DifficultyBreakdownDto
                {
                    DifficultyLevel = g.Key,
                    LevelName = levelName,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct
                };
            })
            .ToList();

        // 6. Gửi dữ liệu sang AI Subsystem ước lượng theta_0 (IRT 2PL), BKT Prior P(L0), Radar Chart (Core Flow 1 - Bước 4)
        var domainNamePayloads = questionResults
            .Where(q => !string.IsNullOrEmpty(q.DomainId))
            .GroupBy(q => q.DomainId)
            .Select(g => new DiagnosticDomainNamePayload(g.Key, g.First().DomainName))
            .ToList();

        if (domainNamePayloads.Count == 0)
        {
            domainNamePayloads = new List<DiagnosticDomainNamePayload>
            {
                new("dom_math", "Toán học & Logic"),
                new("dom_lang", "Sử dụng ngôn ngữ"),
                new("dom_nat_sci", "Khoa học tự nhiên"),
                new("dom_soc_sci", "Khoa học xã hội")
            };
        }

        var aiPayload = new DiagnosticAnalyzeRequestPayload(
            StudentId: request.StudentId.ToString(),
            SubmissionId: submissionId.ToString(),
            TargetScore: targetScore > 0 ? targetScore : 800,
            Answers: questionResults.Select(q => new DiagnosticAnswerItemPayload(
                QuestionId: q.QuestionId.ToString(),
                SkillId: q.SkillId,
                DomainId: q.DomainId,
                DifficultyLevel: q.DifficultyLevel,
                IsCorrect: q.IsCorrect,
                TimeSpentSeconds: q.TimeSpentSeconds
            )).ToList(),
            DomainNames: domainNamePayloads,
            AllSkillIds: new Dictionary<string, string>()
        );

        var aiResult = await _aiDiagnosticClient.AnalyzeDiagnosticAsync(aiPayload, cancellationToken);
        double theta0 = aiResult?.Theta0 ?? 0.0;
        string placementClass = aiResult?.PlacementClass ?? "ACCELERATION";
        string aiCommentary = aiResult?.AiCommentary ?? string.Empty;

        // 7. Lưu trữ bài thi vào CSDL Supabase
        var submission = new ExamSubmission
        {
            SubmissionId = submissionId,
            StudentId = request.StudentId,
            ExamId = request.ExamId,
            ExamType = "DIAGNOSTIC",
            TotalScore = totalCorrect, // Thang điểm 0 - 30
            TotalCorrect = totalCorrect,
            TotalQuestions = totalQuestions,
            TotalTimeSpentSeconds = totalTimeSpent,
            StartedAt = request.StartedAt ?? DateTime.UtcNow.AddSeconds(-totalTimeSpent),
            CompletedAt = DateTime.UtcNow,
            Status = "COMPLETED",
            Theta0 = theta0,
            PlacementClass = placementClass,
            AiCommentary = aiCommentary,
            Answers = submissionAnswers
        };

        await _submissionRepository.AddAsync(submission, cancellationToken);
        await _submissionRepository.SaveChangesAsync(cancellationToken);

        // 8. Lưu giá trị tiên nghiệm BKT P(L0) vào LearningProfiles
        if (aiResult?.SkillPriors != null && aiResult.SkillPriors.Count > 0)
        {
            var priorsToUpsert = aiResult.SkillPriors
                .Where(sp => Guid.TryParse(sp.SkillId, out _))
                .Select(sp => (SkillId: Guid.Parse(sp.SkillId), MasteryScore: sp.PL0))
                .ToList();

            if (priorsToUpsert.Count > 0)
            {
                await _learningProfileRepository.UpsertSkillPriorsAsync(request.StudentId, priorsToUpsert, cancellationToken);
            }
        }

        // 9. Tự động xếp học sinh vào lớp học tại cơ sở (Core Flow 1 - Bước 5)
        var (classId, className, enrollmentId) = await _classEnrollmentRepository.EnrollStudentAsync(
            request.StudentId,
            campusId,
            placementClass,
            submissionId,
            campusName ?? string.Empty,
            cancellationToken);

        submission.EnrolledClassId = classId;
        await _submissionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Diagnostic submission {SubmissionId} finalized: theta_0={Theta0}, placement={Placement}, class={ClassName}",
            submissionId, theta0, placementClass, className);

        // 10. Tạo DTO phản hồi hoàn chỉnh cho Client bao gồm Biểu đồ Radar (Happy case < 2s)
        var radarDtos = aiResult?.RadarChart?.Select(r => new DiagnosticRadarAxisDto
        {
            DomainId = r.DomainId,
            DomainName = r.DomainName,
            StudentPct = r.StudentPct,
            BenchmarkPct = r.BenchmarkPct
        }).ToList() ?? new List<DiagnosticRadarAxisDto>();

        var skillNameMap = questionResults
            .Where(q => !string.IsNullOrEmpty(q.SkillId))
            .GroupBy(q => q.SkillId)
            .ToDictionary(g => g.Key, g => (SkillName: g.First().SkillName, DomainName: g.First().DomainName));

        var skillPriorDtos = aiResult?.SkillPriors?.Select(p =>
        {
            skillNameMap.TryGetValue(p.SkillId, out var info);
            return new DiagnosticSkillPriorDto
            {
                SkillId = p.SkillId,
                SkillName = !string.IsNullOrEmpty(info.SkillName) ? info.SkillName : p.SkillId,
                DomainId = p.DomainId,
                DomainName = !string.IsNullOrEmpty(info.DomainName) ? info.DomainName : p.DomainId,
                ThetaSkill = p.ThetaSkill,
                PL0 = p.PL0,
                Source = p.Source
            };
        }).ToList() ?? new List<DiagnosticSkillPriorDto>();

        var responseDto = new SubmitDiagnosticResponseDto
        {
            SubmissionId = submission.SubmissionId,
            StudentId = submission.StudentId,
            ExamId = submission.ExamId,
            ExamType = submission.ExamType,
            CampusId = campusId,
            CampusName = campusName ?? string.Empty,
            TotalScore = submission.TotalScore,
            TotalCorrect = submission.TotalCorrect,
            TotalQuestions = submission.TotalQuestions,
            AccuracyPercentage = accuracyPercentage,
            TotalTimeSpentSeconds = submission.TotalTimeSpentSeconds,
            StartedAt = submission.StartedAt,
            CompletedAt = submission.CompletedAt,
            SkillBreakdowns = skillBreakdowns,
            WeakSkillIds = weakSkillIds,
            WeakSkills = weakSkills,
            DifficultyBreakdowns = difficultyBreakdowns,
            Questions = questionResults,
            Theta0 = theta0,
            PlacementClass = placementClass,
            ClassName = className,
            ClassId = classId,
            EnrollmentId = enrollmentId,
            AiCommentary = aiCommentary,
            RadarChart = radarDtos,
            SkillPriors = skillPriorDtos
        };

        return Result<SubmitDiagnosticResponseDto>.Success(responseDto);
    }
}
