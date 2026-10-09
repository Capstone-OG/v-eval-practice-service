using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Graph;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoPartitionMicroGroups;

public class AutoPartitionMicroGroupsCommandHandler : IRequestHandler<AutoPartitionMicroGroupsCommand, Result<AutoPartitionMicroGroupsResponseDto>>
{
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly IClassGroupRepository _classGroupRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IClassMicroClusterer _microClusterer;
    private readonly ILogger<AutoPartitionMicroGroupsCommandHandler> _logger;

    public AutoPartitionMicroGroupsCommandHandler(
        IClassEnrollmentRepository classEnrollmentRepository,
        IClassGroupRepository classGroupRepository,
        ILearningProfileRepository learningProfileRepository,
        IContentGrpcClient contentGrpcClient,
        IClassMicroClusterer microClusterer,
        ILogger<AutoPartitionMicroGroupsCommandHandler> logger)
    {
        _classEnrollmentRepository = classEnrollmentRepository;
        _classGroupRepository = classGroupRepository;
        _learningProfileRepository = learningProfileRepository;
        _contentGrpcClient = contentGrpcClient;
        _microClusterer = microClusterer;
        _logger = logger;
    }

    public async Task<Result<AutoPartitionMicroGroupsResponseDto>> Handle(
        AutoPartitionMicroGroupsCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bắt đầu tự động phân chia nhóm vi mô (3 - 5 bạn) cho lớp {ClassId}", request.ClassId);

        // 1. Kiểm tra lớp học tồn tại
        var targetClass = await _classEnrollmentRepository.GetClassByIdAsync(request.ClassId, cancellationToken);
        if (targetClass == null)
        {
            return Result<AutoPartitionMicroGroupsResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học với ID {request.ClassId}."));
        }

        // 2. Lấy danh sách học sinh đang ghi danh trong lớp
        var enrollments = await _classEnrollmentRepository.GetEnrollmentsByClassIdAsync(request.ClassId, cancellationToken);
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

        if (studentIds.Count < 3)
        {
            return Result<AutoPartitionMicroGroupsResponseDto>.Failure(
                Error.Validation("Class.InsufficientStudents",
                    $"Lớp '{targetClass.Name}' hiện chỉ có {studentIds.Count} học sinh. Cần tối thiểu 3 học sinh để phân nhóm học tập vi mô (3 - 5 bạn/bàn)."));
        }

        // 3. Nạp cây kỹ năng từ Content Service qua gRPC (ánh xạ tên kỹ năng và mã miền)
        var skillNameMap = new Dictionary<Guid, string>();
        var skillDomainMap = new Dictionary<Guid, string>();
        try
        {
            var skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(cancellationToken);
            foreach (var node in skillsTree)
            {
                if (Guid.TryParse(node.SkillId.ToString(), out var gId))
                {
                    skillNameMap[gId] = node.Name;
                    if (!string.IsNullOrEmpty(node.DomainCode))
                        skillDomainMap[gId] = node.DomainCode;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối gRPC Content Service để lấy cây kỹ năng. Sử dụng fallback.");
        }

        // 4. Nạp hồ sơ năng lực P(L0) của các học sinh trong lớp
        var profiles = await _learningProfileRepository.GetByStudentIdsAsync(studentIds, cancellationToken);
        var profilesByStudent = profiles.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        // 5. Chuẩn bị hồ sơ kỹ năng cho thuật toán phân nhóm vi mô
        var studentProfileItems = new List<StudentSkillProfileItem>();
        foreach (var sId in studentIds)
        {
            var item = new StudentSkillProfileItem { StudentId = sId };

            if (profilesByStudent.TryGetValue(sId, out var sProfiles))
            {
                var langList = new List<double>();
                var mathList = new List<double>();
                var natList = new List<double>();
                var socList = new List<double>();

                foreach (var p in sProfiles)
                {
                    item.SkillScores[p.SkillId] = p.MasteryScore;

                    if (skillDomainMap.TryGetValue(p.SkillId, out var dCode))
                    {
                        switch (dCode)
                        {
                            case StudentKMeansClusterer.CodeLang: langList.Add(p.MasteryScore); break;
                            case StudentKMeansClusterer.CodeMath: mathList.Add(p.MasteryScore); break;
                            case StudentKMeansClusterer.CodeNatSci: natList.Add(p.MasteryScore); break;
                            case StudentKMeansClusterer.CodeSocSci: socList.Add(p.MasteryScore); break;
                        }
                    }
                }

                item.DomainFeatures[0] = langList.Count > 0 ? langList.Average() : 0.50;
                item.DomainFeatures[1] = mathList.Count > 0 ? mathList.Average() : 0.50;
                item.DomainFeatures[2] = natList.Count > 0 ? natList.Average() : 0.50;
                item.DomainFeatures[3] = socList.Count > 0 ? socList.Average() : 0.50;
            }
            else
            {
                item.DomainFeatures = new[] { 0.50, 0.50, 0.50, 0.50 };
            }

            studentProfileItems.Add(item);
        }

        // 6. Chạy thuật toán phân nhóm vi mô (đảm bảo 3 <= size <= 5)
        int prefSize = request.PreferredGroupSize ?? 4;
        var partitionResults = _microClusterer.Partition(
            studentProfileItems,
            skillNameMap,
            prefSize
        );

        // 7. Xóa các nhóm cũ của lớp này (nếu có) và tạo mới
        await _classGroupRepository.DeleteGroupsByClassIdAsync(request.ClassId, cancellationToken);

        var newClassGroups = new List<ClassGroup>();
        foreach (var pRes in partitionResults)
        {
            var group = new ClassGroup
            {
                GroupId = Guid.NewGuid(),
                ClassId = request.ClassId,
                GroupName = pRes.GroupName,
                FocusArea = pRes.FocusArea,
                CommonWeakSkillIds = JsonSerializer.Serialize(pRes.WeakSkillIds.Select(id => id.ToString())),
                RecommendedWorksheetTitle = pRes.RecommendedWorksheetTitle,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var stId in pRes.StudentIds)
            {
                group.Members.Add(new ClassGroupMember
                {
                    GroupMemberId = Guid.NewGuid(),
                    GroupId = group.GroupId,
                    StudentId = stId,
                    JoinedAt = DateTime.UtcNow
                });
            }

            newClassGroups.Add(group);
        }

        await _classGroupRepository.CreateGroupsAsync(newClassGroups, cancellationToken);
        _logger.LogInformation("Đã phân chia thành công {GroupCount} nhóm vi mô cho lớp '{ClassName}' ({TotalStudents} học sinh)",
            newClassGroups.Count, targetClass.Name, studentIds.Count);

        // 8. Đóng gói DTO phản hồi
        var groupDtos = newClassGroups.Select(g => new ClassMicroGroupDto(
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

        var response = new AutoPartitionMicroGroupsResponseDto(
            ClassId: targetClass.ClassId,
            ClassName: targetClass.Name,
            TotalStudents: studentIds.Count,
            TotalGroups: groupDtos.Count,
            Groups: groupDtos,
            Message: $"Tự động phân chia thành công {groupDtos.Count} nhóm học tập vi mô (3 - 5 bạn/nhóm) theo điểm nghẽn kiến thức tương đồng."
        );

        return Result<AutoPartitionMicroGroupsResponseDto>.Success(response);
    }
}
