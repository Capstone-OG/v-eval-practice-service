using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassMicroGroups;

public class GetClassMicroGroupsQueryHandler : IRequestHandler<GetClassMicroGroupsQuery, Result<List<ClassMicroGroupDto>>>
{
    private readonly IClassGroupRepository _classGroupRepository;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;

    public GetClassMicroGroupsQueryHandler(
        IClassGroupRepository classGroupRepository,
        IClassEnrollmentRepository classEnrollmentRepository)
    {
        _classGroupRepository = classGroupRepository;
        _classEnrollmentRepository = classEnrollmentRepository;
    }

    public async Task<Result<List<ClassMicroGroupDto>>> Handle(
        GetClassMicroGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var targetClass = await _classEnrollmentRepository.GetClassByIdAsync(request.ClassId, cancellationToken);
        if (targetClass == null)
        {
            return Result<List<ClassMicroGroupDto>>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học với ID {request.ClassId}."));
        }

        var groups = await _classGroupRepository.GetGroupsByClassIdAsync(request.ClassId, cancellationToken);

        var dtos = groups.Select(g => new ClassMicroGroupDto(
            GroupId: g.GroupId,
            ClassId: g.ClassId,
            GroupName: g.GroupName,
            FocusArea: g.FocusArea,
            CommonWeakSkillIds: !string.IsNullOrEmpty(g.CommonWeakSkillIds)
                ? JsonSerializer.Deserialize<List<string>>(g.CommonWeakSkillIds) ?? new List<string>()
                : new List<string>(),
            RecommendedWorksheetTitle: g.RecommendedWorksheetTitle,
            AssignedWorksheetId: g.AssignedWorksheetId,
            AssignedWorksheetTitle: g.AssignedWorksheetTitle,
            WorksheetAssignedAt: g.WorksheetAssignedAt,
            CreatedAt: g.CreatedAt,
            MemberCount: g.Members.Count,
            Members: g.Members.Select(m => new ClassGroupMemberDto(
                GroupMemberId: m.GroupMemberId,
                StudentId: m.StudentId,
                JoinedAt: m.JoinedAt
            )).ToList()
        )).ToList();

        return Result<List<ClassMicroGroupDto>>.Success(dtos);
    }
}
