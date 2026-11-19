using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignGroupWorksheet;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignTeacher;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoClusterThematicClasses;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoPartitionMicroGroups;
using V_Eval_Practice_Service.Application.Features.Classes.Commands.ForceAggregateHeatmap;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;
using V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassHeatmap;
using V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassMicroGroups;
using V_Eval_Practice_Service.Application.Features.Schedules.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý thực thể Lớp học cơ sở (Classes - Schema v_eval_practice): mở lớp theo Kỳ/Khóa, phân công giáo viên, điều phối học sinh, phân nhóm học tập vi mô và ma trận nhiệt
/// </summary>
[Route("api/practice/classes")]
[Route("api/v1/classes")]
public class ClassesController : ApiControllerBase
{
    private readonly IScheduleService _scheduleService;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;

    public ClassesController(
        IScheduleService scheduleService,
        IClassEnrollmentRepository classEnrollmentRepository)
    {
        _scheduleService = scheduleService;
        _classEnrollmentRepository = classEnrollmentRepository;
    }

    /// <summary>
    /// Tra cứu danh sách Lớp học theo Cơ sở (Campus), Học kỳ (Term), Khóa học (Course) và Giảng viên
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(
        [FromQuery] Guid? campusId = null,
        [FromQuery] Guid? termId = null,
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? teacherId = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetClassesAsync(campusId, termId, courseId, teacherId, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Mở lớp học mới gắn với Học kỳ (Term) và Khóa học (Course), kèm thiết lập Khung lịch tuần và tự động sinh chuỗi buổi học
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClassOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateClassWithSchedule(
        [FromBody] CreateClassWithScheduleRequestDto request,
        CancellationToken ct = default)
    {
        Guid? assignedBy = null;
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            assignedBy = parsedId;
        }
        else
        {
            var claimVal = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(claimVal) && Guid.TryParse(claimVal, out var claimGuid))
            {
                assignedBy = claimGuid;
            }
        }

        var result = await _scheduleService.CreateClassWithScheduleAsync(request, assignedBy, ct);
        return HandleResult(result);
    }
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTeacher(Guid classId, [FromBody] AssignTeacherRequestDto request)
    {
        Guid? assignedBy = null;
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            assignedBy = parsedId;
        }

        Guid? managerCampusId = request.ManagerCampusId;
        if (!managerCampusId.HasValue && Request.Headers.TryGetValue("X-Campus-Id", out var campusHeader) && Guid.TryParse(campusHeader, out var parsedCampusId))
        {
            managerCampusId = parsedCampusId;
        }

        var command = new AssignTeacherCommand(
            ClassId: classId,
            TeacherId: request.TeacherId,
            AssignedBy: assignedBy,
            TeacherCampusId: request.TeacherCampusId,
            ManagerCampusId: managerCampusId
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Ghi danh hoặc điều chuyển thủ công một học sinh vào lớp cụ thể, tự động kiểm soát trần 20 học sinh/lớp và tràn sang lớp mới (Cohort 02) cho học sinh thứ 21 (MGR_02, MGR_04, MGR_13)
    /// </summary>
    [HttpPost("{classId:guid}/enroll-student")]
    [ProducesResponseType(typeof(EnrollOrTransferStudentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnrollOrTransferStudent(
        Guid classId,
        [FromBody] EnrollOrTransferStudentRequestDto request,
        CancellationToken ct = default)
    {
        if (request == null || request.StudentId == Guid.Empty)
        {
            return HandleResult(Result<EnrollOrTransferStudentResponseDto>.Failure(
                Error.Validation("Class.InvalidStudentId", "Vui lòng chọn mã học sinh hợp lệ để xếp lớp.")));
        }

        var targetClass = await _classEnrollmentRepository.GetClassByIdAsync(classId, ct);
        if (targetClass == null)
        {
            return HandleResult(Result<EnrollOrTransferStudentResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học cơ sở với ID {classId}.")));
        }

        if (request.ManagerCampusId.HasValue &&
            request.ManagerCampusId.Value != Guid.Empty &&
            request.ManagerCampusId.Value != targetClass.CampusId)
        {
            return HandleResult(Result<EnrollOrTransferStudentResponseDto>.Failure(
                Error.Forbidden("Class.CampusAccessDenied",
                    "403 Access Denied - Bạn chỉ có quyền quản lý cơ sở được phân công.")));
        }

        var res = await _classEnrollmentRepository.EnrollOrTransferStudentToClassAsync(classId, request.StudentId, ct);
        if (res == null)
        {
            return HandleResult(Result<EnrollOrTransferStudentResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học cơ sở với ID {classId}.")));
        }

        return HandleResult(Result<EnrollOrTransferStudentResponseDto>.Success(res));
    }

    /// <summary>
    /// Core Flow 2 (Bước 4): Tự động gom cụm học sinh theo lỗ hổng kiến thức K-Means và tạo các Lớp Chuyên Đề
    /// </summary>
    /// <remarks>
    /// - Quét toàn bộ hồ sơ năng lực P(L0) 4 miền của học sinh tại cơ sở.
    /// - Tự động chạy Elbow Method chọn số lượng lớp K tối ưu.
    /// - Chạy K-Means gom học sinh vào các lớp chuyên đề (ClassType = 1) và ghi danh tự động (hoặc xem trước khi DryRun = true).
    /// </remarks>
    [HttpPost("auto-cluster")]
    [ProducesResponseType(typeof(AutoClusterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AutoClusterThematicClasses([FromBody] AutoClusterRequestDto request)
    {
        var command = new AutoClusterThematicClassesCommand(
            CampusId: request.CampusId,
            MaxK: request.MaxK > 0 ? request.MaxK : 12,
            MinCapacity: request.MinCapacity > 0 ? request.MinCapacity : 5,
            MaxCapacity: request.MaxCapacity > 0 ? request.MaxCapacity : 20,
            DryRun: request.DryRun,
            ManagerCampusId: request.ManagerCampusId,
            FoundationThreshold: request.FoundationThreshold > 0 ? request.FoundationThreshold : 0.50,
            AdvancedThreshold: request.AdvancedThreshold > 0 ? request.AdvancedThreshold : 0.75,
            IncludeEmptyClasses: request.IncludeEmptyClasses,
            PreserveExistingEnrollments: request.PreserveExistingEnrollments
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
            WorksheetTitle: request.WorksheetTitle,
            QuestionCount: request.QuestionCount ?? 5
        );

        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 5 - API 2: Lấy ma trận bản đồ nhiệt (Class Heatmap Matrix) dành cho Giảng viên cơ sở
    /// </summary>
    /// <param name="classId">ID định danh lớp học</param>
    /// <param name="domainCode">Mã phân môn tùy chọn (DOM_MATH, DOM_LANG, DOM_NAT_SCI, DOM_SOC_SCI)</param>
    /// <param name="minMastery">Ngưỡng lọc P(Lt) tối thiểu</param>
    /// <remarks>
    /// - Phản hồi siêu tốc dưới 300ms nhờ cơ chế bộ đệm phân tán / in-memory cache.
    /// - Trả về danh sách cột kỹ năng kèm chỉ số trung bình và trạng thái cảnh báo sư phạm (CRITICAL_RED, WARNING_AMBER, SAFE_GREEN).
    /// - Nhận diện hiện tượng phân hóa 2 cực (IsBimodal = true) để khuyến nghị chia nhóm vi mô.
    /// - Ma trận hàng học sinh với từng ô giá trị P(Lt) và phân loại nguy cơ (SAFE, MEDIUM_RISK, HIGH_RISK).
    /// </remarks>
    [HttpGet("{classId:guid}/heatmap")]
    [ProducesResponseType(typeof(ClassHeatmapResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassHeatmap(
        Guid classId,
        [FromQuery] string? domainCode = null,
        [FromQuery] double? minMastery = null)
    {
        var query = new GetClassHeatmapQuery(classId, domainCode, minMastery);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 5 - API 6: Kích hoạt cưỡng chế tính toán lại ma trận nhiệt lớp học (Force Aggregate)
    /// </summary>
    /// <param name="classId">ID định danh lớp học</param>
    /// <remarks>
    /// - Cho phép giảng viên hoặc quản trị viên tính toán lại ma trận ngay lập tức (bỏ qua chu kỳ quét 15 phút của worker).
    /// - Cập nhật bảng ClassHeatmapSummaries và làm mới bộ đệm Cache.
    /// </remarks>
    [HttpPost("{classId:guid}/heatmap/force-aggregate")]
    [ProducesResponseType(typeof(ForceAggregateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForceAggregateHeatmap(Guid classId)
    {
        var command = new ForceAggregateHeatmapCommand(classId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
