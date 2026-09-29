using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.TrackVideo;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMyRoadmap;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetRoadmapNodeDetail;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý quy hoạch lộ trình học tập cá nhân hóa và máy trạng thái thích ứng (Core Flow 2)
/// </summary>
[Route("api/v1/practice/roadmaps")]
public class RoadmapsController : ApiControllerBase
{
    /// <summary>
    /// Core Flow 2 - API 1: Khởi tạo lộ trình học tập thích ứng cá nhân hóa từ bài thi chẩn đoán
    /// </summary>
    /// <remarks>
    /// Quy trình tuần tự 7 bước:
    /// 1. Trích xuất hồ sơ năng lực đầu vào (theta_0, P(L0), WeakSkills, EnrolledClassId).
    /// 2. Nạp Cây khung năng lực 12 kỹ năng chuẩn và cung tiên quyết từ Content Service qua gRPC.
    /// 3. Chạy thuật toán Tarjan Cycle Detector phát hiện và ngăn chặn chu trình lặp kín.
    /// 4. Chạy thuật toán Path Pruning Engine cắt tỉa 3 tầng thông minh theo quỹ thời gian khả dụng.
    /// 5. Chạy thuật toán Topological Sorter với Priority Queue ưu tiên sư phạm.
    /// 6. Chạy Milestone Binder đóng gói 3 tài nguyên (Video + Quiz + Live) và khởi tạo State Machine.
    /// 7. Mở Transaction lưu trữ nguyên tử vào CSDL và trả về Roadmap Timeline.
    /// </remarks>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateRoadmapResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateRoadmap([FromBody] GenerateRoadmapRequestDto request)
    {
        Guid studentId = Guid.Empty;

        // Ưu tiên trích xuất StudentId từ Gateway Header (X-User-Id)
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            studentId = parsedId;
        }
        else if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty)
        {
            studentId = request.StudentId.Value;
        }

        if (studentId == Guid.Empty)
        {
            return HandleResult(Result<GenerateRoadmapResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (X-User-Id header hoặc studentId payload).")));
        }

        var command = new GenerateRoadmapCommand(
            StudentId: studentId,
            DiagnosticSubmissionId: request.DiagnosticSubmissionId,
            ExamDate: request.ExamDate,
            StudyHoursPerDay: request.StudyHoursPerDay
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 2: Tra cứu lộ trình học tập thích ứng cá nhân hóa (Timeline) đang kích hoạt của học sinh
    /// </summary>
    /// <remarks>
    /// - Hỗ trợ trích xuất StudentId tự động từ Gateway Header (X-User-Id) hoặc query parameter studentId.
    /// - Trả về dòng thời gian toàn bộ các chặng học (RoadmapTimelineDto) kèm thống kê tiến độ phần trăm hoàn thành.
    /// - Dữ liệu được gom nhóm theo từng Miền năng lực / Môn học (stages) và danh sách tuần tự toàn bài (nodes).
    /// </remarks>
    [HttpGet("my-roadmap")]
    [ProducesResponseType(typeof(RoadmapTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyRoadmap([FromQuery] Guid? studentId)
    {
        Guid targetStudentId = Guid.Empty;

        // Ưu tiên trích xuất StudentId từ Gateway Header (X-User-Id)
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            targetStudentId = parsedId;
        }
        else if (studentId.HasValue && studentId.Value != Guid.Empty)
        {
            targetStudentId = studentId.Value;
        }

        if (targetStudentId == Guid.Empty)
        {
            return HandleResult(Result<RoadmapTimelineDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (X-User-Id header hoặc query parameter studentId).")));
        }

        var result = await Mediator.Send(new GetMyRoadmapQuery(targetStudentId));
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 3: Lấy thông tin chi tiết một chặng học (Milestone / RoadmapNode)
    /// </summary>
    /// <remarks>
    /// - Trả về đầy đủ 3 thành phần của chặng: Video bài giảng (MaterialId), Bài Quiz củng cố (QuizExamId), và Buổi học Live Q&amp;A cơ sở (LiveSession).
    /// - Cung cấp trạng thái mở khóa (LOCKED, IN_PROGRESS, COMPLETED, SKIPPED_PRUNED) và lịch sử điểm danh buổi Live.
    /// - Kiểm tra quyền sở hữu bảo mật: ngăn chặn học sinh truy cập trái phép chặng học của học sinh khác.
    /// </remarks>
    [HttpGet("nodes/{nodeId:guid}")]
    [ProducesResponseType(typeof(RoadmapNodeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodeDetail(Guid nodeId)
    {
        Guid? studentId = null;

        // Bóc tách StudentId từ Gateway Header (X-User-Id) nếu có để kiểm tra phân quyền
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            studentId = parsedId;
        }

        var result = await Mediator.Send(new GetRoadmapNodeDetailQuery(nodeId, studentId));
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 2 - API 4: Ghi nhận thời gian xem video bài giảng lý thuyết của chặng học
    /// </summary>
    /// <remarks>
    /// - Nhận thông tin: `WatchedDurationSeconds` (thời gian đã xem) và `TotalDurationSeconds` (tổng thời lượng video).
    /// - Tính toán tỷ lệ phần trăm xem bài giảng (`WatchPercentage`).
    /// - Kiểm tra điều kiện tiên quyết: Yêu cầu học sinh xem đạt tối thiểu 80% thời lượng để được mở quyền làm bài Quiz củng cố (`IsQuizEligible = true`).
    /// - Hỗ trợ bóc tách StudentId từ Gateway Header (`X-User-Id`) để xác thực bảo mật quyền sở hữu.
    /// </remarks>
    [HttpPost("nodes/{nodeId:guid}/track-video")]
    [ProducesResponseType(typeof(TrackVideoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrackVideo(Guid nodeId, [FromBody] TrackVideoRequestDto request)
    {
        Guid? studentId = null;

        // Bóc tách StudentId từ Gateway Header (X-User-Id) nếu có để kiểm tra phân quyền
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            studentId = parsedId;
        }

        var command = new TrackVideoCommand(
            NodeId: nodeId,
            WatchedDurationSeconds: request.WatchedDurationSeconds,
            TotalDurationSeconds: request.TotalDurationSeconds,
            StudentId: studentId
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
