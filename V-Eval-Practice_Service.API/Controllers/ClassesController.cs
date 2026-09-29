using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignTeacher;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý lớp học cơ sở, phân công giáo viên và điều phối học sinh (Core Flow 2 - Phase 3 and 5)
/// </summary>
[Route("api/v1/practice/classes")]
public class ClassesController : ApiControllerBase
{
    /// <summary>
    /// Core Flow 2 - API 9: Phân công hoặc điều chuyển giáo viên phụ trách lớp học cơ sở
    /// </summary>
    /// <remarks>
    /// - Dành cho Trưởng bộ phận chuyên môn cơ sở (Academic Manager).
    /// - Cập nhật TeacherId, AssignedBy và thời điểm phân công AssignedAt.
    /// </remarks>
    [HttpPut("{classId:guid}/assign-teacher")]
    [ProducesResponseType(typeof(AssignTeacherResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTeacher(Guid classId, [FromBody] AssignTeacherRequestDto request)
    {
        Guid? assignedBy = null;
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            assignedBy = parsedId;
        }

        var command = new AssignTeacherCommand(
            ClassId: classId,
            TeacherId: request.TeacherId,
            AssignedBy: assignedBy
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
