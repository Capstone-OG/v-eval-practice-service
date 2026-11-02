using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;
using V_Eval_Practice_Service.Domain.Constants;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Commands.SubmitDiagnostic;

public class SubmitDiagnosticCommandHandler : IRequestHandler<SubmitDiagnosticCommand, Result<SubmitDiagnosticResponseDto>>
{
    private const string DefaultCampusId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string DefaultCampusName = "Cơ sở 1: ĐHQG TP.HCM";

    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly IClassHeatmapRepository _heatmapRepository;
    private readonly IStudentActivityRepository _activityRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IAiDiagnosticClient _aiDiagnosticClient;
    private readonly ILogger<SubmitDiagnosticCommandHandler> _logger;

    public SubmitDiagnosticCommandHandler(
        IExamSubmissionRepository submissionRepository,
        ILearningProfileRepository learningProfileRepository,
        IClassEnrollmentRepository classEnrollmentRepository,
        IClassHeatmapRepository heatmapRepository,
        IStudentActivityRepository activityRepository,
        IIdentityGrpcClient identityGrpcClient,
        IContentGrpcClient contentGrpcClient,
        IAiDiagnosticClient aiDiagnosticClient,
        ILogger<SubmitDiagnosticCommandHandler> logger)
    {
        _submissionRepository = submissionRepository;
        _learningProfileRepository = learningProfileRepository;
        _classEnrollmentRepository = classEnrollmentRepository;
        _heatmapRepository = heatmapRepository;
        _activityRepository = activityRepository;
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

        if (request.StudentId == Guid.Empty)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.Validation("Student.Invalid", "Mã định danh học sinh không hợp lệ."));
        }

        // 1. Lấy thông tin học sinh & Campus qua Identity gRPC (hỗ trợ fallback mềm nếu học sinh mới chưa gán Campus)
        string campusId = DefaultCampusId;
        string campusName = DefaultCampusName;
        int targetScore = request.TargetScore is > 0 ? request.TargetScore.Value : 900;

        try
        {
            var profile = await _identityGrpcClient.GetStudentProfileAsync(request.StudentId, cancellationToken);
            if (profile.Exists)
            {
                if (!string.IsNullOrWhiteSpace(profile.CampusId))
                {
                    campusId = profile.CampusId;
                    campusName = !string.IsNullOrWhiteSpace(profile.CampusName) ? profile.CampusName : DefaultCampusName;
                }

                if (profile.TargetScore > 0)
                {
                    targetScore = profile.TargetScore;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Identity gRPC unavailable when submitting diagnostic for Student {StudentId}. Using default campus {CampusId}.",
                request.StudentId, campusId);
        }

        // 2. Kiểm tra trường hợp phiên làm bài hiện tại quá 24 giờ (Unhappy Case: EXPIRED)
        DateTime completedAtUtc = request.CompletedAt.HasValue
            ? DateTime.SpecifyKind(request.CompletedAt.Value.ToUniversalTime(), DateTimeKind.Utc)
            : DateTime.UtcNow;

        bool isExpired = false;
        DateTime startTime = request.StartedAt.HasValue
            ? DateTime.SpecifyKind(request.StartedAt.Value.ToUniversalTime(), DateTimeKind.Utc)
            : completedAtUtc;

        if (request.StartedAt.HasValue && (completedAtUtc - startTime).TotalHours > 24)
        {
            isExpired = true;
        }
        else if (request.Answers != null && request.Answers.Sum(a => a.TimeSpentSeconds) > 86400)
        {
            isExpired = true;
            startTime = completedAtUtc.AddSeconds(-request.Answers.Sum(a => a.TimeSpentSeconds));
        }

        if (isExpired)
        {
            var expiredSubmission = new ExamSubmission
            {
                SubmissionId = Guid.NewGuid(),
                StudentId = request.StudentId,
                ExamId = request.ExamId,
                ExamType = "DIAGNOSTIC",
                TotalScore = 0,
                TotalCorrect = 0,
                TotalQuestions = request.Answers?.Count ?? 0,
                TotalTimeSpentSeconds = (int)Math.Min(Math.Max(0, (completedAtUtc - startTime).TotalSeconds), int.MaxValue),
                StartedAt = startTime,
                CompletedAt = completedAtUtc,
                Status = "EXPIRED",
                Theta0 = null,
                PlacementClass = null,
                AiCommentary = "Bài kiểm tra chẩn đoán đã quá thời hạn 24 giờ do học sinh bỏ dở (EXPIRED). Hệ thống đã tự động khóa phiên làm bài quá hạn để bảo vệ độ tin cậy của tham số năng lực theta_0."
            };

            await _submissionRepository.AddAsync(expiredSubmission, cancellationToken);
            await _submissionRepository.SaveChangesAsync(cancellationToken);

            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.Validation("Exam.Expired", "Phiên làm bài kiểm tra chẩn đoán đã quá 24 giờ và đã hết hạn (EXPIRED). Vui lòng làm một bài chẩn đoán mới."));
        }

        // 3. Lấy bảng đáp án từ Content Service gRPC hoặc tổng hợp từ metadata đề thi AI (Gemini / Fallback)
        var answersList = request.Answers ?? new List<QuestionAnswerInputDto>();
        IReadOnlyDictionary<Guid, ExamQuestionKeyDto> answerKeys = new Dictionary<Guid, ExamQuestionKeyDto>();

        try
        {
            if (request.ExamId != Guid.Empty)
            {
                answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(request.ExamId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Content gRPC unavailable for Exam {ExamId}. Falling back to client-supplied question metadata if available.",
                request.ExamId);
        }

        var normalizedInputAnswers = new List<(Guid ResolvedQuestionId, QuestionAnswerInputDto Input)>();
        var usedQuestionIds = new HashSet<Guid>();
        for (int idx = 0; idx < answersList.Count; idx++)
        {
            var item = answersList[idx];
            var qId = item.QuestionId;
            if (qId == Guid.Empty || !usedQuestionIds.Add(qId))
            {
                qId = ToDeterministicGuid($"q:{request.ExamId}:{idx + 1}:{item.SkillId}");
                usedQuestionIds.Add(qId);
            }
            normalizedInputAnswers.Add((qId, item));
        }

        if (answerKeys.Count == 0 && normalizedInputAnswers.Any(x => !string.IsNullOrWhiteSpace(x.Input.CorrectOption)))
        {
            var syntheticKeys = new Dictionary<Guid, ExamQuestionKeyDto>();
            for (int idx = 0; idx < normalizedInputAnswers.Count; idx++)
            {
                var (qId, input) = normalizedInputAnswers[idx];
                string domainCode = NormalizeDomainCode(input.DomainId, input.DomainName);
                string domainName = !string.IsNullOrWhiteSpace(input.DomainName)
                    ? input.DomainName!
                    : GetDefaultDomainName(domainCode);
                string skillId = !string.IsNullOrWhiteSpace(input.SkillId)
                    ? input.SkillId!
                    : $"sk_{domainCode.ToLowerInvariant()}";
                string skillName = !string.IsNullOrWhiteSpace(input.SkillName)
                    ? input.SkillName!
                    : domainName;
                int diff = Math.Clamp(input.DifficultyLevel ?? 2, 1, 6);

                syntheticKeys[qId] = new ExamQuestionKeyDto(
                    QuestionId: qId,
                    QuestionOrder: idx + 1,
                    CorrectOption: (input.CorrectOption ?? "A").Trim().ToUpperInvariant(),
                    SkillId: skillId,
                    DifficultyLevel: diff,
                    SkillName: skillName,
                    DomainId: domainCode,
                    DomainName: domainName
                );
            }
            answerKeys = syntheticKeys;
        }

        if (answerKeys.Count == 0)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Exam.NotFound", $"Không tìm thấy bộ đề thi hoặc đề thi {request.ExamId} chưa có câu hỏi."));
        }

        // 4. Tiến hành chấm điểm và ghi nhận vi mô từng câu
        var studentAnswersMap = normalizedInputAnswers
            .GroupBy(a => a.ResolvedQuestionId)
            .ToDictionary(g => g.Key, g => g.First().Input);

        var submissionAnswers = new List<SubmissionAnswer>();
        var questionResults = new List<QuestionResultDto>();

        int totalCorrect = 0;
        int totalTimeSpent = 0;
        var submissionId = Guid.NewGuid();

        foreach (var key in answerKeys.Values.OrderBy(k => k.QuestionOrder))
        {
            studentAnswersMap.TryGetValue(key.QuestionId, out var studentAns);

            string? selectedOption = studentAns?.SelectedOption?.Trim().ToUpperInvariant();
            int timeSpent = Math.Max(0, studentAns?.TimeSpentSeconds ?? 0);
            totalTimeSpent += timeSpent;

            bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                             string.Equals(selectedOption, key.CorrectOption?.Trim().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                totalCorrect++;
            }

            submissionAnswers.Add(new SubmissionAnswer
            {
                AnswerId = Guid.NewGuid(),
                SubmissionId = submissionId,
                QuestionId = key.QuestionId,
                SelectedOption = selectedOption,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent
            });

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

        // 5. Phân tích chẩn đoán theo Kỹ năng & Độ khó
        var skillBreakdowns = questionResults
            .GroupBy(q => string.IsNullOrWhiteSpace(q.SkillId) ? "sk_default" : q.SkillId)
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
                    SkillName = !string.IsNullOrWhiteSpace(first.SkillName) ? first.SkillName : g.Key,
                    DomainId = first.DomainId,
                    DomainName = first.DomainName,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct,
                    IsWeak = isWeak
                };
            })
            .ToList();

        var weakSkillIds = skillBreakdowns.Where(s => s.IsWeak).Select(s => s.SkillId).ToList();
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

        var difficultyBreakdowns = questionResults
            .GroupBy(q => q.DifficultyLevel)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                return new DifficultyBreakdownDto
                {
                    DifficultyLevel = g.Key,
                    LevelName = BloomTaxonomy.GetName(g.Key),
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct
                };
            })
            .ToList();

        // 6. Gửi dữ liệu sang AI Subsystem ước lượng theta_0 (IRT 2PL) & BKT P(L0) (có fallback tất định)
        var domainNamePayloads = questionResults
            .Where(q => !string.IsNullOrEmpty(q.DomainId))
            .GroupBy(q => q.DomainId)
            .Select(g => new DiagnosticDomainNamePayload(g.Key, g.First().DomainName))
            .ToList();

        if (domainNamePayloads.Count == 0)
        {
            domainNamePayloads = new List<DiagnosticDomainNamePayload>
            {
                new("DOM_LANG", "Sử dụng Ngôn ngữ"),
                new("DOM_MATH", "Toán & Tư duy Logic"),
                new("DOM_NAT_SCI", "Khoa học Tự nhiên"),
                new("DOM_SOC_SCI", "Khoa học Xã hội")
            };
        }

        DiagnosticAnalyzeResponsePayload? aiResult = null;
        try
        {
            var aiPayload = new DiagnosticAnalyzeRequestPayload(
                StudentId: request.StudentId.ToString(),
                SubmissionId: submissionId.ToString(),
                TargetScore: targetScore,
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

            aiResult = await _aiDiagnosticClient.AnalyzeDiagnosticAsync(aiPayload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI Diagnostic Subsystem unavailable for Submission {SubmissionId}. Using deterministic IRT 2PL logit calculation.",
                submissionId);
        }

        double clampedP = Math.Clamp(totalQuestions > 0 ? totalCorrect / (double)totalQuestions : 0.5, 0.04, 0.96);
        double fallbackTheta0 = Math.Round(Math.Log(clampedP / (1.0 - clampedP)), 2);
        double theta0 = aiResult?.Theta0 ?? fallbackTheta0;
        string placementClass = aiResult?.PlacementClass
            ?? (theta0 >= 0.5 ? "BREAKTHROUGH" : theta0 >= -0.5 ? "ACCELERATION" : "FOUNDATION");
        string aiCommentary = !string.IsNullOrWhiteSpace(aiResult?.AiCommentary)
            ? aiResult!.AiCommentary
            : $"Hoàn thành {totalCorrect}/{totalQuestions} câu ({accuracyPercentage}%), năng lực ước tính IRT theta_0 = {theta0:0.##}.";

        // 7. Lưu trữ bài thi vào PostgreSQL
        DateTime actualStartedAt = request.StartedAt.HasValue
            ? startTime
            : completedAtUtc.AddSeconds(-Math.Max(1, totalTimeSpent));

        var submission = new ExamSubmission
        {
            SubmissionId = submissionId,
            StudentId = request.StudentId,
            ExamId = request.ExamId == Guid.Empty ? ToDeterministicGuid(request.ExamTitle ?? "diagnostic_default") : request.ExamId,
            ExamType = "DIAGNOSTIC",
            TotalScore = totalCorrect,
            TotalCorrect = totalCorrect,
            TotalQuestions = totalQuestions,
            TotalTimeSpentSeconds = totalTimeSpent,
            StartedAt = actualStartedAt,
            CompletedAt = completedAtUtc,
            Status = "COMPLETED",
            Theta0 = theta0,
            PlacementClass = placementClass,
            AiCommentary = aiCommentary,
            MentorNotes = !string.IsNullOrWhiteSpace(request.ExamTitle) ? request.ExamTitle.Trim() : null,
            Answers = submissionAnswers
        };

        await _submissionRepository.AddAsync(submission, cancellationToken);
        await _submissionRepository.SaveChangesAsync(cancellationToken);

        // 8. Lưu giá trị tiên nghiệm BKT P(L0) vào LearningProfiles (hỗ trợ cả SkillId dạng chuỗi lẫn UUID)
        var priorsBySkillKey = new Dictionary<Guid, double>();
        if (aiResult?.SkillPriors != null && aiResult.SkillPriors.Count > 0)
        {
            foreach (var sp in aiResult.SkillPriors)
            {
                var skGuid = ToSkillGuid(sp.SkillId);
                priorsBySkillKey[skGuid] = Math.Clamp(sp.PL0, 0.05, 0.98);
            }
        }
        else
        {
            foreach (var sb in skillBreakdowns)
            {
                var skGuid = ToSkillGuid(sb.SkillId);
                priorsBySkillKey[skGuid] = Math.Clamp(sb.AccuracyPercentage / 100.0, 0.05, 0.98);
            }
        }

        if (priorsBySkillKey.Count > 0)
        {
            var priorsToUpsert = priorsBySkillKey
                .Select(kv => (SkillId: kv.Key, MasteryScore: kv.Value))
                .ToList();
            await _learningProfileRepository.UpsertSkillPriorsAsync(request.StudentId, priorsToUpsert, cancellationToken);
        }

        // 9. Tự động xếp học sinh vào lớp học tại cơ sở (Core Flow 1 - Bước 5)
        var (classId, className, enrollmentId) = await _classEnrollmentRepository.EnrollStudentAsync(
            request.StudentId,
            campusId,
            placementClass,
            submissionId,
            campusName,
            cancellationToken);

        submission.EnrolledClassId = classId;
        await _submissionRepository.SaveChangesAsync(cancellationToken);

        // 10. Đồng bộ sang Core Flow 5: Cập nhật ClassSkillHeatmaps & StudentDailyActivities (Streak)
        var heatmapCells = new List<ClassSkillHeatmap>();
        foreach (var sb in skillBreakdowns)
        {
            var skGuid = ToSkillGuid(sb.SkillId);
            double plt = priorsBySkillKey.TryGetValue(skGuid, out var pVal)
                ? pVal
                : Math.Clamp(sb.AccuracyPercentage / 100.0, 0.05, 0.98);

            string canonicalDomain = NormalizeDomainCode(sb.DomainId, sb.DomainName);
            string masteryLevel = plt < 0.30
                ? "CRITICAL"
                : plt < 0.50
                    ? "WEAK"
                    : plt < 0.70
                        ? "DEVELOPING"
                        : "MASTERED";

            heatmapCells.Add(new ClassSkillHeatmap
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                StudentId = request.StudentId,
                SkillId = skGuid,
                SkillName = !string.IsNullOrWhiteSpace(sb.SkillName) ? sb.SkillName : GetDefaultDomainName(canonicalDomain),
                DomainCode = canonicalDomain,
                PltValue = Math.Round(plt, 4),
                MasteryLevel = masteryLevel,
                LastAssessmentType = "DIAGNOSTIC",
                UpdatedAt = completedAtUtc
            });
        }

        if (heatmapCells.Count > 0)
        {
            // Gộp trùng theo SkillId trước khi batch upsert để tránh xung đột unique constraint (class_id, student_id, skill_id)
            var deduplicatedCells = heatmapCells
                .GroupBy(c => c.SkillId)
                .Select(g =>
                {
                    var first = g.First();
                    double avgPlt = Math.Round(g.Average(x => x.PltValue), 4);
                    first.PltValue = avgPlt;
                    first.MasteryLevel = avgPlt < 0.30 ? "CRITICAL" : avgPlt < 0.50 ? "WEAK" : avgPlt < 0.70 ? "DEVELOPING" : "MASTERED";
                    return first;
                })
                .ToList();

            await _heatmapRepository.BatchUpsertHeatmapCellsAsync(deduplicatedCells, cancellationToken);
        }

        var activityDate = DateTime.SpecifyKind(completedAtUtc.Date, DateTimeKind.Utc);
        var existingActivity = await _activityRepository.GetDailyActivityAsync(request.StudentId, activityDate, cancellationToken);
        int addedMinutes = Math.Max(1, (int)Math.Ceiling(totalTimeSpent / 60.0));

        var dailyActivity = existingActivity ?? new StudentDailyActivity
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            ActivityDate = activityDate,
            CreatedAt = completedAtUtc
        };

        dailyActivity.StudyTimeMinutes += addedMinutes;
        dailyActivity.PracticeQuestionsSolved += totalQuestions;
        dailyActivity.MockExamsTaken += 1;
        dailyActivity.IsStreakCounted = true;
        dailyActivity.UpdatedAt = completedAtUtc;

        await _activityRepository.UpsertDailyActivityAsync(dailyActivity, cancellationToken);

        _logger.LogInformation(
            "Diagnostic submission {SubmissionId} persisted & synced to Flow 5: correct={Correct}/{Total}, theta_0={Theta0}, class={ClassName}",
            submissionId, totalCorrect, totalQuestions, theta0, className);

        // 11. Tạo DTO phản hồi hoàn chỉnh cho Client
        var radarDtos = aiResult?.RadarChart?.Select(r => new DiagnosticRadarAxisDto
        {
            DomainId = r.DomainId,
            DomainName = r.DomainName,
            StudentPct = r.StudentPct,
            BenchmarkPct = r.BenchmarkPct
        }).ToList();

        if (radarDtos == null || radarDtos.Count == 0)
        {
            radarDtos = questionResults
                .GroupBy(q => NormalizeDomainCode(q.DomainId, q.DomainName))
                .Select(g => new DiagnosticRadarAxisDto
                {
                    DomainId = g.Key,
                    DomainName = GetDefaultDomainName(g.Key),
                    StudentPct = Math.Round((g.Count(x => x.IsCorrect) / (double)Math.Max(1, g.Count())) * 100.0, 1),
                    BenchmarkPct = g.Key == "DOM_MATH" ? 85.0 : 80.0
                })
                .ToList();
        }

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
            Status = submission.Status,
            CampusId = campusId,
            CampusName = campusName,
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

    private static Guid ToSkillGuid(string? rawSkillId)
    {
        if (!string.IsNullOrWhiteSpace(rawSkillId) && Guid.TryParse(rawSkillId, out var parsed))
        {
            return parsed;
        }
        return ToDeterministicGuid($"skill:{rawSkillId ?? "default"}");
    }

    private static Guid ToDeterministicGuid(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }

    private static string NormalizeDomainCode(string? domainId, string? domainName)
    {
        string raw = $"{domainId} {domainName}".ToLowerInvariant();
        if (raw.Contains("dom_lang") || raw.Contains("ngôn ngữ") || raw.Contains("tiếng việt") || raw.Contains("english") || raw.Contains("văn"))
        {
            return "DOM_LANG";
        }
        if (raw.Contains("dom_nat") || raw.Contains("tự nhiên") || raw.Contains("vật lý") || raw.Contains("hóa") || raw.Contains("sinh"))
        {
            return "DOM_NAT_SCI";
        }
        if (raw.Contains("dom_soc") || raw.Contains("xã hội") || raw.Contains("lịch sử") || raw.Contains("địa lý"))
        {
            return "DOM_SOC_SCI";
        }
        return "DOM_MATH";
    }

    private static string GetDefaultDomainName(string domainCode) => domainCode switch
    {
        "DOM_LANG" => "Sử dụng Ngôn ngữ (Việt - Anh)",
        "DOM_NAT_SCI" => "Giải quyết vấn đề - Khoa học Tự nhiên",
        "DOM_SOC_SCI" => "Giải quyết vấn đề - Khoa học Xã hội",
        _ => "Toán học, Tư duy Logic & Phân tích số liệu"
    };
}
