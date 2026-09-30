using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CreateLiveSession;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.JoinLiveSession;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.TeacherAttendance;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.UpdateRecording;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetMyLiveSchedule;
using V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetTeacherSchedule;

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
    /// Core Flow 2 - API 11: Tham gia buổi học trực tuyến Live Q&amp;A và ghi nhận dấu vết vào lớp
    /// </summary>
    /// <remarks>
    /// - Dành cho Học sinh (Student).
    /// - Trả về thông tin buổi học và link phòng họp trực tuyến (`MeetingUrl`).
    /// - Ghi nhận thời gian `JoinedAt`. Bảo lưu thẩm quyền điểm danh chuyên cần cho Giáo viên ở API 12.
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

    /// <summary>
    /// Core Flow 2 - API 12: Giáo viên thực hiện điểm danh chuyên cần cho học sinh trong buổi học Live Q&amp;A
    /// </summary>
    /// <remarks>
    /// - Dành cho Giáo viên (Teacher).
    /// - Ghi nhận trạng thái điểm danh chính thức (ATTENDED hoặc ABSENT) cho từng học sinh.
    /// </remarks>
    [HttpPost("{sessionId:guid}/attendance")]
    [ProducesResponseType(typeof(TeacherAttendanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitTeacherAttendance(Guid sessionId, [FromBody] TeacherAttendanceRequestDto request)
    {
        Guid? teacherId = null;
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            teacherId = parsedId;
        }

        var command = new TeacherAttendanceCommand(sessionId, request.Items, teacherId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 13: Lấy thời khóa biểu giảng dạy và danh sách buổi Live Q&amp;A được phân công của giáo viên
    /// </summary>
    /// <remarks>
    /// - Dành cho Giáo viên (Teacher).
    /// - Lấy teacherId từ query param hoặc Header 'X-User-Id'.
    /// - Trả về danh sách toàn bộ các buổi học Live Q&amp;A do giáo viên phụ trách kèm thông tin lớp và số liệu chuyên cần.
    /// </remarks>
    [HttpGet("teacher-schedule")]
    [ProducesResponseType(typeof(TeacherScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTeacherSchedule([FromQuery] Guid? teacherId)
    {
        Guid effectiveTeacherId = teacherId ?? Guid.Empty;

        if (effectiveTeacherId == Guid.Empty &&
            Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) &&
            Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveTeacherId = parsedId;
        }

        if (effectiveTeacherId == Guid.Empty)
        {
            return BadRequest(new { Message = "TeacherId không được để trống. Vui lòng truyền qua query param hoặc Header 'X-User-Id'." });
        }

        var query = new GetTeacherScheduleQuery(effectiveTeacherId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 14: Giáo viên cập nhật link video ghi hình buổi học trực tuyến Live Q&amp;A
    /// </summary>
    /// <remarks>
    /// - Dành cho Giáo viên (Teacher).
    /// - Cập nhật link video ghi hình (`RecordingUrl`) và đánh dấu `IsRecorded = true` để học sinh xem lại bài giảng.
    /// </remarks>
    [HttpPut("{sessionId:guid}/recording")]
    [ProducesResponseType(typeof(UpdateLiveSessionRecordingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRecording(Guid sessionId, [FromBody] UpdateLiveSessionRecordingRequestDto request)
    {
        Guid? teacherId = null;
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            teacherId = parsedId;
        }

        var command = new UpdateLiveSessionRecordingCommand(sessionId, request.RecordingUrl, teacherId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
