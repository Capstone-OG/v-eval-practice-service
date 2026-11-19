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

        if (request.MinCapacity < 1)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.Validation("Cohort.InvalidMinCapacity",
                    "Sĩ số tối thiểu của một lớp không được nhỏ hơn 1 học sinh."));
        }

        if (request.MaxCapacity > 20 || request.MaxCapacity < request.MinCapacity)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.Validation("Cohort.InvalidMaxCapacity",
                    "Trần sĩ số tối đa của một lớp phải nằm trong khung từ sĩ số tối thiểu đến 20 học sinh."));
        }

        double foundationThreshold = request.FoundationThreshold is > 0.05 and < 0.90
            ? request.FoundationThreshold
            : 0.50;
        double advancedThreshold = request.AdvancedThreshold > foundationThreshold && request.AdvancedThreshold <= 0.95
            ? request.AdvancedThreshold
            : 0.75;

        _logger.LogInformation(
            "Bắt đầu xếp lớp phân tầng 4 Miền x 3 Cấp bậc cho cơ sở {CampusId} (Ngưỡng Cơ bản < {F:P0}, Nâng cao >= {A:P0}, DryRun={DryRun})",
            request.CampusId, foundationThreshold, advancedThreshold, request.DryRun);

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

        // 3. Nạp hồ sơ năng lực tiên nghiệm P(L0) từ bài kiểm tra đầu vào của học sinh tại cơ sở
        var profiles = await _learningProfileRepository.GetByStudentIdsAsync(enrolledStudentIds, cancellationToken);
        var profileGroups = profiles.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        // 4. Xây dựng vector năng lực 4 miền [DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI] cho học sinh đã hoàn thành Chẩn đoán (MGR_11)
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

        if (studentVectors.Count == 0)
        {
            return Result<AutoClusterResponseDto>.Failure(
                Error.UnprocessableEntity("Cohort.InsufficientStudents",
                    "Chưa có học sinh nào hoàn thành bài thi Chẩn đoán đầu vào 30 câu tại cơ sở này để xếp lớp."));
        }

        // 5. Phân tầng Ma trận 4 Miền x 3 Cấp bậc (Cơ bản / Tăng tốc / Nâng cao) — Tối thiểu 12 lớp chuẩn
        // Mỗi học sinh được phân hóa vào đúng 4 lớp (1 lớp/miền kiến thức) dựa trên độ nắm vững kỹ năng từng miền
        var domainSpecs = new (int FeatureIndex, string Code, Guid Id, string Name)[]
        {
            (0, StudentKMeansClusterer.CodeLang, StudentKMeansClusterer.DomainLanguageId, "Sử dụng Ngôn ngữ"),
            (1, StudentKMeansClusterer.CodeMath, StudentKMeansClusterer.DomainMathLogicId, "Toán & Tư duy Logic"),
            (2, StudentKMeansClusterer.CodeNatSci, StudentKMeansClusterer.DomainNaturalScienceId, "Khoa học Tự nhiên"),
            (3, StudentKMeansClusterer.CodeSocSci, StudentKMeansClusterer.DomainSocialScienceId, "Khoa học Xã hội")
        };

        var tierSpecs = new (string Code, string Name, Func<double, bool> Matches)[]
        {
            ("FOUNDATION", "Cơ bản", score => score < foundationThreshold),
            ("ACCELERATED", "Tăng tốc", score => score >= foundationThreshold && score < advancedThreshold),
            ("ADVANCED", "Nâng cao", score => score >= advancedThreshold)
        };

        var studentVectorMap = studentVectors.ToDictionary(s => s.StudentId, s => s.Features);
        var existingDomainClasses = request.PreserveExistingEnrollments
            ? await _classEnrollmentRepository.GetActiveDomainClassesWithStudentsAsync(request.CampusId, cancellationToken)
            : Array.Empty<(Guid ClassId, string ClassName, string DomainCode, List<Guid> StudentIds)>();

        var clusterItems = new List<ThematicClusterItemDto>();
        int nextSlotIndex = 0;
        double totalWcssAll = 0.0;
        int totalNewPlacements = 0;

        foreach (var domain in domainSpecs)
        {
            var domainExistingClasses = existingDomainClasses
                .Where(c => string.Equals(c.DomainCode, domain.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Tập học sinh CŨ đã có lớp ACTIVE ở miền này -> Giữ nguyên 100%, không làm xáo trộn
            var alreadyPlacedInDomain = request.PreserveExistingEnrollments
                ? domainExistingClasses.SelectMany(c => c.StudentIds).ToHashSet()
                : new HashSet<Guid>();

            foreach (var tier in tierSpecs)
            {
                string tierPrefix = $"{domain.Name} - {tier.Name}";
                var existingTierClasses = request.PreserveExistingEnrollments
                    ? domainExistingClasses
                        .Where(c => c.ClassName.StartsWith(tierPrefix, StringComparison.OrdinalIgnoreCase))
                        .ToList()
                    : new List<(Guid ClassId, string ClassName, string DomainCode, List<Guid> StudentIds)>();

                // Chỉ lọc những học sinh CHƯA có lớp ở miền này (khi bật PreserveExistingEnrollments)
                // hoặc toàn bộ học sinh (khi tắt để đánh giá lại toàn cơ sở)
                var candidateStudents = studentVectors
                    .Where(s => !alreadyPlacedInDomain.Contains(s.StudentId) && tier.Matches(s.Features[domain.FeatureIndex]))
                    .OrderBy(s => s.Features[domain.FeatureIndex])
                    .Select(s => s.StudentId)
                    .ToList();

                int cursor = 0;

                // Bước A: Bảo toàn các lớp đã tồn tại của (Miền, Cấp bậc) và đổ thêm HS mới vào chỗ còn trống (< 20 HS)
                foreach (var existingCls in existingTierClasses)
                {
                    var existingIds = existingCls.StudentIds.Distinct().ToList();
                    int freeSlots = Math.Max(0, request.MaxCapacity - existingIds.Count);
                    var addedNewIds = freeSlots > 0 && cursor < candidateStudents.Count
                        ? candidateStudents.Skip(cursor).Take(freeSlots).ToList()
                        : new List<Guid>();

                    cursor += addedNewIds.Count;
                    totalNewPlacements += addedNewIds.Count;

                    var combinedIds = existingIds.Concat(addedNewIds).ToList();

                    if (!request.DryRun && addedNewIds.Count > 0)
                    {
                        await _classEnrollmentRepository.CreateThematicClassWithEnrollmentsAsync(
                            campusId: request.CampusId,
                            name: existingCls.ClassName,
                            domainId: domain.Id,
                            domainCode: domain.Code,
                            clusterIndex: nextSlotIndex,
                            studentIds: combinedIds,
                            ct: cancellationToken
                        );
                    }

                    var knownVectorIds = combinedIds.Where(sid => studentVectorMap.ContainsKey(sid)).ToList();
                    double[] centroid = knownVectorIds.Count > 0
                        ? ComputeCentroid(knownVectorIds, studentVectorMap)
                        : new double[] { 0.0, 0.0, 0.0, 0.0 };
                    double avgDomainMastery = knownVectorIds.Count > 0
                        ? Math.Round(knownVectorIds.Average(sid => studentVectorMap[sid][domain.FeatureIndex]), 4)
                        : 0.0;
                    double domainWcss = knownVectorIds.Count > 0
                        ? Math.Round(knownVectorIds.Sum(sid =>
                        {
                            double diff = studentVectorMap[sid][domain.FeatureIndex] - avgDomainMastery;
                            return diff * diff;
                        }), 4)
                        : 0.0;
                    totalWcssAll += domainWcss;

                    string classStatus = combinedIds.Count == 0
                        ? "EMPTY"
                        : combinedIds.Count >= request.MinCapacity
                            ? "READY"
                            : "PENDING_MIN_CAPACITY";

                    clusterItems.Add(new ThematicClusterItemDto(
                        ClusterIndex: nextSlotIndex++,
                        ClassId: existingCls.ClassId,
                        ClassName: existingCls.ClassName,
                        Centroid: centroid,
                        DominantWeakDomains: tier.Code == "ADVANCED" ? Array.Empty<string>() : new[] { domain.Code },
                        StudentCount: combinedIds.Count,
                        StudentIds: combinedIds,
                        DomainId: domain.Id,
                        DomainCode: domain.Code,
                        Wcss: domainWcss,
                        TierCode: tier.Code,
                        TierName: tier.Name,
                        DomainName: domain.Name,
                        AverageDomainMastery: avgDomainMastery,
                        ClassStatus: classStatus,
                        NewStudentsAssignedCount: addedNewIds.Count
                    ));
                }

                // Bước B: Nếu vẫn còn HS mới (do lớp cũ đã đầy 20/20 hoặc chưa có lớp nào) -> tạo lớp mới
                var remainingNewStudents = cursor < candidateStudents.Count
                    ? candidateStudents.Skip(cursor).ToList()
                    : new List<Guid>();

                if (existingTierClasses.Count == 0 && remainingNewStudents.Count == 0)
                {
                    if (!request.IncludeEmptyClasses) continue;

                    string emptyClassName = $"{domain.Name} - {tier.Name} 01";
                    Guid createdEmptyClassId = Guid.Empty;

                    if (!request.DryRun)
                    {
                        var createdClass = await _classEnrollmentRepository.CreateThematicClassWithEnrollmentsAsync(
                            campusId: request.CampusId,
                            name: emptyClassName,
                            domainId: domain.Id,
                            domainCode: domain.Code,
                            clusterIndex: nextSlotIndex,
                            studentIds: Array.Empty<Guid>(),
                            ct: cancellationToken
                        );
                        createdEmptyClassId = createdClass.ClassId;
                        emptyClassName = createdClass.Name;
                    }

                    clusterItems.Add(new ThematicClusterItemDto(
                        ClusterIndex: nextSlotIndex++,
                        ClassId: createdEmptyClassId,
                        ClassName: emptyClassName,
                        Centroid: new double[] { 0.0, 0.0, 0.0, 0.0 },
                        DominantWeakDomains: tier.Code == "ADVANCED" ? Array.Empty<string>() : new[] { domain.Code },
                        StudentCount: 0,
                        StudentIds: Array.Empty<Guid>(),
                        DomainId: domain.Id,
                        DomainCode: domain.Code,
                        Wcss: 0.0,
                        TierCode: tier.Code,
                        TierName: tier.Name,
                        DomainName: domain.Name,
                        AverageDomainMastery: 0.0,
                        ClassStatus: "EMPTY",
                        NewStudentsAssignedCount: 0
                    ));
                    continue;
                }

                int nextCohortNo = existingTierClasses.Count + 1;
                for (int offset = 0; offset < remainingNewStudents.Count; offset += request.MaxCapacity)
                {
                    var slice = remainingNewStudents.Skip(offset).Take(request.MaxCapacity).ToList();
                    totalNewPlacements += slice.Count;

                    string className = $"{domain.Name} - {tier.Name} {nextCohortNo:D2}";
                    double[] centroid = ComputeCentroid(slice, studentVectorMap);
                    double avgDomainMastery = Math.Round(slice.Average(sid => studentVectorMap[sid][domain.FeatureIndex]), 4);
                    double domainWcss = Math.Round(slice.Sum(sid =>
                    {
                        double diff = studentVectorMap[sid][domain.FeatureIndex] - avgDomainMastery;
                        return diff * diff;
                    }), 4);
                    totalWcssAll += domainWcss;

                    string classStatus = slice.Count >= request.MinCapacity
                        ? "READY"
                        : "PENDING_MIN_CAPACITY";

                    Guid createdClassId = Guid.Empty;
                    string finalClassName = className;

                    if (!request.DryRun)
                    {
                        var thematicClass = await _classEnrollmentRepository.CreateThematicClassWithEnrollmentsAsync(
                            campusId: request.CampusId,
                            name: className,
                            domainId: domain.Id,
                            domainCode: domain.Code,
                            clusterIndex: nextSlotIndex,
                            studentIds: slice,
                            ct: cancellationToken
                        );
                        createdClassId = thematicClass.ClassId;
                        finalClassName = thematicClass.Name;
                    }

                    clusterItems.Add(new ThematicClusterItemDto(
                        ClusterIndex: nextSlotIndex++,
                        ClassId: createdClassId,
                        ClassName: finalClassName,
                        Centroid: centroid,
                        DominantWeakDomains: tier.Code == "ADVANCED" ? Array.Empty<string>() : new[] { domain.Code },
                        StudentCount: slice.Count,
                        StudentIds: slice,
                        DomainId: domain.Id,
                        DomainCode: domain.Code,
                        Wcss: domainWcss,
                        TierCode: tier.Code,
                        TierName: tier.Name,
                        DomainName: domain.Name,
                        AverageDomainMastery: avgDomainMastery,
                        ClassStatus: classStatus,
                        NewStudentsAssignedCount: slice.Count
                    ));

                    nextCohortNo++;
                }
            }
        }

        int populatedClassesCount = clusterItems.Count(c => c.StudentCount > 0);
        int readyClassesCount = clusterItems.Count(c => c.ClassStatus == "READY");

        _logger.LogInformation(
            "Xếp lớp phân tầng 4 Miền x 3 Bậc hoàn tất (PreserveExisting={Preserve}): Tổng {TotalClasses} lớp ({Populated} lớp có HS, {Ready} lớp đủ sĩ số >= {MinCap}), xếp mới {NewPlacements} lượt lớp",
            request.PreserveExistingEnrollments, clusterItems.Count, populatedClassesCount, readyClassesCount, request.MinCapacity, totalNewPlacements);

        // 6. Đóng gói phản hồi
        var elbowDtos = new List<ElbowPointDto>
        {
            new(clusterItems.Count, Math.Round(totalWcssAll, 4))
        };

        string excludedNote = excludedUndiagnosedCount > 0
            ? $" (Đã loại trừ {excludedUndiagnosedCount} HS chưa làm bài test đầu vào 30 câu)."
            : ".";

        string modeLabel = request.PreserveExistingEnrollments
            ? "Bảo toàn 100% HS cũ"
            : "Đánh giá & xếp lại toàn cơ sở";

        string statusMsg = request.DryRun
            ? $"[Mô phỏng • {modeLabel}] Ma trận {clusterItems.Count} lớp (4 Miền × 3 Cấp bậc): phát hiện {totalNewPlacements} lượt xếp lớp mới cần bổ sung ({populatedClassesCount} lớp đang có HS){excludedNote}"
            : $"[Đã Xuất Bản • {modeLabel}] Đã cập nhật ma trận {clusterItems.Count} lớp và xếp bổ sung thành công {totalNewPlacements} lượt lớp mới cho học sinh{excludedNote}";

        var response = new AutoClusterResponseDto(
            CampusId: request.CampusId,
            TotalStudents: studentVectors.Count,
            OptimalK: clusterItems.Count,
            ElbowData: elbowDtos,
            Clusters: clusterItems,
            Message: statusMsg,
            IsDryRun: request.DryRun,
            ExcludedUndiagnosedCount: excludedUndiagnosedCount,
            NewPlacementsCount: totalNewPlacements,
            PreservedExistingEnrollments: request.PreserveExistingEnrollments
        );

        return Result<AutoClusterResponseDto>.Success(response);
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
