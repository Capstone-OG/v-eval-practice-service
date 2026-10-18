using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;
using V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetMyForecast;

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
}
