using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassMicroGroups;

public class GetClassMicroGroupsQueryHandler : IRequestHandler<GetClassMicroGroupsQuery, Result<List<ClassMicroGroupDto>>>
{
    private readonly IClassGroupRepository _classGroupRepository;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly ILogger<GetClassMicroGroupsQueryHandler> _logger;

    public GetClassMicroGroupsQueryHandler(
        IClassGroupRepository classGroupRepository,
        IClassEnrollmentRepository classEnrollmentRepository,
        IIdentityGrpcClient identityGrpcClient,
        IContentGrpcClient contentGrpcClient,
        ILearningProfileRepository learningProfileRepository,
        ILogger<GetClassMicroGroupsQueryHandler> logger)
    {
        _classGroupRepository = classGroupRepository;
        _classEnrollmentRepository = classEnrollmentRepository;
        _identityGrpcClient = identityGrpcClient;
        _contentGrpcClient = contentGrpcClient;
        _learningProfileRepository = learningProfileRepository;
        _logger = logger;
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

        // Thu thập toàn bộ ID học sinh trong các nhóm để nạp tên thật từ Identity Service
        var studentIds = groups.SelectMany(g => g.Members.Select(m => m.StudentId)).Distinct().ToList();
        var studentNames = new Dictionary<Guid, string>();
        if (studentIds.Count > 0)
        {
            try
            {
                studentNames = await _identityGrpcClient.GetStudentNamesAsync(studentIds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy tên học sinh từ Identity qua gRPC trong GetClassMicroGroups.");
            }
        }

        // Nạp cây kỹ năng để ánh xạ ID kỹ năng sang tên thật
        var skillNameMap = new Dictionary<Guid, string>();
        try
        {
            var skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(cancellationToken);
            foreach (var node in skillsTree)
            {
                if (Guid.TryParse(node.SkillId.ToString(), out var gId))
                {
                    skillNameMap[gId] = node.Name;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy cây kỹ năng từ Content Service qua gRPC trong GetClassMicroGroups.");
        }

        // Nạp hồ sơ năng lực của học sinh để hiển thị độ làm chủ
        var profiles = studentIds.Count > 0
            ? await _learningProfileRepository.GetByStudentIdsAsync(studentIds, cancellationToken)
            : new List<Domain.Entities.LearningProfile>();
        var profilesByStudent = profiles.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = groups.Select(g => {
            var weakIds = !string.IsNullOrEmpty(g.CommonWeakSkillIds)
                ? JsonSerializer.Deserialize<List<string>>(g.CommonWeakSkillIds) ?? new List<string>()
                : new List<string>();

            var weakNames = weakIds
                .Select(idStr => Guid.TryParse(idStr, out var gid) && skillNameMap.TryGetValue(gid, out var sName) ? sName : null)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct()
                .Take(3)
                .ToList();

            return new ClassMicroGroupDto(
                GroupId: g.GroupId,
                ClassId: g.ClassId,
                GroupName: g.GroupName,
                FocusArea: g.FocusArea,
                CommonWeakSkillIds: weakIds,
                RecommendedWorksheetTitle: g.RecommendedWorksheetTitle,
                AssignedWorksheetId: g.AssignedWorksheetId,
                AssignedWorksheetTitle: g.AssignedWorksheetTitle,
                WorksheetAssignedAt: g.WorksheetAssignedAt,
                CreatedAt: g.CreatedAt,
                MemberCount: g.Members.Count,
                Members: g.Members.Select(m => {
                    studentNames.TryGetValue(m.StudentId, out var sName);
                    double? mastery = null;
                    if (profilesByStudent.TryGetValue(m.StudentId, out var sProfs) && sProfs.Count > 0)
                    {
                        mastery = Math.Round(sProfs.Average(p => p.MasteryScore), 2);
                    }
                    return new ClassGroupMemberDto(
                        GroupMemberId: m.GroupMemberId,
                        StudentId: m.StudentId,
                        JoinedAt: m.JoinedAt,
                        Name: sName,
                        Mastery: mastery
                    );
                }).ToList(),
                CommonWeakSkillNames: weakNames
            );
        }).ToList();

        return Result<List<ClassMicroGroupDto>>.Success(dtos);
    }
}
