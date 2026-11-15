using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Schedules.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý Khung lịch tuần (ClassSchedules), Buổi học thực tế (ClassSessions), Điểm danh chuyên cần (SessionAttendances),
/// Thời khóa biểu Sinh viên và Lịch lên lớp Giảng viên (Theo Học kỳ và Theo Khóa học)
/// </summary>
[Route("api/practice/schedules")]
[Route("api/v1/schedules")]
public class SchedulesController : ApiControllerBase
{
    private readonly IScheduleService _scheduleService;

    public SchedulesController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    // =========================================================================
    // 1. KHUNG LỊCH ĐỊNH KỲ HẰNG TUẦN CỦA LỚP (CLASS SCHEDULES)
    // =========================================================================

    /// <summary>
    /// Thiết lập hoặc cập nhật Khung lịch định kỳ hằng tuần (ClassSchedules) cho một lớp học
    /// </summary>
    [HttpPut("classes/{classId:guid}/weekly-schedules")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassScheduleSlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveClassSchedules(
        Guid classId,
        [FromBody] SaveClassSchedulesRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.SaveClassSchedulesAsync(classId, request, ct);
        return HandleResult(result);
    }

    // =========================================================================
    // 2. BUỔI HỌC THỰC TẾ, TỰ ĐỘNG SINH LỊCH, DẠY THAY & HỌC BÙ (CLASS SESSIONS)
    // =========================================================================

    /// <summary>
    /// Tự động sinh hàng loạt buổi học cụ thể (ClassSessions) từ Khung lịch tuần (ClassSchedules) của lớp
    /// </summary>
    /// <remarks>
    /// - Tự động đối chiếu xung đột phòng học (Room Conflict) và xung đột lịch dạy của Giảng viên (Teacher Conflict).
    /// - Tự động gắn kỹ năng chuyên đề từ cây tri thức vào từng buổi học.
    /// </remarks>
    [HttpPost("classes/{classId:guid}/sessions/generate")]
    [ProducesResponseType(typeof(GenerateClassSessionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateClassSessions(
        Guid classId,
        [FromBody] GenerateClassSessionsRequestDto? request,
        CancellationToken ct = default)
    {
        var req = request ?? new GenerateClassSessionsRequestDto();
        var result = await _scheduleService.GenerateClassSessionsAsync(classId, req, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các buổi học (ClassSessions) của một lớp học theo khoảng ngày
    /// </summary>
    [HttpGet("classes/{classId:guid}/sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSessionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassSessions(
        Guid classId,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetClassSessionsAsync(classId, fromDate, toDate, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Cập nhật thông tin một buổi học cụ thể (Đổi phòng, Đổi giờ, Phân công Giảng viên dạy thay, Gắn kỹ năng bài giảng)
    /// </summary>
    [HttpPut("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(ClassSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateClassSession(
        Guid sessionId,
        [FromBody] UpdateClassSessionRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.UpdateClassSessionAsync(sessionId, request, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Báo hoãn buổi học và tự động sinh buổi học bù (Makeup Session) liên kết qua RescheduledFromId
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/reschedule")]
    [ProducesResponseType(typeof(ClassSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleClassSession(
        Guid sessionId,
        [FromBody] RescheduleClassSessionRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.RescheduleClassSessionAsync(sessionId, request, ct);
        return HandleResult(result);
    }

    // =========================================================================
    // 3. ĐIỂM DANH CHUYÊN CẦN BUỔI HỌC (SESSION ATTENDANCES)
    // =========================================================================

    /// <summary>
    /// Lấy danh sách điểm danh của một buổi học cụ thể
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/attendances")]
    [ProducesResponseType(typeof(SessionAttendanceSummaryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionAttendances(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetSessionAttendancesAsync(sessionId, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Ghi nhận điểm danh chuyên cần cho buổi học (PRESENT, ABSENT, LATE, EXCUSED)
    /// </summary>
    /// <remarks>
    /// - Tự động đồng bộ thời lượng học tập vào bảng StudentDailyActivities (duy trì chuỗi Streak cho học sinh đi học).
    /// - Tự động phát hiện học sinh vắng nhiều buổi và kích hoạt Cảnh báo sớm (SystemAlerts - BR-04) cho Mentor và Phụ huynh.
    /// </remarks>
    [HttpPost("sessions/{sessionId:guid}/attendances")]
    [ProducesResponseType(typeof(SessionAttendanceSummaryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitSessionAttendances(
        Guid sessionId,
        [FromBody] SubmitSessionAttendanceRequestDto request,
        CancellationToken ct = default)
    {
        var recordedBy = ResolveCurrentUserId();
        var result = await _scheduleService.SubmitSessionAttendancesAsync(sessionId, request, recordedBy, ct);
        return HandleResult(result);
    }

    // =========================================================================
    // 4. THỜI KHÓA BIỂU SINH VIÊN & LỊCH LÊN LỚP GIẢNG VIÊN (THEO KỲ / THEO KHÓA)
    // =========================================================================

    /// <summary>
    /// Lấy Thời khóa biểu học tập của Sinh viên (Lọc theo Học kỳ, Khóa học, Khoảng ngày/tuần)
    /// </summary>
    /// <remarks>
    /// - Bao gồm danh sách các Kỳ học hiện có, tiến độ chuyên cần từng Lớp/Khóa học, và chi tiết từng buổi học.
    /// - Hiển thị trạng thái điểm danh (PRESENT/ABSENT/LATE/EXCUSED), lịch học bù, giáo viên dạy thay và nhóm học tập vi mô của sinh viên.
    /// </remarks>
    [HttpGet("students/{studentId:guid}/timetable")]
    [ProducesResponseType(typeof(StudentTimetableResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentTimetable(
        Guid studentId,
        [FromQuery] Guid? termId = null,
        [FromQuery] Guid? courseId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetStudentTimetableAsync(studentId, termId, courseId, fromDate, toDate, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy Thời khóa biểu của chính Sinh viên đang đăng nhập (dựa trên JWT / Header X-User-Id)
    /// </summary>
    [HttpGet("students/me/timetable")]
    [ProducesResponseType(typeof(StudentTimetableResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyStudentTimetable(
        [FromQuery] Guid? termId = null,
        [FromQuery] Guid? courseId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var currentStudentId = ResolveCurrentUserId();
        if (!currentStudentId.HasValue || currentStudentId.Value == Guid.Empty)
        {
            return HandleResult(Result.Failure<StudentTimetableResponseDto>(
                Error.Unauthorized("Schedule.Unauthorized", "Không xác định được định danh học sinh từ phiên đăng nhập.")));
        }

        var result = await _scheduleService.GetStudentTimetableAsync(
            currentStudentId.Value, termId, courseId, fromDate, toDate, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy Lịch lên lớp và Giảng dạy của Giảng viên (Lọc theo Học kỳ, Khóa học, Khoảng ngày/tuần)
    /// </summary>
    /// <remarks>
    /// - Tự động tổng hợp cả các buổi dạy chính (PRIMARY), các buổi dạy thay cho đồng nghiệp (SUBSTITUTE_IN) và các buổi có đồng nghiệp dạy thay (SUBSTITUTE_OUT).
    /// - Kèm sĩ số lớp, thống kê điểm danh từng buổi và số lượng nhóm vi mô trong lớp.
    /// </remarks>
    [HttpGet("teachers/{teacherId:guid}/schedule")]
    [ProducesResponseType(typeof(TeacherScheduleResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherSchedule(
        Guid teacherId,
        [FromQuery] Guid? termId = null,
        [FromQuery] Guid? courseId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetTeacherScheduleAsync(teacherId, termId, courseId, fromDate, toDate, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy Lịch lên lớp của chính Giảng viên đang đăng nhập (dựa trên JWT / Header X-User-Id)
    /// </summary>
    [HttpGet("teachers/me/schedule")]
    [ProducesResponseType(typeof(TeacherScheduleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTeacherSchedule(
        [FromQuery] Guid? termId = null,
        [FromQuery] Guid? courseId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var currentTeacherId = ResolveCurrentUserId();
        if (!currentTeacherId.HasValue || currentTeacherId.Value == Guid.Empty)
        {
            return HandleResult(Result.Failure<TeacherScheduleResponseDto>(
                Error.Unauthorized("Schedule.Unauthorized", "Không xác định được định danh giảng viên từ phiên đăng nhập.")));
        }

        var result = await _scheduleService.GetTeacherScheduleAsync(
            currentTeacherId.Value, termId, courseId, fromDate, toDate, ct);
        return HandleResult(result);
    }

    private Guid? ResolveCurrentUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) &&
            Guid.TryParse(userIdHeader, out var headerGuid))
        {
            return headerGuid;
        }

        var claimVal = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(claimVal) && Guid.TryParse(claimVal, out var claimGuid))
        {
            return claimGuid;
        }

        return null;
    }
}
