using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.Commands.StartMockExam;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý Phòng thi thử mô phỏng có giám sát &amp; Thẩm định Mentor (Core Flow 3 - Module 3)
/// </summary>
[Route("api/practice/mock-exams")]
public class MockExamsController : ApiControllerBase
{
    /// <summary>
    /// API 1: Khởi tạo phiên thi thử chuẩn hóa 120 câu / 150 phút (Mock Exam)
    /// </summary>
    /// <remarks>
    /// Bắt đầu đếm ngược thời gian 150 phút, lấy 120 câu hỏi bảo mật từ Content Service (ẩn đáp án đúng),
    /// và thiết lập trạng thái giám sát thi cử (Proctoring).
    /// </remarks>
    /// <param name="examId">Mã đề thi thử</param>
    /// <param name="request">Thông tin học sinh đăng ký làm bài</param>
    /// <returns>Phiên thi thử khởi tạo cùng danh sách câu hỏi và đồng hồ đếm ngược</returns>
    [HttpPost("{examId:guid}/start")]
    [ProducesResponseType(typeof(Result<StartMockExamResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<StartMockExamResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<StartMockExamResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartMockExam(
        [FromRoute] Guid examId,
        [FromBody] StartMockExamRequestDto request)
    {
        var command = new StartMockExamCommand(examId, request.StudentId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
