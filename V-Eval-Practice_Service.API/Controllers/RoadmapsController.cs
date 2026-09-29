using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

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
}
