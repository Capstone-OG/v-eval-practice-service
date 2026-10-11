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
        var targetClass = await _classEnrollmentRepository.GetClassByIdAsync(request.ClassId, cancellationToken);
        if (targetClass == null)
        {
            return Result<AssignTeacherResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học cơ sở với ID {request.ClassId}."));
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
