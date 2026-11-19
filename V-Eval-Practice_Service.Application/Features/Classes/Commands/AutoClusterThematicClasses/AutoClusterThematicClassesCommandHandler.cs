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
        if (request.ManagerCampusId.HasValue &&
            request.ManagerCampusId.Value != Guid.Empty &&
            request.ManagerCampusId.Value != request.CampusId)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.Forbidden("Campus.AccessDenied",
                    "403 Access Denied - Bạn chỉ có quyền quản lý cơ sở được phân công."));
        }

        if (request.MinCapacity < 5)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.Validation("Cohort.InvalidMinCapacity",
                    "Sĩ số tối thiểu của một lớp chuyên đề không được nhỏ hơn 5 học sinh."));
        }

        if (request.MaxCapacity > 20 || request.MaxCapacity < request.MinCapacity)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.Validation("Cohort.InvalidMaxCapacity",
                    "Trần sĩ số tối đa của một lớp chuyên đề phải nằm trong khung từ 5 đến 20 học sinh."));
        }

        if (request.MaxK < 2 || request.MaxK > 10)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.Validation("Cohort.InvalidMaxK",
                    "Số lượng cụm K phải nằm trong khoảng từ 2 đến 10."));
        }

        _logger.LogInformation(
            "Bắt đầu tự động phân cụm K-Means cho cơ sở {CampusId} với MaxK={MaxK}, DryRun={DryRun}",
            request.CampusId, request.MaxK, request.DryRun);

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

        // 4. Xây dựng vector đặc trưng 4 chiều [DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI] cho học sinh đã hoàn thành Chẩn đoán (MGR_11)
        var studentVectors = new List<StudentFeatureVector>();
        int excludedUndiagnosedCount = 0;

        foreach (var studentId in enrolledStudentIds)
        {
            if (profileGroups.TryGetValue(studentId, out var studentProfiles) && studentProfiles.Count > 0)
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
                excludedUndiagnosedCount++;
            }
        }

        // Kiểm tra ngưỡng sĩ số tối thiểu [5..20] theo BR-16 / BR-22 (UT018 & MGR_11, MGR_12)
        if (studentVectors.Count < request.MinCapacity)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.UnprocessableEntity("Cohort.InsufficientStudents",
                    $"Số lượng học sinh đủ điều kiện đã hoàn thành bài thi Chẩn đoán ({studentVectors.Count} học sinh) nhỏ hơn ngưỡng tối thiểu để mở lớp ({request.MinCapacity} học sinh)."));
        }

        // 5. Chạy thuật toán Elbow Method và K-Means++ để phân cụm tối ưu
        // Ràng buộc quy mô tối thiểu: Mỗi lớp chuyên đề phải có tối thiểu request.MinCapacity (5) học sinh
        int maxFeasibleK = studentVectors.Count / request.MinCapacity;
        int effectiveMaxK = Math.Min(request.MaxK, maxFeasibleK);

        int optimalK;
        List<ElbowDataPoint> elbowData;
        List<ClusterResult> rawClusters;

        if (effectiveMaxK <= 1)
        {
            // Sĩ số không đủ để chia >= 2 lớp (mỗi lớp tối thiểu 5 HS) -> gom 1 lớp duy nhất
            optimalK = 1;
            rawClusters = _studentKMeansClusterer.Cluster(studentVectors, 1);
            elbowData = new List<ElbowDataPoint> { new(1, rawClusters.Sum(c => c.Wcss)) };
        }
        else
        {
            (optimalK, elbowData, rawClusters) = _studentKMeansClusterer.FindOptimalKAndCluster(
                studentVectors,
                minK: 2,
                maxK: effectiveMaxK
            );

            // Post-clustering constraint: đảm bảo mọi cụm đều có tối thiểu MinCapacity học sinh
            // Nếu có cụm < MinCapacity (ví dụ cụm lẻ 1-4 HS do phân bố điểm cực đoan), gộp vào cụm có tâm gần nhất
            rawClusters = RebalanceUndersizedClusters(rawClusters, studentVectors, request.MinCapacity);
            optimalK = rawClusters.Count;
        }

        // Đảm bảo trần sĩ số mỗi lớp không vượt quá MaxCapacity (20 học sinh/lớp)
        var boundedClusters = new List<ClusterResult>();
        int nextClusterIdx = 0;
        foreach (var c in rawClusters)
        {
            if (c.StudentIds.Count <= request.MaxCapacity)
            {
                boundedClusters.Add(c with { ClusterIndex = nextClusterIdx++ });
            }
            else
            {
                int cohortNo = 1;
                for (int offset = 0; offset < c.StudentIds.Count; offset += request.MaxCapacity)
                {
                    var slice = c.StudentIds.Skip(offset).Take(request.MaxCapacity).ToList();
                    boundedClusters.Add(c with
                    {
                        ClusterIndex = nextClusterIdx++,
                        SuggestedClassName = $"{c.SuggestedClassName} - Nhóm lớp {cohortNo:D2}",
                        StudentIds = slice
                    });
                    cohortNo++;
                }
            }
        }

        _logger.LogInformation(
            "Phân cụm K-Means hoàn tất: OptimalK={OptimalK}, BoundedClusters={BoundedCount} cho {Total} học sinh (Loại trừ {Excluded} chưa thi chẩn đoán)",
            optimalK, boundedClusters.Count, studentVectors.Count, excludedUndiagnosedCount);

        // 6. Nếu DryRun = true (MGR_15), chỉ trả về đề xuất mô phỏng; nếu DryRun = false (MGR_10), tạo lớp chính thức
        var clusterItems = new List<ThematicClusterItemDto>();
        foreach (var cluster in boundedClusters)
        {
            Guid createdClassId = Guid.Empty;
            string finalClassName = cluster.SuggestedClassName;

            if (!request.DryRun)
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
                createdClassId = thematicClass.ClassId;
                finalClassName = thematicClass.Name;
            }

            clusterItems.Add(new ThematicClusterItemDto(
                ClusterIndex: cluster.ClusterIndex,
                ClassId: createdClassId,
                ClassName: finalClassName,
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
        string excludedNote = excludedUndiagnosedCount > 0
            ? $" ({excludedUndiagnosedCount} học sinh chưa hoàn thành bài thi Chẩn đoán đầu vào nên chưa được xếp cụm)."
            : ".";

        string statusMsg = request.DryRun
            ? $"[Chế độ Mô phỏng] Đã đề xuất {clusterItems.Count} lớp chuyên đề cho {studentVectors.Count} học sinh{excludedNote}"
            : $"Đã phê duyệt và xuất bản {clusterItems.Count} lớp chuyên đề chính thức cho {studentVectors.Count} học sinh{excludedNote}";

        var response = new AutoClusterResponseDto(
            CampusId: request.CampusId,
            TotalStudents: studentVectors.Count,
            OptimalK: clusterItems.Count,
            ElbowData: elbowDtos,
            Clusters: clusterItems,
            Message: statusMsg,
            IsDryRun: request.DryRun,
            ExcludedUndiagnosedCount: excludedUndiagnosedCount
        );

        return Result<AutoClusterResponseDto>.Success(response);
    }

    /// <summary>
    /// Tự động tái phân bổ các cụm bị thiếu sĩ số (< minCapacity, thường là 5 HS)
    /// vào các cụm hợp lệ có tâm gần nhất theo chuẩn BR-16 / UT018.
    /// </summary>
    private static List<ClusterResult> RebalanceUndersizedClusters(
        List<ClusterResult> rawClusters,
        List<StudentFeatureVector> studentVectors,
        int minCapacity)
    {
        if (rawClusters.Count <= 1) return rawClusters;

        var studentVectorMap = studentVectors.ToDictionary(s => s.StudentId, s => s.Features);
        var validClusters = rawClusters.Where(c => c.StudentIds.Count >= minCapacity).ToList();
        var undersizedClusters = rawClusters.Where(c => c.StudentIds.Count < minCapacity).ToList();

        if (undersizedClusters.Count == 0) return rawClusters;

        // Nếu không có cụm nào đạt minCapacity (do chia nhỏ), gom toàn bộ thành 1 cụm
        if (validClusters.Count == 0)
        {
            var allStudentIds = rawClusters.SelectMany(c => c.StudentIds).Distinct().ToList();
            double[] singleCentroid = ComputeCentroid(allStudentIds, studentVectorMap);
            var (weakDomains, className, domainId, domainCode) = StudentKMeansClusterer.AnalyzeCentroid(singleCentroid);
            double singleWcss = allStudentIds.Sum(sid => StudentKMeansClusterer.DistanceSquared(studentVectorMap[sid], singleCentroid));

            return new List<ClusterResult>
            {
                new ClusterResult(
                    ClusterIndex: 0,
                    Centroid: singleCentroid,
                    DominantWeakDomains: weakDomains,
                    SuggestedClassName: className,
                    StudentIds: allStudentIds,
                    TargetDomainId: domainId,
                    TargetDomainCode: domainCode,
                    Wcss: Math.Round(singleWcss, 4)
                )
            };
        }

        // Tái phân bổ học sinh từ cụm thiếu sĩ số (< minCapacity) vào cụm hợp lệ gần nhất
        foreach (var under in undersizedClusters)
        {
            foreach (var studentId in under.StudentIds)
            {
                var studentFeatures = studentVectorMap[studentId];
                var nearestValid = validClusters
                    .OrderBy(vc => StudentKMeansClusterer.DistanceSquared(studentFeatures, vc.Centroid))
                    .First();

                nearestValid.StudentIds.Add(studentId);
            }
        }

        // Cập nhật lại Centroid, WCSS và tên lớp cho các cụm hợp lệ sau khi sáp nhập
        var updatedClusters = new List<ClusterResult>();
        int idx = 0;
        foreach (var vc in validClusters)
        {
            double[] newCentroid = ComputeCentroid(vc.StudentIds, studentVectorMap);
            var (weakDomains, className, domainId, domainCode) = StudentKMeansClusterer.AnalyzeCentroid(newCentroid);
            double newWcss = vc.StudentIds.Sum(sid => StudentKMeansClusterer.DistanceSquared(studentVectorMap[sid], newCentroid));

            updatedClusters.Add(new ClusterResult(
                ClusterIndex: idx++,
                Centroid: newCentroid,
                DominantWeakDomains: weakDomains,
                SuggestedClassName: className,
                StudentIds: vc.StudentIds,
                TargetDomainId: domainId,
                TargetDomainCode: domainCode,
                Wcss: Math.Round(newWcss, 4)
            ));
        }

        return updatedClusters;
    }

    private static double[] ComputeCentroid(List<Guid> studentIds, Dictionary<Guid, double[]> studentVectorMap)
    {
        if (studentIds.Count == 0) return new double[4];
        int dimensions = studentVectorMap[studentIds[0]].Length;
        double[] centroid = new double[dimensions];

        foreach (var sid in studentIds)
        {
            var features = studentVectorMap[sid];
            for (int d = 0; d < dimensions; d++)
            {
                centroid[d] += features[d];
            }
        }

        for (int d = 0; d < dimensions; d++)
        {
            centroid[d] = Math.Round(centroid[d] / studentIds.Count, 4);
        }

        return centroid;
    }
}
