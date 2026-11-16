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
/// Quản lý thực thể Học kỳ / Đợt đào tạo chính thức (AcademicTerms - Schema v_eval_enrollment)
/// </summary>
[Route("api/practice/academic-terms")]
[Route("api/v1/academic-terms")]
public class AcademicTermsController : ApiControllerBase
{
    private readonly IScheduleService _scheduleService;

    public AcademicTermsController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    /// <summary>
    /// Lấy danh sách các Học kỳ / Đợt đào tạo (Academic Terms) theo cơ sở, năm học và trạng thái
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AcademicTermDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAcademicTerms(
        [FromQuery] Guid? campusId = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetAcademicTermsAsync(campusId, academicYear, status, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Khởi tạo Học kỳ / Đợt tuyển sinh mới (VD: HK1 2026-2027, HK2 2026-2027, Khóa Cấp Tốc Hè 2027)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AcademicTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAcademicTerm(
        [FromBody] CreateAcademicTermRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.CreateAcademicTermAsync(request, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Cập nhật trạng thái Học kỳ (UPCOMING, ACTIVE, COMPLETED, ARCHIVED)
    /// </summary>
    [HttpPatch("{termId:guid}/status")]
    [ProducesResponseType(typeof(AcademicTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAcademicTermStatus(
        Guid termId,
        [FromBody] UpdateAcademicTermStatusRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.UpdateAcademicTermStatusAsync(termId, request.Status, ct);
        return HandleResult(result);
    }
}

