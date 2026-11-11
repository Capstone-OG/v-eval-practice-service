using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Services;

public class ClassHeatmapAggregatorService : IClassHeatmapAggregatorService
{
    private readonly IClassHeatmapRepository _heatmapRepo;
    private readonly IStudentActivityRepository _activityRepo;
    private readonly IClassHeatmapCacheService _cacheService;
    private readonly PracticeDbContext _context;
    private readonly ILogger<ClassHeatmapAggregatorService> _logger;

    public ClassHeatmapAggregatorService(
        IClassHeatmapRepository heatmapRepo,
        IStudentActivityRepository activityRepo,
        IClassHeatmapCacheService cacheService,
        PracticeDbContext context,
        ILogger<ClassHeatmapAggregatorService> logger)
    {
        _heatmapRepo = heatmapRepo;
        _activityRepo = activityRepo;
        _cacheService = cacheService;
        _context = context;
        _logger = logger;
    }

    public async Task AggregateClassHeatmapAsync(Guid classId, CancellationToken ct = default)
    {
        _logger.LogInformation("[Aggregator] Bắt đầu tổng hợp ma trận nhiệt cho Lớp {ClassId}...", classId);

        var enrollments = await _heatmapRepo.GetClassEnrollmentsAsync(classId, ct);
        int totalStudents = enrollments.Count;

        if (totalStudents == 0)
        {
            _logger.LogWarning("[Aggregator] Lớp {ClassId} chưa có học sinh ghi danh, bỏ qua.", classId);
            return;
        }

        var heatmaps = await _heatmapRepo.GetHeatmapsByClassIdAsync(classId, null, ct);
        if (!heatmaps.Any())
        {
            _logger.LogInformation("[Aggregator] Lớp {ClassId} chưa có dữ liệu điểm nhiệt P(Lt).", classId);
            return;
        }

        var enrolledStudentIds = enrollments.Select(e => e.StudentId).ToHashSet();
        var skillGroups = heatmaps
            .Where(h => enrolledStudentIds.Contains(h.StudentId))
            .GroupBy(h => h.SkillId)
            .ToList();

        var summaries = new List<ClassHeatmapSummary>();

        foreach (var group in skillGroups)
        {
            var first = group.First();
            var skillPlts = group.Select(g => g.PltValue).ToList();

            // Nếu số học sinh đã có điểm ít hơn sĩ số lớp, điền mặc định 0.1000 cho các bạn chưa học
            int missingStudents = Math.Max(0, totalStudents - skillPlts.Count);
            for (int i = 0; i < missingStudents; i++)
            {
                skillPlts.Add(0.1000);
            }

            double avgPlt = Math.Round(skillPlts.Average(), 4);
            int strugglingCount = skillPlts.Count(p => p < 0.50);
            double strugglingRate = Math.Round((double)strugglingCount / totalStudents, 4);

            // Tính phương sai và độ lệch chuẩn mẫu sigma
            double variance = skillPlts.Sum(p => Math.Pow(p - avgPlt, 2)) / totalStudents;
            double stdDevPlt = Math.Round(Math.Sqrt(variance), 4);

            // Cờ phân hóa 2 cực: sigma >= 0.28 VÀ struggling_rate >= 0.30
            bool isBimodal = stdDevPlt >= 0.28 && strugglingRate >= 0.30;

            // Phân loại cảnh báo sư phạm 3 cấp độ
            string alertStatus;
            if (strugglingRate >= 0.40 || avgPlt < 0.50)
            {
                alertStatus = "CRITICAL_RED";
            }
            else if (strugglingRate >= 0.20 || avgPlt < 0.70)
            {
                alertStatus = "WARNING_AMBER";
            }
            else
            {
                alertStatus = "SAFE_GREEN";
            }

            summaries.Add(new ClassHeatmapSummary
            {
                ClassId = classId,
                SkillId = group.Key,
                SkillName = first.SkillName,
                DomainCode = first.DomainCode,
                AveragePlt = avgPlt,
                StrugglingCount = strugglingCount,
                TotalStudents = totalStudents,
                StrugglingRate = strugglingRate,
                StdDevPlt = stdDevPlt,
                AlertStatus = alertStatus,
                IsBimodal = isBimodal,
                CalculatedAt = DateTime.UtcNow
            });
        }

        await _heatmapRepo.BatchUpsertSummariesAsync(summaries, ct);
        await _cacheService.InvalidateClassHeatmapAsync(classId, ct);

        _logger.LogInformation("[Aggregator] Đã tổng hợp thành công {Count} kỹ năng cho Lớp {ClassId}.", summaries.Count, classId);
    }

    public async Task AggregateAllActiveClassesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Aggregator Job] Bắt đầu chu kỳ quét định kỳ 15 phút cho toàn bộ các lớp đang hoạt động...");

        var activeClassIds = await _heatmapRepo.GetActiveClassesAsync(ct);
        _logger.LogInformation("[Aggregator Job] Tìm thấy {Count} lớp học ACTIVE cần tổng hợp.", activeClassIds.Count);

        foreach (var classId in activeClassIds)
        {
            try
            {
                await AggregateClassHeatmapAsync(classId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Aggregator Job] Lỗi khi tổng hợp ma trận nhiệt cho Lớp {ClassId}.", classId);
            }
        }

        _logger.LogInformation("[Aggregator Job] Hoàn thành chu kỳ quét định kỳ cho {Count} lớp.", activeClassIds.Count);
    }

    public async Task UpdateCellWriteThroughAsync(
        Guid classId,
        Guid studentId,
        Guid skillId,
        string skillName,
        string domainCode,
        double pltValue,
        string assessmentType,
        CancellationToken ct = default)
    {
        double clampedPlt = Math.Clamp(pltValue, 0.0, 1.0);

        string masteryLevel = clampedPlt switch
        {
            < 0.30 => "CRITICAL",
            < 0.50 => "WEAK",
            < 0.70 => "DEVELOPING",
            _ => "MASTERED"
        };

        var cell = new ClassSkillHeatmap
        {
            ClassId = classId,
            StudentId = studentId,
            SkillId = skillId,
            SkillName = skillName,
            DomainCode = domainCode,
            PltValue = clampedPlt,
            MasteryLevel = masteryLevel,
            LastAssessmentType = assessmentType,
            UpdatedAt = DateTime.UtcNow
        };

        await _heatmapRepo.UpsertHeatmapCellAsync(cell, ct);
        await _cacheService.InvalidateClassHeatmapAsync(classId, ct);

        _logger.LogInformation("[Write-Through] Đã cập nhật ô nhiệt Student {StudentId} Skill {SkillId} P(Lt)={Plt:F2} ({Level}).",
            studentId, skillId, clampedPlt, masteryLevel);

        // Tính toán lại chỉ số sẵn sàng thi cho học sinh
        await ComputeStudentReadinessAsync(studentId, ct);
    }

    public async Task RecordStudentActivityAsync(
        Guid studentId,
        int studyMinutes,
        int practiceQuestionsSolved,
        int videoMinutesWatched,
        int mockExamsTaken,
        CancellationToken ct = default)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var existing = await _activityRepo.GetDailyActivityAsync(studentId, today, ct);

        int totalStudy = (existing?.StudyTimeMinutes ?? 0) + studyMinutes;
        int totalQuestions = (existing?.PracticeQuestionsSolved ?? 0) + practiceQuestionsSolved;
        int totalVideo = (existing?.VideoMinutesWatched ?? 0) + videoMinutesWatched;
        int totalMock = (existing?.MockExamsTaken ?? 0) + mockExamsTaken;

        // Chuẩn Active Study Day: Làm >= 1 câu hỏi APPLY HOẶC xem >= 15 phút video LEARN HOẶC làm >= 1 bài thi thử
        bool isStreakCounted = totalQuestions >= 1 || totalVideo >= 15 || totalMock >= 1;

        var activity = new StudentDailyActivity
        {
            StudentId = studentId,
            ActivityDate = today,
            StudyTimeMinutes = totalStudy,
            PracticeQuestionsSolved = totalQuestions,
            VideoMinutesWatched = totalVideo,
            MockExamsTaken = totalMock,
            IsStreakCounted = isStreakCounted,
            IsFrozen = existing?.IsFrozen ?? false,
            UpdatedAt = DateTime.UtcNow
        };

        await _activityRepo.UpsertDailyActivityAsync(activity, ct);
        await ComputeStudentReadinessAsync(studentId, ct);
    }

    public async Task ComputeStudentReadinessAsync(Guid studentId, CancellationToken ct = default)
    {
        // 1. Tính chuỗi ngày chuyên cần hiện tại
        int streak = await _activityRepo.CalculateCurrentStreakAsync(studentId, ct);

        // 2. Tính số giờ tự học trong 7 ngày gần nhất
        double studyHoursWeek = await _activityRepo.CalculateStudyHoursInPastDaysAsync(studentId, 7, ct);

        // 3. Tính điểm kỷ luật: Consistency Score = min(100, (StudyHoursWeek / 10 * 60 + min(Streak, 14) / 14 * 40))
        double studyHoursScore = Math.Min(studyHoursWeek / 10.0, 1.5) * 60.0;
        double streakScore = (Math.Min(streak, 14) / 14.0) * 40.0;
        double consistencyScore = Math.Min(100.0, Math.Round(studyHoursScore + streakScore, 1));

        // 4. Lấy điểm năng lực IRT Theta (ưu tiên từ bài thi thử Mock Exam gần nhất, nếu không lấy từ Diagnostic)
        var latestSubmission = await _context.ExamSubmissions
            .AsNoTracking()
            .Where(s => s.StudentId == studentId && s.Status == "COMPLETED" && s.Theta0 != null)
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);

        double theta = latestSubmission?.Theta0 ?? 0.0;
        // Chuẩn hóa Theta từ [-3.0, +3.0] sang thang [0, 100]
        double normalizedTheta = Math.Clamp(((theta - (-3.0)) / (3.0 - (-3.0))) * 100.0, 0.0, 100.0);

        // 5. Tính độ phủ thành thạo kiến thức BKT từ bảng ClassSkillHeatmaps
        var studentHeatmaps = await _context.ClassSkillHeatmaps
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .ToListAsync(ct);

        double skillCoverage = studentHeatmaps.Any()
            ? studentHeatmaps.Average(h => h.PltValue) * 100.0
            : 10.0;

        // 6. Tính Exam Readiness Index tổng hợp: R_exam = 0.50*Theta + 0.35*BKT + 0.15*Consistency
        double examReadiness = Math.Round(
            (0.50 * normalizedTheta) +
            (0.35 * skillCoverage) +
            (0.15 * consistencyScore), 1);

        string tier = examReadiness switch
        {
            < 50.0 => "NOT_READY",
            < 65.0 => "PREPARING",
            < 75.0 => "ACCELERATING",
            _ => "READY"
        };

        var metric = new StudentReadinessMetric
        {
            StudentId = studentId,
            NormalizedTheta = Math.Round(normalizedTheta, 1),
            SkillCoveragePlt = Math.Round(skillCoverage, 1),
            ConsistencyScore = consistencyScore,
            ExamReadinessIndex = examReadiness,
            ReadinessTier = tier,
            StudyHoursThisWeek = studyHoursWeek,
            CurrentStreakDays = streak,
            CalculatedAt = DateTime.UtcNow
        };

        await _activityRepo.UpsertReadinessMetricAsync(metric, ct);
        _logger.LogInformation("[Readiness] Đã tính toán lại cho Student {StudentId}: R_exam={Index}% ({Tier}), Streak={Streak} ngày, Giờ học={Hours}h.",
            studentId, examReadiness, tier, streak, studyHoursWeek);
    }
}
