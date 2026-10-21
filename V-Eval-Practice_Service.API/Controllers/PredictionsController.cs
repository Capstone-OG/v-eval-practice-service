using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;
using V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetMyForecast;
using V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetPrescriptionSession;
using V_Eval_Practice_Service.Application.Features.Predictions.Commands.SubmitPrescriptionSession;
using V_Eval_Practice_Service.Application.Features.Predictions.Commands.RecalculatePrediction;

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

    /// <summary>
    /// Nộp bài làm ôn tập vi mô, chấm điểm và xóa thâm hụt điểm (Knapsack Deficit Recovery)
    /// </summary>
    /// <remarks>
    /// Chấm điểm các câu trả lời của học sinh. Nếu đạt tỷ lệ chính xác &gt;= 67%, hệ thống đánh dấu dạng bài đã hoàn thành (is_remedied = true), nâng xác suất thành thạo BKT P(L_t) và tự động trừ bớt điểm thâm hụt trong Toa thuốc.
    /// </remarks>
    /// <param name="patternId">Mã dạng bài trong toa thuốc (ví dụ: PAT_TOAN_LOGIC_01)</param>
    /// <param name="request">Payload danh sách câu trả lời của học sinh</param>
    /// <param name="studentId">Mã định danh học sinh (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Kết quả chấm điểm, điểm thâm hụt còn lại và tỷ lệ thành thạo mới</returns>
    [HttpPost("prescriptions/{patternId}/submit-session")]
    [ProducesResponseType(typeof(Result<SubmitPrescriptionSessionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<SubmitPrescriptionSessionResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<SubmitPrescriptionSessionResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result<SubmitPrescriptionSessionResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitPrescriptionPracticeSession(
        [FromRoute] string patternId,
        [FromBody] SubmitPrescriptionSessionRequestDto request,
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
            return HandleResult(Result<SubmitPrescriptionSessionResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền X-User-Id header hoặc query param studentId).")));
        }

        var command = new SubmitPrescriptionSessionCommand(effectiveStudentId, patternId, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Kích hoạt tái tính toán lại dải điểm SEM và Toa thuốc bù điểm Knapsack ROI (Core Flow 6)
    /// </summary>
    /// <remarks>
    /// Ước lượng lại dải điểm chuẩn hóa [Min, Max], tính toán lại mức độ thâm hụt so với mục tiêu và tối ưu lại 2-3 dạng bài bù điểm có ROI cao nhất sau khi hoàn thành bài thi thử hoặc chuỗi bài tập.
    /// </remarks>
    /// <param name="request">Payload yêu cầu tính toán lại (tùy chọn nguồn trigger_source và reference_submission_id)</param>
    /// <returns>Dải điểm dự báo mới và bản ghi dự báo vừa tạo</returns>
    [HttpPost("recalculate")]
    [ProducesResponseType(typeof(Result<RecalculatePredictionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<RecalculatePredictionResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecalculatePrediction([FromBody] RecalculatePredictionRequestDto? request = null)
    {
        Guid effectiveStudentId = Guid.Empty;

        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            effectiveStudentId = parsedId;
        }

        var effectiveRequest = request ?? new RecalculatePredictionRequestDto();
        var command = new RecalculatePredictionCommand(effectiveStudentId, effectiveRequest);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
