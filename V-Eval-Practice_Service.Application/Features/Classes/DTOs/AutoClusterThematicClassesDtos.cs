using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

/// <summary>
/// Yêu cầu tự động xếp lớp theo Ma trận 4 Miền x 3 Cấp bậc (Cơ bản / Tăng tốc / Nâng cao - tối thiểu 12 lớp)
/// dựa trên độ nắm vững kỹ năng từ bài kiểm tra đầu vào
/// </summary>
public record AutoClusterRequestDto
{
    public Guid CampusId { get; init; }
    public int MaxK { get; init; } = 12;
    public int MinCapacity { get; init; } = 5;
    public int MaxCapacity { get; init; } = 20;
    public bool DryRun { get; init; } = false;
    public Guid? ManagerCampusId { get; init; }
    /// <summary>Ngưỡng trên của cấp Cơ bản (mặc định 0.50 = 50%)</summary>
    public double FoundationThreshold { get; init; } = 0.50;
    /// <summary>Ngưỡng dưới của cấp Nâng cao (mặc định 0.75 = 75%)</summary>
    public double AdvancedThreshold { get; init; } = 0.75;
    /// <summary>Luôn khởi tạo & hiển thị đủ khung 12 lớp (kể cả ô lớp chưa có học sinh)</summary>
    public bool IncludeEmptyClasses { get; init; } = true;
    /// <summary>Bảo toàn 100% lớp của học sinh cũ, chỉ xếp bổ sung học sinh mới chưa có lớp ở từng miền</summary>
    public bool PreserveExistingEnrollments { get; init; } = true;
}

/// <summary>
/// Điểm dữ liệu đường cong Elbow (K và tổng phương sai WCSS)
/// </summary>
public record ElbowPointDto(
    int K,
    double Wcss
);

/// <summary>
/// Chi tiết lớp học chuyên đề theo Miền & Cấp bậc
/// </summary>
public record ThematicClusterItemDto(
    int ClusterIndex,
    Guid ClassId,
    string ClassName,
    double[] Centroid,
    IReadOnlyList<string> DominantWeakDomains,
    int StudentCount,
    IReadOnlyList<Guid> StudentIds,
    Guid? DomainId,
    string? DomainCode,
    double Wcss,
    string TierCode = "",
    string TierName = "",
    string DomainName = "",
    double AverageDomainMastery = 0.0,
    string ClassStatus = "READY",
    int NewStudentsAssignedCount = 0
);

/// <summary>
/// Kết quả phản hồi toàn diện sau khi tự động phân cụm và tạo lớp chuyên đề
/// </summary>
public record AutoClusterResponseDto(
    Guid CampusId,
    int TotalStudents,
    int OptimalK,
    IReadOnlyList<ElbowPointDto> ElbowData,
    IReadOnlyList<ThematicClusterItemDto> Clusters,
    string Message,
    bool IsDryRun = false,
    int ExcludedUndiagnosedCount = 0,
    int NewPlacementsCount = 0,
    bool PreservedExistingEnrollments = true
)
{
    public int TotalStudentsClustered => TotalStudents;
}
