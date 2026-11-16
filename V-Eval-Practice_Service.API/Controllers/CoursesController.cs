using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Features.Schedules.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý thực thể Khóa học / Chương trình đào tạo (Courses - Schema v_eval_enrollment)
/// </summary>
[Route("api/practice/courses")]
[Route("api/v1/courses")]
public class CoursesController : ApiControllerBase
{
    private readonly IScheduleService _scheduleService;

    public CoursesController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    /// <summary>
    /// Lấy danh sách các Khóa học / Chương trình đào tạo (Courses) tại cơ sở
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CourseSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourses(
        [FromQuery] Guid? campusId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetCoursesAsync(campusId, isActive, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Tạo mới một Khóa học / Chương trình đào tạo (Course)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCourse(
        [FromBody] CreateCourseRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.CreateCourseAsync(request, ct);
        return HandleResult(result);
    }
}

