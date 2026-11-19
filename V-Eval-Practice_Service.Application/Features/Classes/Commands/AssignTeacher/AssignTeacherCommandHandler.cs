using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignTeacher;

public class AssignTeacherCommandHandler : IRequestHandler<AssignTeacherCommand, Result<AssignTeacherResponseDto>>
{
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly ILogger<AssignTeacherCommandHandler> _logger;

    public AssignTeacherCommandHandler(
        IClassEnrollmentRepository classEnrollmentRepository,
        ILogger<AssignTeacherCommandHandler> logger)
    {
        _classEnrollmentRepository = classEnrollmentRepository;
        _logger = logger;
    }

    public async Task<Result<AssignTeacherResponseDto>> Handle(
        AssignTeacherCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TeacherId == Guid.Empty)
        {
            return Result<AssignTeacherResponseDto>.Failure(
                Error.Validation("Class.InvalidTeacher", "Vui lòng chọn giáo viên phụ trách hợp lệ"));
        }

        var targetClass = await _classEnrollmentRepository.GetClassByIdAsync(request.ClassId, cancellationToken);
        if (targetClass == null)
        {
            return Result<AssignTeacherResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học cơ sở với ID {request.ClassId}."));
        }

        if (request.ManagerCampusId.HasValue &&
            request.ManagerCampusId.Value != Guid.Empty &&
            request.ManagerCampusId.Value != targetClass.CampusId)
        {
            return Result<AssignTeacherResponseDto>.Failure(
                Error.Forbidden("Class.CampusAccessDenied",
                    "403 Access Denied - Bạn chỉ có quyền quản lý cơ sở được phân công."));
        }

        if (request.TeacherCampusId.HasValue &&
            request.TeacherCampusId.Value != Guid.Empty &&
            request.TeacherCampusId.Value != targetClass.CampusId)
        {
            return Result<AssignTeacherResponseDto>.Failure(
                Error.Validation("Class.CrossCampusTeacher",
                    "Không thể phân công giảng viên thuộc cơ sở khác phụ trách lớp học tại cơ sở này."));
        }

        targetClass.TeacherId = request.TeacherId;
        targetClass.AssignedBy = request.AssignedBy;
        targetClass.AssignedAt = DateTime.UtcNow;

        await _classEnrollmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Teacher {TeacherId} assigned to Class {ClassId} ('{ClassName}') by {AssignedBy}",
            request.TeacherId, targetClass.ClassId, targetClass.Name, request.AssignedBy);

        var responseDto = new AssignTeacherResponseDto
        {
            ClassId = targetClass.ClassId,
            ClassName = targetClass.Name,
            CampusId = targetClass.CampusId,
            TeacherId = targetClass.TeacherId.Value,
            AssignedBy = targetClass.AssignedBy,
            AssignedAt = targetClass.AssignedAt.Value,
            Status = targetClass.Status,
            Message = $"Phân công giáo viên phụ trách cho lớp {targetClass.Name} thành công."
        };

        return Result<AssignTeacherResponseDto>.Success(responseDto);
    }
}
