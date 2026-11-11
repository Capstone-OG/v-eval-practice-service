using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class StudentActivityRepository : IStudentActivityRepository
{
    private readonly PracticeDbContext _context;

    public StudentActivityRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDailyActivity?> GetDailyActivityAsync(Guid studentId, DateTime activityDate, CancellationToken ct = default)
    {
        var targetDate = DateTime.SpecifyKind(activityDate.Date, DateTimeKind.Utc);
        return await _context.StudentDailyActivities
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.ActivityDate == targetDate, ct);
    }

    public async Task UpsertDailyActivityAsync(StudentDailyActivity activity, CancellationToken ct = default)
    {
        var targetDate = DateTime.SpecifyKind(activity.ActivityDate.Date, DateTimeKind.Utc);
        var existing = await _context.StudentDailyActivities
            .FirstOrDefaultAsync(a => a.StudentId == activity.StudentId && a.ActivityDate == targetDate, ct);

        if (existing == null)
        {
            activity.ActivityDate = targetDate;
            await _context.StudentDailyActivities.AddAsync(activity, ct);
        }
        else
        {
            existing.StudyTimeMinutes = activity.StudyTimeMinutes;
            existing.PracticeQuestionsSolved = activity.PracticeQuestionsSolved;
            existing.VideoMinutesWatched = activity.VideoMinutesWatched;
            existing.MockExamsTaken = activity.MockExamsTaken;
            existing.IsStreakCounted = activity.IsStreakCounted;
            existing.IsFrozen = activity.IsFrozen;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<StudentDailyActivity>> GetRecentActivitiesAsync(Guid studentId, int days = 30, CancellationToken ct = default)
    {
        var fromDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-days), DateTimeKind.Utc);
        return await _context.StudentDailyActivities
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.ActivityDate >= fromDate)
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync(ct);
    }

    public async Task<int> CalculateCurrentStreakAsync(Guid studentId, CancellationToken ct = default)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var thirtyDaysAgo = today.AddDays(-60);

        var activities = await _context.StudentDailyActivities
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.ActivityDate >= thirtyDaysAgo)
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync(ct);

        if (!activities.Any())
        {
            return 0;
        }

        var activityMap = activities.ToDictionary(a => a.ActivityDate.Date, a => a);
        int streak = 0;

        // Bắt đầu kiểm tra từ hôm nay; nếu hôm nay chưa làm, bắt đầu từ hôm qua
        var checkDate = today.Date;
        if (!activityMap.TryGetValue(checkDate, out var todayAct) || (!todayAct.IsStreakCounted && !todayAct.IsFrozen))
        {
            checkDate = checkDate.AddDays(-1);
        }

        while (activityMap.TryGetValue(checkDate, out var act))
        {
            if (act.IsStreakCounted || act.IsFrozen)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    public async Task<double> CalculateStudyHoursInPastDaysAsync(Guid studentId, int days = 7, CancellationToken ct = default)
    {
        var fromDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-days), DateTimeKind.Utc);
        var totalMinutes = await _context.StudentDailyActivities
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.ActivityDate >= fromDate)
            .SumAsync(a => a.StudyTimeMinutes, ct);

        return Math.Round(totalMinutes / 60.0, 1);
    }

    public async Task<StudentReadinessMetric?> GetReadinessMetricAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.StudentReadinessMetrics
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.StudentId == studentId, ct);
    }

    public async Task UpsertReadinessMetricAsync(StudentReadinessMetric metric, CancellationToken ct = default)
    {
        var existing = await _context.StudentReadinessMetrics
            .FirstOrDefaultAsync(m => m.StudentId == metric.StudentId, ct);

        if (existing == null)
        {
            await _context.StudentReadinessMetrics.AddAsync(metric, ct);
        }
        else
        {
            existing.NormalizedTheta = metric.NormalizedTheta;
            existing.SkillCoveragePlt = metric.SkillCoveragePlt;
            existing.ConsistencyScore = metric.ConsistencyScore;
            existing.ExamReadinessIndex = metric.ExamReadinessIndex;
            existing.ReadinessTier = metric.ReadinessTier;
            existing.StudyHoursThisWeek = metric.StudyHoursThisWeek;
            existing.CurrentStreakDays = metric.CurrentStreakDays;
            existing.CalculatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }
}
