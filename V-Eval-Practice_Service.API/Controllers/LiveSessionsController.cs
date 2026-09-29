using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CreateLiveSession;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.JoinLiveSession;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetMyLiveSchedule;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý các buổi học trực tuyến Live Q&amp;A, lịch học và điểm danh cơ sở (Core Flow 2 - Phase 3)
/// </summary>
[Route("api/v1/practice/live-sessions")]
public class LiveSessionsController : ApiControllerBase
{
    /// <summary>
    /// Core Flow 2 - API 8: Tạo lịch buổi học Live Q&amp;A giải đáp thắc mắc cho lớp học cơ sở
    /// </summary>
    /// <remarks>
    /// - Dành cho Quản trị viên chuyên môn / Trưởng bộ phận đào tạo cơ sở (Academic Manager).
    /// - Nhận thông tin lớp học (`ClassId`), giáo viên phụ trách (`TeacherId`), tiêu đề (`Title`), thời gian (`ScheduledAt`), thời lượng (`DurationMinutes`) và link phòng họp (`MeetingUrl`).
    /// - Tự động liên kết giáo viên của lớp học nếu không truyền `TeacherId`.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(LiveSessionSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLiveSession([FromBody] CreateLiveSessionRequestDto request)
    {
        Guid? createdBy = null;
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            createdBy = parsedId;
        }

        var command = new CreateLiveSessionCommand(
            ClassId: request.ClassId,
            TeacherId: request.TeacherId,
            Title: request.Title,
            Description: request.Description,
            ScheduledAt: request.ScheduledAt,
            DurationMinutes: request.DurationMinutes,
            MeetingUrl: request.MeetingUrl,
            CreatedBy: createdBy
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 10: Lấy thời khóa biểu các buổi Live Q&amp;A của lớp học cơ sở học sinh ghi danh
    /// </summary>
    /// <remarks>
    /// - Dành cho Học sinh (Student).
    /// - Lấy StudentId từ query param `studentId` hoặc Header `X-User-Id`.
    /// - Trả về danh sách toàn bộ các buổi học Live Q&amp;A của lớp, trạng thái điểm danh cá nhân (`ATTENDED`, `ABSENT`, `NOT_ATTENDED`), link phòng họp và link video ghi hình.
    /// </remarks>
    [HttpGet("my-schedule")]
    [ProducesResponseType(typeof(MyLiveScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyLiveSchedule([FromQuery] Guid? studentId)
    {
        Guid effectiveStudentId = studentId ?? Guid.Empty;

        if (effectiveStudentId == Guid.Empty &&
            Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) &&
            Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveStudentId = parsedId;
        }

        if (effectiveStudentId == Guid.Empty)
        {
            return BadRequest(new { Message = "StudentId không được để trống. Vui lòng truyền qua query param hoặc Header 'X-User-Id'." });
        }

        var query = new GetMyLiveScheduleQuery(effectiveStudentId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 11: Tham gia buổi học trực tuyến Live Q&amp;A và tự động ghi nhận điểm danh
    /// </summary>
    /// <remarks>
    /// - Dành cho Học sinh (Student).
    /// - Trả về thông tin buổi học và link phòng họp trực tuyến (`MeetingUrl`).
    /// - Tự động tạo hoặc cập nhật bản ghi điểm danh `LiveSessionAttendance` sang trạng thái `ATTENDED` kèm thời gian `JoinedAt`.
    /// </remarks>
    [HttpPost("{sessionId:guid}/join")]
    [ProducesResponseType(typeof(JoinLiveSessionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinLiveSession(Guid sessionId, [FromBody] JoinLiveSessionRequestDto? request)
    {
        Guid effectiveStudentId = request?.StudentId ?? Guid.Empty;

        if (effectiveStudentId == Guid.Empty &&
            Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) &&
            Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveStudentId = parsedId;
        }

        if (effectiveStudentId == Guid.Empty)
        {
            return BadRequest(new { Message = "StudentId không được để trống. Vui lòng truyền qua request body hoặc Header 'X-User-Id'." });
        }

        var command = new JoinLiveSessionCommand(sessionId, effectiveStudentId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
