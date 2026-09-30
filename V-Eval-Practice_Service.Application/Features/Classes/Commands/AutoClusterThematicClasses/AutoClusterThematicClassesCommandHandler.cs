using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Graph;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoClusterThematicClasses;

public class AutoClusterThematicClassesCommandHandler : IRequestHandler<AutoClusterThematicClassesCommand, Result<AutoClusterResponseDto>>
{
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IStudentKMeansClusterer _studentKMeansClusterer;
    private readonly ILogger<AutoClusterThematicClassesCommandHandler> _logger;

    public AutoClusterThematicClassesCommandHandler(
        IClassEnrollmentRepository classEnrollmentRepository,
        ILearningProfileRepository learningProfileRepository,
        IContentGrpcClient contentGrpcClient,
        IStudentKMeansClusterer studentKMeansClusterer,
        ILogger<AutoClusterThematicClassesCommandHandler> logger)
    {
        _classEnrollmentRepository = classEnrollmentRepository;
        _learningProfileRepository = learningProfileRepository;
        _contentGrpcClient = contentGrpcClient;
        _studentKMeansClusterer = studentKMeansClusterer;
        _logger = logger;
    }

    public async Task<Result<AutoClusterResponseDto>> Handle(
        AutoClusterThematicClassesCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bắt đầu tự động phân cụm K-Means cho cơ sở {CampusId} với MaxK={MaxK}",
            request.CampusId, request.MaxK);

        // 1. Lấy danh sách học sinh thuộc cơ sở đã có mặt trong hệ thống
        var enrolledStudentIds = await _classEnrollmentRepository.GetEnrolledStudentIdsByCampusIdAsync(request.CampusId, cancellationToken);
        if (enrolledStudentIds.Count == 0)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.NotFound("Campus.NoStudents", $"Không tìm thấy học sinh nào thuộc cơ sở {request.CampusId}."));
        }

        // 2. Nạp cấu trúc cây kỹ năng từ Content Service qua gRPC để ánh xạ SkillId -> DomainCode
        IReadOnlyList<SkillTreeNodeDto> skillsTree;
        try
        {
            skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể kết nối gRPC Content Service để lấy cây kỹ năng.");
            return Result<AutoClusterResponseDto>.Failure(
                Error.Failure("Grpc.ContentUnavailable", "Không thể nạp cây kỹ năng để ánh xạ miền năng lực."));
        }

        var skillDomainMap = skillsTree.ToDictionary(s => s.SkillId, s => s.DomainCode);

        // 3. Nạp hồ sơ năng lực tiên nghiệm P(L0) của học sinh tại cơ sở
        var profiles = await _learningProfileRepository.GetByStudentIdsAsync(enrolledStudentIds, cancellationToken);
        var profileGroups = profiles.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        // 4. Xây dựng vector đặc trưng 4 chiều [DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI] cho mỗi học sinh
        var studentVectors = new List<StudentFeatureVector>();
        foreach (var studentId in enrolledStudentIds)
        {
            if (profileGroups.TryGetValue(studentId, out var studentProfiles))
            {
                var scoresByDomain = new Dictionary<string, List<double>>();
                foreach (var p in studentProfiles)
                {
                    if (skillDomainMap.TryGetValue(p.SkillId, out var domCode) && !string.IsNullOrEmpty(domCode))
                    {
                        if (!scoresByDomain.ContainsKey(domCode))
                            scoresByDomain[domCode] = new List<double>();
                        scoresByDomain[domCode].Add(p.MasteryScore);
                    }
                }

                double langScore = scoresByDomain.TryGetValue(StudentKMeansClusterer.CodeLang, out var lList) && lList.Count > 0
                    ? lList.Average() : 0.50;
                double mathScore = scoresByDomain.TryGetValue(StudentKMeansClusterer.CodeMath, out var mList) && mList.Count > 0
                    ? mList.Average() : 0.50;
                double natSciScore = scoresByDomain.TryGetValue(StudentKMeansClusterer.CodeNatSci, out var nList) && nList.Count > 0
                    ? nList.Average() : 0.50;
                double socSciScore = scoresByDomain.TryGetValue(StudentKMeansClusterer.CodeSocSci, out var sList) && sList.Count > 0
                    ? sList.Average() : 0.50;

                studentVectors.Add(new StudentFeatureVector(
                    studentId,
                    new[] { Math.Round(langScore, 4), Math.Round(mathScore, 4), Math.Round(natSciScore, 4), Math.Round(socSciScore, 4) }
                ));
            }
            else
            {
                studentVectors.Add(new StudentFeatureVector(studentId, new[] { 0.50, 0.50, 0.50, 0.50 }));
            }
        }

        // 5. Chạy thuật toán Elbow Method và K-Means++ để phân cụm tối ưu
        var (optimalK, elbowData, clusters) = _studentKMeansClusterer.FindOptimalKAndCluster(
            studentVectors,
            minK: 2,
            maxK: request.MaxK
        );

        _logger.LogInformation("Phân cụm K-Means hoàn tất: OptimalK={OptimalK} cho {Total} học sinh",
            optimalK, studentVectors.Count);

        // 6. Tạo các bản ghi Class (ClassType = 1: Lớp Chuyên Đề) và ghi danh học sinh
        var clusterItems = new List<ThematicClusterItemDto>();
        foreach (var cluster in clusters)
        {
            var thematicClass = await _classEnrollmentRepository.CreateThematicClassWithEnrollmentsAsync(
                campusId: request.CampusId,
                name: cluster.SuggestedClassName,
                domainId: cluster.TargetDomainId,
                domainCode: cluster.TargetDomainCode,
                clusterIndex: cluster.ClusterIndex,
                studentIds: cluster.StudentIds,
                ct: cancellationToken
            );

            clusterItems.Add(new ThematicClusterItemDto(
                ClusterIndex: cluster.ClusterIndex,
                ClassId: thematicClass.ClassId,
                ClassName: thematicClass.Name,
                Centroid: cluster.Centroid,
                DominantWeakDomains: cluster.DominantWeakDomains,
                StudentCount: cluster.StudentIds.Count,
                StudentIds: cluster.StudentIds,
                DomainId: cluster.TargetDomainId,
                DomainCode: cluster.TargetDomainCode,
                Wcss: cluster.Wcss
            ));
        }

        // 7. Đóng gói phản hồi
        var elbowDtos = elbowData.Select(e => new ElbowPointDto(e.K, e.Wcss)).ToList();
        var response = new AutoClusterResponseDto(
            CampusId: request.CampusId,
            TotalStudents: studentVectors.Count,
            OptimalK: optimalK,
            ElbowData: elbowDtos,
            Clusters: clusterItems,
            Message: $"Tự động phân cụm K-Means thành công. Đã tạo {clusterItems.Count} lớp chuyên đề tối ưu theo Elbow Method."
        );

        return Result<AutoClusterResponseDto>.Success(response);
    }
}
