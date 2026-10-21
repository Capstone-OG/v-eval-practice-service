using System;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Services;

/// <summary>
/// Động cơ tổng hợp số liệu ma trận nhiệt và tiến độ học tập đa chiều (Hybrid Aggregation Engine - Core Flow 5)
/// </summary>
public interface IClassHeatmapAggregatorService
{
    /// <summary>
    /// Tổng hợp và tính toán các chỉ số thống kê (Mean, StdDev, StrugglingRate, Bimodal Flag) cho 1 lớp học
    /// </summary>
    Task AggregateClassHeatmapAsync(Guid classId, CancellationToken ct = default);

    /// <summary>
    /// Background job quét toàn bộ các lớp đang hoạt động để làm tươi số liệu thống kê định kỳ
    /// </summary>
    Task AggregateAllActiveClassesAsync(CancellationToken ct = default);

    /// <summary>
    /// Cập nhật real-time Write-Through O(1) ngay khi học sinh hoàn thành bài làm mà không cần đợi Job 15 phút
    /// </summary>
    Task UpdateCellWriteThroughAsync(
        Guid classId,
        Guid studentId,
        Guid skillId,
        string skillName,
        string domainCode,
        double pltValue,
        string assessmentType,
        CancellationToken ct = default);

    /// <summary>
    /// Ghi nhận hoạt động học tập hằng ngày và cập nhật chuỗi chuyên cần (Streak) theo chuẩn Active Study Day
    /// </summary>
    Task RecordStudentActivityAsync(
        Guid studentId,
        int studyMinutes,
        int practiceQuestionsSolved,
        int videoMinutesWatched,
        int mockExamsTaken,
        CancellationToken ct = default);

    /// <summary>
    /// Tính toán và cập nhật Chỉ số sẵn sàng thi (Exam Readiness Index R_exam) cho phụ huynh & học sinh
    /// </summary>
    Task ComputeStudentReadinessAsync(Guid studentId, CancellationToken ct = default);
}
