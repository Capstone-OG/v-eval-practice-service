using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;
using V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetMyForecast;
using V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetPrescriptionSession;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý dự báo khoảng điểm chuẩn hóa &amp; Toa thuốc bù điểm Knapsack ROI (Core Flow 6)
/// </summary>
[Route("api/practice/predictions")]
public class PredictionsController : ApiControllerBase
{
    /// <summary>
    /// Tra cứu dải điểm dự báo &amp; Toa thuốc bù điểm tuần này của học sinh
    /// </summary>
    /// <remarks>
    /// Trả về dải điểm tin cậy [Min, Max] tính theo SEM 95%, mức độ thâm hụt so với mục tiêu và danh sách 2-3 dạng bài có tỷ suất ROI cao nhất cần bù điểm.
    /// </remarks>
    /// <param name="studentId">Mã định danh học sinh (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Dải điểm dự báo và Toa thuốc bù điểm</returns>
    [HttpGet("my-forecast")]
    [ProducesResponseType(typeof(Result<MyForecastResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<MyForecastResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result<MyForecastResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyForecast([FromQuery] Guid? studentId = null)
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
            return HandleResult(Result<MyForecastResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền X-User-Id header hoặc query param studentId).")));
        }

        var query = new GetMyForecastQuery(effectiveStudentId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy đề ôn tập 3-5 câu biến thể cho dạng bài trong Toa thuốc bù điểm
    /// </summary>
    /// <remarks>
    /// Kiểm tra dạng bài có nằm trong Toa thuốc bù điểm của học sinh hay không và bốc ngẫu nhiên 3-5 câu hỏi biến thể từ Ngân hàng đề bảo mật (ẩn đáp án đúng).
    /// </remarks>
    /// <param name="patternId">Mã dạng bài trong toa thuốc (ví dụ: PAT_TOAN_LOGIC_01)</param>
    /// <param name="studentId">Mã định danh học sinh (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Phiên làm bài và danh sách câu hỏi biến thể</returns>
    [HttpGet("prescriptions/{patternId}/practice-session")]
    [ProducesResponseType(typeof(Result<PrescriptionSessionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<PrescriptionSessionResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<PrescriptionSessionResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result<PrescriptionSessionResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPrescriptionPracticeSession(
        [FromRoute] string patternId,
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
            return HandleResult(Result<PrescriptionSessionResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền X-User-Id header hoặc query param studentId).")));
        }

        var query = new GetPrescriptionSessionQuery(effectiveStudentId, patternId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }
}
