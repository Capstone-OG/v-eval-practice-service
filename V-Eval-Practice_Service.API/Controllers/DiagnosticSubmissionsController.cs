using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Commands.SubmitDiagnostic;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionById;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionsByStudent;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý nộp bài khảo sát chẩn đoán năng lực đầu vào và chấm điểm tự động (Core Flow 1 - Bước 3)
/// </summary>
[Route("api/v1/practice/diagnostic-submissions")]
public class DiagnosticSubmissionsController : ApiControllerBase
{
    /// <summary>
    /// Core Flow 1 - Bước 3: Nộp bài kiểm tra khảo sát chẩn đoán 30 câu hỏi
    /// </summary>
    /// <remarks>
    /// - Xác thực điều kiện học sinh và cơ sở qua Identity gRPC.
    /// - Lấy bảng đáp án và metadata câu hỏi qua Content gRPC.
    /// - Tự động chấm điểm (thang 30), ghi nhận kết quả và thời gian phản hồi (time_spent) từng câu.
    /// - Phân tích điểm yếu theo Kỹ năng và Độ khó để cung cấp dữ liệu sạch cho AI Subsystem phân lớp.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(SubmitDiagnosticResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitDiagnostic([FromBody] SubmitDiagnosticCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Tra cứu chi tiết kết quả bài nộp khảo sát theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubmitDiagnosticResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetDiagnosticSubmissionByIdQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy danh sách tóm tắt các bài khảo sát đã thực hiện của một học sinh (Điểm số, thời gian, kết quả tổng quan)
    /// </summary>
    [HttpGet("student/{studentId:guid}")]
    [ProducesResponseType(typeof(List<DiagnosticSubmissionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStudentId(Guid studentId)
    {
        var result = await Mediator.Send(new GetDiagnosticSubmissionsByStudentQuery(studentId));
        return HandleResult(result);
    }
}
