using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetMyAlerts;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý hệ thống cảnh báo sớm 3 cấp BR-04 &amp; Can thiệp sư phạm (Core Flow 6)
/// </summary>
[Route("api/practice/early-warnings")]
public class EarlyWarningsController : ApiControllerBase
{
    /// <summary>
    /// Học sinh tra cứu danh sách cảnh báo cá nhân (Mức Vàng trễ hạn, Mức Đỏ nguy cơ, Cảnh báo mục tiêu không tưởng)
    /// </summary>
    /// <remarks>
    /// Trả về toàn bộ danh sách cảnh báo 3 cấp được cá nhân hóa cho học sinh kèm theo các số liệu thống kê tổng hợp (chưa đọc, cảnh báo đỏ, cảnh báo vàng).
    /// </remarks>
    /// <param name="unreadOnly">Lọc chỉ lấy các cảnh báo chưa đọc (mặc định: false)</param>
    /// <param name="alertType">Lọc theo mức độ cảnh báo (ví dụ: 'YELLOW', 'RED', 'UNREALISTIC_GOAL', 'GREEN')</param>
    /// <param name="studentId">Mã định danh học sinh (tùy chọn nếu đã truyền qua header X-User-Id)</param>
    /// <returns>Danh sách cảnh báo cá nhân và thống kê số lượng</returns>
    [HttpGet("my-alerts")]
    [ProducesResponseType(typeof(Result<MyAlertsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<MyAlertsResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAlerts(
        [FromQuery] bool? unreadOnly = false,
        [FromQuery] string? alertType = null,
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
            return HandleResult(Result<MyAlertsResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền header X-User-Id hoặc query param studentId).")));
        }

        var query = new GetMyAlertsQuery(effectiveStudentId, unreadOnly, alertType);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }
}
