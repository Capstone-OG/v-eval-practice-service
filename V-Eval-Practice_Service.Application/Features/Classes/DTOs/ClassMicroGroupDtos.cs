using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

public record AutoPartitionMicroGroupsRequestDto(
    int? PreferredGroupSize = 4
);

public record ClassGroupMemberDto(
    Guid GroupMemberId,
    Guid StudentId,
    DateTime JoinedAt
);

public record ClassMicroGroupDto(
    Guid GroupId,
    Guid ClassId,
    string GroupName,
    string? FocusArea,
    List<string> CommonWeakSkillIds,
    string? RecommendedWorksheetTitle,
    string? AssignedWorksheetId,
    string? AssignedWorksheetTitle,
    DateTime? WorksheetAssignedAt,
    DateTime CreatedAt,
    int MemberCount,
    List<ClassGroupMemberDto> Members
);

public record AutoPartitionMicroGroupsResponseDto(
    Guid ClassId,
    string ClassName,
    int TotalStudents,
    int TotalGroups,
    List<ClassMicroGroupDto> Groups,
    string Message
);

public record AssignWorksheetRequestDto(
    string WorksheetId,
    string WorksheetTitle
);

public record AssignWorksheetResponseDto(
    Guid GroupId,
    string GroupName,
    string AssignedWorksheetId,
    string AssignedWorksheetTitle,
    DateTime WorksheetAssignedAt,
    string Message
);
