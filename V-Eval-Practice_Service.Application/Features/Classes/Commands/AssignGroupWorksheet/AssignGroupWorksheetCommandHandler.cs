using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignGroupWorksheet;

public class AssignGroupWorksheetCommandHandler : IRequestHandler<AssignGroupWorksheetCommand, Result<AssignWorksheetResponseDto>>
{
    private readonly IClassGroupRepository _classGroupRepository;
    private readonly ILogger<AssignGroupWorksheetCommandHandler> _logger;

    public AssignGroupWorksheetCommandHandler(
        IClassGroupRepository classGroupRepository,
        ILogger<AssignGroupWorksheetCommandHandler> logger)
    {
        _classGroupRepository = classGroupRepository;
        _logger = logger;
    }

    public async Task<Result<AssignWorksheetResponseDto>> Handle(
        AssignGroupWorksheetCommand request,
        CancellationToken cancellationToken)
    {
        var group = await _classGroupRepository.GetGroupByIdAsync(request.GroupId, cancellationToken);
        if (group == null || group.ClassId != request.ClassId)
        {
            return Result<AssignWorksheetResponseDto>.Failure(
                Error.NotFound("ClassGroup.NotFound", $"Không tìm thấy nhóm học tập với ID {request.GroupId} trong lớp {request.ClassId}."));
        }

        bool success = await _classGroupRepository.AssignWorksheetAsync(
            request.GroupId,
            request.WorksheetId,
            request.WorksheetTitle,
            cancellationToken);

        if (!success)
        {
            return Result<AssignWorksheetResponseDto>.Failure(
                Error.Failure("ClassGroup.AssignFailed", "Không thể gán đề luyện tập cho nhóm học tập."));
        }

        _logger.LogInformation("Đã gán đề '{Title}' ({Id}) cho nhóm {GroupName} ({GroupId})",
            request.WorksheetTitle, request.WorksheetId, group.GroupName, group.GroupId);

        var response = new AssignWorksheetResponseDto(
            GroupId: group.GroupId,
            GroupName: group.GroupName,
            AssignedWorksheetId: request.WorksheetId,
            AssignedWorksheetTitle: request.WorksheetTitle,
            WorksheetAssignedAt: DateTime.UtcNow,
            Message: $"Đã phân phối thành công đề luyện tập '{request.WorksheetTitle}' tới {group.GroupName} ({group.Members.Count} học sinh)."
        );

        return Result<AssignWorksheetResponseDto>.Success(response);
    }
}
