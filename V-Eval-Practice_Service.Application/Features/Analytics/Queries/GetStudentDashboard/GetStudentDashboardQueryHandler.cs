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
using V_Eval_Practice_Service.Application.Features.Analytics.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetStudentDashboard;

public class GetStudentDashboardQueryHandler : IRequestHandler<GetStudentDashboardQuery, Result<StudentDashboardResponseDto>>
{
    private readonly IStudentActivityRepository _activityRepository;
    private readonly IClassHeatmapRepository _heatmapRepository;
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly ILogger<GetStudentDashboardQueryHandler> _logger;

    public GetStudentDashboardQueryHandler(
        IStudentActivityRepository activityRepository,
        IClassHeatmapRepository heatmapRepository,
        IExamSubmissionRepository submissionRepository,
        ILearningRoadmapRepository roadmapRepository,
        IIdentityGrpcClient identityGrpcClient,
        ILogger<GetStudentDashboardQueryHandler> logger)
    {
        _activityRepository = activityRepository;
        _heatmapRepository = heatmapRepository;
        _submissionRepository = submissionRepository;
        _roadmapRepository = roadmapRepository;
        _identityGrpcClient = identityGrpcClient;
        _logger = logger;
    }

    public async Task<Result<StudentDashboardResponseDto>> Handle(GetStudentDashboardQuery request, CancellationToken ct)
    {
        var studentId = request.StudentId;

        // 1. Lấy mục tiêu điểm số của học sinh từ Identity gRPC & Roadmap
        string fullName = string.Empty;
        int targetScore = 900;
        try
        {
            var profile = await _identityGrpcClient.GetStudentProfileAsync(studentId, ct);
            if (profile.Exists && profile.TargetScore > 0)
            {
                targetScore = profile.TargetScore;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Flow 5] Không thể kết nối Identity gRPC để lấy thông tin Student {StudentId}, sử dụng mục tiêu mặc định.",
                studentId);
        }

        var roadmap = await _roadmapRepository.GetActiveByStudentIdAsync(studentId, ct);
        if (roadmap != null && roadmap.TargetScore > 0)
        {
            targetScore = roadmap.TargetScore;
        }

        // 2. Truy xuất lịch sử làm bài thực tế, ma trận nhiệt BKT và nhật ký chuyên cần
        var allSubmissions = await _submissionRepository.GetByStudentIdAsync(studentId, ct);
        var submissions = allSubmissions
            .Where(s => s.Status == "COMPLETED")
            .OrderByDescending(s => s.CompletedAt)
            .ToList();

        var heatmaps = await _heatmapRepository.GetHeatmapsByStudentIdAsync(studentId, ct);
        var activities = await _activityRepository.GetRecentActivitiesAsync(studentId, 30, ct);

        bool needsOnboarding = submissions.Count == 0 && heatmaps.Count == 0 && activities.Count == 0;

        double targetDomainPct = Math.Clamp(Math.Round((targetScore / 1200.0) * 100.0, 1), 60.0, 95.0);

        if (needsOnboarding)
        {
            var emptyRadar = new List<RadarCompetencyDto>
            {
                new("DOM_LANG", "Sử dụng Ngôn ngữ (Việt - Anh)", 0.0, targetDomainPct),
                new("DOM_MATH", "Toán học, Tư duy Logic & Phân tích số liệu", 0.0, Math.Min(95.0, targetDomainPct + 5.0)),
                new("DOM_NAT_SCI", "Giải quyết vấn đề - Khoa học Tự nhiên", 0.0, targetDomainPct),
                new("DOM_SOC_SCI", "Giải quyết vấn đề - Khoa học Xã hội", 0.0, Math.Max(60.0, targetDomainPct - 2.0))
            };

            return Result<StudentDashboardResponseDto>.Success(new StudentDashboardResponseDto(
                studentId,
                fullName,
                0,
                targetScore,
                "Chưa có dữ liệu",
                emptyRadar,
                new RoadmapProgressDto(0, 0, 0.0, "Hoàn thành khảo sát chẩn đoán 30 câu để mở khóa lộ trình DAG"),
                new StreakAnalyticsDto(0, 0, true, false, Enumerable.Repeat(false, 7).ToList()),
                new List<RecentMockExamDto>(),
                true
            ));
        }

        // 3. Ước lượng điểm số ĐGNL hiện tại (thang 1200) từ bài làm thực tế hoặc ma trận BKT
        var latestSubmission = submissions.FirstOrDefault();
        int estimatedScore;
        int semMargin;

        if (latestSubmission != null)
        {
            estimatedScore = ScaleSubmissionScore(latestSubmission);
            semMargin = latestSubmission.TotalQuestions >= 100 ? 25 : 45;
        }
        else if (heatmaps.Count > 0)
        {
            estimatedScore = Math.Clamp((int)Math.Round(heatmaps.Average(h => h.PltValue) * 1200.0), 0, 1200);
            semMargin = 35;
        }
        else
        {
            estimatedScore = 600;
            semMargin = 45;
        }

        string confidenceInterval = $"{Math.Max(0, estimatedScore - semMargin)} - {Math.Min(1200, estimatedScore + semMargin)}";

        // 4. Biểu đồ Radar 4 miền năng lực ĐGNL thực tế
        double fallbackAccuracyPct = latestSubmission != null && latestSubmission.TotalQuestions > 0
            ? Math.Round((latestSubmission.TotalCorrect / (double)latestSubmission.TotalQuestions) * 100.0, 1)
            : heatmaps.Count > 0
                ? Math.Round(heatmaps.Average(h => h.PltValue) * 100.0, 1)
                : 0.0;

        var radarCompetencies = new List<RadarCompetencyDto>
        {
            CalculateDomainRadar(heatmaps, "DOM_LANG", "Sử dụng Ngôn ngữ (Việt - Anh)", fallbackAccuracyPct, targetDomainPct),
            CalculateDomainRadar(heatmaps, "DOM_MATH", "Toán học, Tư duy Logic & Phân tích số liệu", fallbackAccuracyPct, Math.Min(95.0, targetDomainPct + 5.0)),
            CalculateDomainRadar(heatmaps, "DOM_NAT_SCI", "Giải quyết vấn đề - Khoa học Tự nhiên", fallbackAccuracyPct, targetDomainPct),
            CalculateDomainRadar(heatmaps, "DOM_SOC_SCI", "Giải quyết vấn đề - Khoa học Xã hội", fallbackAccuracyPct, Math.Max(60.0, targetDomainPct - 2.0))
        };

        // 5. Tiến độ lộ trình thích ứng (DAG Progress)
        RoadmapProgressDto roadmapProgress;
        if (roadmap != null && roadmap.TotalMilestones > 0)
        {
            double progressPct = Math.Round((double)roadmap.CompletedMilestones / roadmap.TotalMilestones * 100.0, 1);
            var inProgressNode = roadmap.Nodes.FirstOrDefault(n => n.Status == "IN_PROGRESS");
            string milestoneTitle = inProgressNode != null
                ? $"Chặng {inProgressNode.StepOrder}: Luyện tập chuyên đề thích ứng"
                : roadmap.CompletedMilestones >= roadmap.TotalMilestones
                    ? "Đã hoàn thành toàn bộ lộ trình"
                    : "Sẵn sàng vào học chặng kế tiếp";

            roadmapProgress = new RoadmapProgressDto(
                roadmap.TotalMilestones,
                roadmap.CompletedMilestones,
                progressPct,
                milestoneTitle
            );
        }
        else
        {
            roadmapProgress = new RoadmapProgressDto(
                0,
                0,
                0.0,
                "Bấm vào Dàn Kiến Thức để khởi tạo lộ trình DAG từ kết quả chẩn đoán"
            );
        }

        // 6. Chuỗi chuyên cần thực tế (kết hợp StudentDailyActivities & ngày hoàn thành ExamSubmissions)
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var activeDates = new HashSet<DateTime>(
            activities.Where(a => a.IsStreakCounted).Select(a => a.ActivityDate.Date)
        );
        foreach (var sub in submissions)
        {
            activeDates.Add(sub.CompletedAt.ToUniversalTime().Date);
        }

        bool isTodayCompleted = activeDates.Contains(today);
        var todayAct = activities.FirstOrDefault(a => a.ActivityDate.Date == today);
        bool hasStreakFreeze = !(todayAct?.IsFrozen ?? false);

        int repoStreak = await _activityRepository.CalculateCurrentStreakAsync(studentId, ct);
        int mergedStreak = CalculateConsecutiveStreak(activeDates, today);
        int currentStreak = Math.Max(repoStreak, mergedStreak);
        int longestStreak = Math.Max(currentStreak, CalculateLongestStreak(activeDates));

        var weeklyActivityDays = new List<bool>();
        for (int i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i).Date;
            weeklyActivityDays.Add(activeDates.Contains(day));
        }

        // 7. Lịch sử bài làm thực tế gần nhất (bao gồm cả Khảo sát Chẩn đoán 30 câu & Thi thử 120 câu)
        var recentExams = submissions
            .Take(5)
            .Select(s =>
            {
                bool isFullMock = s.ExamType == "MOCK_EXAM" || s.TotalQuestions >= 100;
                string title = !string.IsNullOrWhiteSpace(s.MentorNotes) && s.ExamType == "DIAGNOSTIC"
                    ? s.MentorNotes!
                    : isFullMock
                        ? $"Đề Thi Thử ĐGNL ĐHQG-HCM Chuẩn Hóa ({s.TotalQuestions} Câu)"
                        : $"Bài Khảo Sát Chẩn Đoán ĐGNL ({s.TotalQuestions} Câu)";

                return new RecentMockExamDto(
                    s.SubmissionId,
                    title,
                    ScaleSubmissionScore(s),
                    s.TotalCorrect,
                    s.TotalQuestions,
                    s.CompletedAt
                );
            })
            .ToList();

        var response = new StudentDashboardResponseDto(
            studentId,
            fullName,
            estimatedScore,
            targetScore,
            confidenceInterval,
            radarCompetencies,
            roadmapProgress,
            new StreakAnalyticsDto(
                currentStreak,
                longestStreak,
                hasStreakFreeze,
                isTodayCompleted,
                weeklyActivityDays
            ),
            recentExams,
            false
        );

        return Result<StudentDashboardResponseDto>.Success(response);
    }

    private static int ScaleSubmissionScore(ExamSubmission submission)
    {
        if (submission.TotalQuestions <= 0)
        {
            return Math.Clamp(submission.TotalScore, 0, 1200);
        }

        if (submission.TotalScore > submission.TotalQuestions)
        {
            return Math.Clamp(submission.TotalScore, 0, 1200);
        }

        return Math.Clamp(
            (int)Math.Round((submission.TotalCorrect / (double)submission.TotalQuestions) * 1200.0),
            0,
            1200);
    }

    private static RadarCompetencyDto CalculateDomainRadar(
        List<ClassSkillHeatmap> heatmaps,
        string domainCode,
        string domainName,
        double fallbackStudentScore,
        double targetScore)
    {
        var domainCells = heatmaps
            .Where(h => string.Equals(h.DomainCode, domainCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        double studentScore = domainCells.Count > 0
            ? Math.Round(domainCells.Average(c => c.PltValue) * 100.0, 1)
            : fallbackStudentScore;

        return new RadarCompetencyDto(
            domainCode,
            domainName,
            studentScore,
            targetScore
        );
    }

    private static int CalculateConsecutiveStreak(HashSet<DateTime> activeDates, DateTime today)
    {
        if (activeDates.Count == 0)
        {
            return 0;
        }

        DateTime cursor = activeDates.Contains(today) ? today : today.AddDays(-1);
        if (!activeDates.Contains(cursor))
        {
            return 0;
        }

        int streak = 0;
        while (activeDates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static int CalculateLongestStreak(HashSet<DateTime> activeDates)
    {
        if (activeDates.Count == 0)
        {
            return 0;
        }

        var ordered = activeDates.OrderBy(d => d).ToList();
        int longest = 1;
        int current = 1;

        for (int i = 1; i < ordered.Count; i++)
        {
            if ((ordered[i] - ordered[i - 1]).Days == 1)
            {
                current++;
                if (current > longest)
                {
                    longest = current;
                }
            }
            else if ((ordered[i] - ordered[i - 1]).Days > 1)
            {
                current = 1;
            }
        }

        return longest;
    }
}
