using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetMyAlerts;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.MarkAlertAsRead;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetAlertsDashboard;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.ResolveAlert;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.ScheduleCounseling;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý hệ thống cảnh báo sớm 3 cấp BR-04 &amp; Can thiệp sư phạm (Core Flow 6)
/// </summary>
[Route("api/practice/early-warnings")]
public class EarlyWarningsController : ApiControllerBase
{
    /// <summary>
    /// Học sinh tra cứu danh sách cảnh báo cá nhân (Mức Vàng trễ hạn, Mức Đỏ nguy cơ, Cảnh báo mục tiêu không tưởng)
    /// </summary>
    /// <remarks>
    /// Trả về toàn bộ danh sách cảnh báo 3 cấp được cá nhân hóa cho học sinh kèm theo các số liệu thống kê tổng hợp (chưa đọc, cảnh báo đỏ, cảnh báo vàng).
    /// </remarks>
    /// <param name="unreadOnly">Lọc chỉ lấy các cảnh báo chưa đọc (mặc định: false)</param>
    /// <param name="alertType">Lọc theo mức độ cảnh báo (ví dụ: 'YELLOW', 'RED', 'UNREALISTIC_GOAL', 'GREEN')</param>
    /// <param name="studentId">Mã định danh học sinh (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Danh sách cảnh báo cá nhân và thống kê số lượng</returns>
    [HttpGet("my-alerts")]
    [ProducesResponseType(typeof(Result<MyAlertsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<MyAlertsResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAlerts(
        [FromQuery] bool? unreadOnly = false,
        [FromQuery] string? alertType = null,
        [FromQuery] Guid? studentId = null)
    {
        Guid effectiveStudentId = Guid.Empty;

        // Ưu tiên trích xuất StudentId từ Gateway Header (X-User-Id)
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveStudentId = parsedId;
        }
        else if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            effectiveStudentId = studentId.Value;
        }

        if (effectiveStudentId == Guid.Empty)
        {
            return HandleResult(Result<MyAlertsResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền header X-User-Id hoặc query param studentId).")));
        }

        var query = new GetMyAlertsQuery(effectiveStudentId, unreadOnly, alertType);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Học sinh đánh dấu đã đọc một thông báo cảnh báo sớm
    /// </summary>
    /// <remarks>
    /// Chuyển cờ is_read của thông báo thành true, hỗ trợ đồng bộ badge chuông thông báo trên giao diện học sinh.
    /// </remarks>
    /// <param name="alertId">Mã định danh cảnh báo</param>
    /// <param name="studentId">Mã định danh học sinh (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Kết quả cập nhật trạng thái đã đọc</returns>
    [HttpPatch("{alertId}/read")]
    [ProducesResponseType(typeof(Result<MarkAlertAsReadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<MarkAlertAsReadResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<MarkAlertAsReadResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result<MarkAlertAsReadResponseDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Result<MarkAlertAsReadResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAlertAsRead(
        [FromRoute] Guid alertId,
        [FromQuery] Guid? studentId = null)
    {
        Guid effectiveStudentId = Guid.Empty;

        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveStudentId = parsedId;
        }
        else if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            effectiveStudentId = studentId.Value;
        }

        if (effectiveStudentId == Guid.Empty)
        {
            return HandleResult(Result<MarkAlertAsReadResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền header X-User-Id hoặc query param studentId).")));
        }

        var command = new MarkAlertAsReadCommand(alertId, effectiveStudentId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Mentor/Giáo viên tra cứu dashboard cảnh báo sớm học sinh thuộc lớp phụ trách (Core Flow 6)
    /// </summary>
    /// <remarks>
    /// Trả về số liệu thống kê cảnh báo tổng thể (cảnh báo đỏ, cảnh báo vàng, mục tiêu không tưởng, số ca đã giải quyết) và danh sách chi tiết học sinh kèm điểm dự báo, thâm hụt điểm để lên phương án can thiệp sư phạm.
    /// </remarks>
    /// <param name="classId">Mã định danh lớp học cần lọc (tùy chọn)</param>
    /// <param name="alertType">Lọc theo cấp độ cảnh báo ('RED', 'YELLOW', 'UNREALISTIC_GOAL')</param>
    /// <param name="isResolved">Lọc theo trạng thái xử lý: false (đang cần can thiệp), true (đã xử lý), null (tất cả)</param>
    /// <param name="mentorId">Mã định danh Mentor (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Dashboard thống kê cảnh báo và danh sách chi tiết</returns>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(Result<EarlyWarningsDashboardResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlertsDashboard(
        [FromQuery] Guid? classId = null,
        [FromQuery] string? alertType = null,
        [FromQuery] bool? isResolved = false,
        [FromQuery] Guid? mentorId = null)
    {
        Guid? effectiveMentorId = mentorId;

        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveMentorId = parsedId;
        }

        var query = new GetAlertsDashboardQuery(classId, effectiveMentorId, alertType, isResolved);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Mentor/Giáo viên ghi chú giải quyết can thiệp sư phạm cho một cảnh báo sớm (Core Flow 6)
    /// </summary>
    /// <remarks>
    /// Cập nhật trạng thái cảnh báo sang đã xử lý (is_resolved = true), ghi nhận mã định danh Mentor (handled_by), thời điểm xử lý (resolved_at) và nội dung giải trình can thiệp sư phạm (resolution_notes).
    /// </remarks>
    /// <param name="alertId">Mã định danh cảnh báo</param>
    /// <param name="request">Payload ghi chú can thiệp và mã Mentor</param>
    /// <returns>Kết quả cập nhật giải quyết cảnh báo</returns>
    [HttpPost("{alertId}/resolve")]
    [ProducesResponseType(typeof(Result<ResolveAlertResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ResolveAlertResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<ResolveAlertResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result<ResolveAlertResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveAlert(
        [FromRoute] Guid alertId,
        [FromBody] ResolveAlertRequestDto request)
    {
        Guid effectiveMentorId = Guid.Empty;

        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveMentorId = parsedId;
        }

        var command = new ResolveAlertCommand(alertId, effectiveMentorId, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Hẹn lịch tư vấn 3 bên định hướng lại mục tiêu khi học sinh gặp cảnh báo UNREALISTIC_GOAL (Core Flow 6)
    /// </summary>
    /// <remarks>
    /// Thiết lập lịch hẹn tư vấn giữa Phụ huynh - Học sinh - Mentor, đề xuất mức điểm mục tiêu khả thi dựa trên phổ điểm ĐGNL thực tế và lưu vết vào hệ thống cảnh báo sớm.
    /// </remarks>
    /// <param name="alertId">Mã định danh cảnh báo</param>
    /// <param name="request">Payload thông tin buổi hẹn (thời gian, địa điểm, điểm mục tiêu đề xuất, ghi chú)</param>
    /// <returns>Kết quả lên lịch hẹn tư vấn</returns>
    [HttpPost("{alertId}/schedule-counseling")]
    [ProducesResponseType(typeof(Result<ScheduleCounselingResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ScheduleCounselingResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<ScheduleCounselingResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result<ScheduleCounselingResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleCounseling(
        [FromRoute] Guid alertId,
        [FromBody] ScheduleCounselingRequestDto request)
    {
        Guid effectiveMentorId = Guid.Empty;

        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveMentorId = parsedId;
        }

        var command = new ScheduleCounselingCommand(alertId, effectiveMentorId, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
