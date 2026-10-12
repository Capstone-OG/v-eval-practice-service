using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignGroupWorksheet;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignTeacher;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoClusterThematicClasses;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoPartitionMicroGroups;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;
using V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassMicroGroups;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý lớp học cơ sở, phân công giáo viên, điều phối học sinh và phân nhóm học tập vi mô
/// </summary>
[Route("api/practice/classes")]
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

    /// <summary>
    /// Core Flow 2 (Bước 4): Tự động gom cụm học sinh theo lỗ hổng kiến thức K-Means và tạo các Lớp Chuyên Đề
    /// </summary>
    /// <remarks>
    /// - Quét toàn bộ hồ sơ năng lực P(L0) 4 miền của học sinh tại cơ sở.
    /// - Tự động chạy Elbow Method chọn số lượng lớp K tối ưu.
    /// - Chạy K-Means gom học sinh vào các lớp chuyên đề (ClassType = 1) và ghi danh tự động.
    /// </remarks>
    [HttpPost("auto-cluster")]
    [ProducesResponseType(typeof(AutoClusterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AutoClusterThematicClasses([FromBody] AutoClusterRequestDto request)
    {
        var command = new AutoClusterThematicClassesCommand(
            CampusId: request.CampusId,
            MaxK: request.MaxK
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Tự động phân chia học sinh trong lớp thành các Nhóm học tập vi mô (3 - 5 bạn/nhóm)
    /// </summary>
    /// <remarks>
    /// - Phân nhóm theo độ tương đồng năng lực và điểm nghẽn kiến thức (Homogeneous Ability and Deficiency).
    /// - Ràng buộc cứng: Mỗi nhóm đảm bảo tối thiểu 3 học sinh và tối đa 5 học sinh.
    /// - Tự động xác định chủ đề trọng tâm (FocusArea), sinh tên nhóm sư phạm và gợi ý phiếu bài tập vi mô.
    /// </remarks>
    [HttpPost("{classId:guid}/micro-groups/auto-partition")]
    [ProducesResponseType(typeof(AutoPartitionMicroGroupsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AutoPartitionMicroGroups(
        Guid classId,
        [FromBody] AutoPartitionMicroGroupsRequestDto? request)
    {
        var command = new AutoPartitionMicroGroupsCommand(
            ClassId: classId,
            PreferredGroupSize: request?.PreferredGroupSize ?? 4
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy danh sách các Nhóm học tập vi mô hiện có của một lớp học
    /// </summary>
    /// <remarks>
    /// - Phục vụ màn hình Dashboard của Giảng viên trên lớp để theo dõi sơ đồ bàn học offline.
    /// - Trả về thành viên từng nhóm, điểm yếu chung và đề luyện tập đã được phân phối.
    /// </remarks>
    [HttpGet("{classId:guid}/micro-groups")]
    [ProducesResponseType(typeof(List<ClassMicroGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassMicroGroups(Guid classId)
    {
        var query = new GetClassMicroGroupsQuery(classId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Phân phối đề luyện tập / phiếu bài tập vi mô thích ứng trực tiếp cho nhóm học tập
    /// </summary>
    /// <remarks>
    /// - Giảng viên gán đề luyện tập trúng đích cho nhóm để rèn luyện theo đúng vùng trũng kiến thức.
    /// </remarks>
    [HttpPost("{classId:guid}/micro-groups/{groupId:guid}/assign-worksheet")]
    [ProducesResponseType(typeof(AssignWorksheetResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignGroupWorksheet(
        Guid classId,
        Guid groupId,
        [FromBody] AssignWorksheetRequestDto request)
    {
        var command = new AssignGroupWorksheetCommand(
            ClassId: classId,
            GroupId: groupId,
            WorksheetId: request.WorksheetId,
            WorksheetTitle: request.WorksheetTitle
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
