using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMyRoadmap;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetRoadmapNodeDetail;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý quy hoạch lộ trình học tập thích ứng cá nhân hóa (Core Flow 2)
/// </summary>
[Route("api/practice/roadmaps")]
public class RoadmapsController : ApiControllerBase
{
    /// <summary>
    /// Khởi tạo lộ trình học tập cá nhân hóa từ bài thi chẩn đoán
    /// </summary>
    /// <remarks>
    /// Thuật toán tích hợp:
    /// 1. Trích xuất năng lực đầu vào từ bài thi chẩn đoán (theta_0, P(L0), WeakSkills).
    /// 2. Nạp cây khung kỹ năng chuẩn và cung tiên quyết qua gRPC.
    /// 3. Chạy thuật toán Tarjan Cycle Detector phát hiện và khử chu trình lặp kín.
    /// 4. Chạy thuật toán Path Pruning cắt tỉa thông minh theo quỹ thời gian học tập.
    /// 5. Chạy thuật toán Topological Sort Kahn xếp lịch tuần tự theo độ ưu tiên sư phạm.
    /// 6. Đóng gói chặng học và gán lịch lớp chuyên đề K-Means tương ứng.
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
    /// Tra cứu dòng thời gian lộ trình học tập cá nhân (Timeline)
    /// </summary>
    /// <remarks>
    /// - Trả về dòng thời gian toàn bộ các chặng học của học sinh kèm tiến độ phần trăm hoàn thành.
    /// - Gom nhóm chặng học theo từng Miền năng lực / Môn học (stages) và danh sách tuần tự toàn lộ trình (nodes).
    /// </remarks>
    [HttpGet("my-roadmap")]
    [ProducesResponseType(typeof(RoadmapTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyRoadmap([FromQuery] Guid? studentId)
    {
        Guid targetStudentId = Guid.Empty;

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
    /// Xem thông tin chi tiết một chặng học trên lộ trình
    /// </summary>
    /// <remarks>
    /// - Trả về thông tin chi tiết chặng: Kỹ năng mục tiêu, học liệu video, bài quiz củng cố và lịch Live Q&amp;A chuyên đề.
    /// - Cung cấp trạng thái mở khóa của chặng (LOCKED, IN_PROGRESS, COMPLETED, SKIPPED_PRUNED).
    /// </remarks>
    [HttpGet("nodes/{nodeId:guid}")]
    [ProducesResponseType(typeof(RoadmapNodeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodeDetail(Guid nodeId)
    {
        Guid? studentId = null;

        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            studentId = parsedId;
        }

        var result = await Mediator.Send(new GetRoadmapNodeDetailQuery(nodeId, studentId));
        return HandleResult(result);
    }
}
