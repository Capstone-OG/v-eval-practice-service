using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Features.Analytics.DTOs;
using V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetParentDashboard;
using V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetStudentDashboard;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Controller xử lý các phân tích số liệu học tập, Dashboard năng lực học sinh, cổng giám sát phụ huynh và báo cáo so sánh cơ sở (Core Flow 5)
/// </summary>
[Route("api/practice/analytics")]
public class AnalyticsController : ApiControllerBase
{
    /// <summary>
    /// Core Flow 5 - API 1: Lấy dữ liệu tổng hợp cho Dashboard năng lực và chuyên cần của học sinh
    /// </summary>
    /// <param name="studentId">ID định danh duy nhất của học sinh</param>
    /// <remarks>
    /// - Trực quan hóa Biểu đồ Radar 4 miền năng lực ĐGNL (Điểm thực tế vs Điểm mục tiêu).
    /// - Tiến độ chặng học thích ứng (Milestones) và chuỗi học tập chuyên cần (Streak Tracker).
    /// - Lịch sử thi thử ĐGNL gần nhất và cờ kích hoạt Ghost Onboarding UI nếu học sinh chưa có đủ dữ liệu.
    /// </remarks>
    [HttpGet("student/{studentId:guid}/dashboard")]
    [ProducesResponseType(typeof(StudentDashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentDashboard(Guid studentId)
    {
        var query = new GetStudentDashboardQuery(studentId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 5 - API 4: Lấy dữ liệu giám sát học tập dành cho Phụ huynh (Parent Companion Portal)
    /// </summary>
    /// <param name="parentId">ID định danh của tài khoản phụ huynh</param>
    /// <remarks>
    /// - Truy xuất toàn bộ con cái liên kết qua Identity Service.
    /// - Chỉ số sẵn sàng thi tổng hợp (Exam Readiness Index R_exam), phân cấp (READY, NEAR_READY, AT_RISK).
    /// - Tổng số giờ tự học trong tuần, so sánh với định mức 12 giờ và chuỗi ngày chuyên cần.
    /// - Hộp thông báo AI thông minh (AiParentAlerts) phản ánh các bước tiến và khuyến nghị nhắc nhở tại gia đình.
    /// </remarks>
    [HttpGet("parent/{parentId:guid}/children")]
    [ProducesResponseType(typeof(ParentDashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParentDashboard(Guid parentId)
    {
        var query = new GetParentDashboardQuery(parentId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }
}
