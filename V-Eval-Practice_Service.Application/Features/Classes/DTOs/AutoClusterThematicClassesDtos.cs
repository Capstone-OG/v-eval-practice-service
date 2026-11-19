using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

/// <summary>
/// Yêu cầu tự động phân cụm học sinh theo lỗ hổng kiến thức K-Means và tạo các Lớp Chuyên Đề
/// </summary>
public record AutoClusterRequestDto
{
    public Guid CampusId { get; init; }
    public int MaxK { get; init; } = 8;
    public int MinCapacity { get; init; } = 5;
    public int MaxCapacity { get; init; } = 20;
    public bool DryRun { get; init; } = false;
    public Guid? ManagerCampusId { get; init; }
}

/// <summary>
/// Điểm dữ liệu đường cong Elbow (K và tổng phương sai WCSS)
/// </summary>
public record ElbowPointDto(
    int K,
    double Wcss
);

/// <summary>
/// Chi tiết lớp học chuyên đề sinh ra từ phân cụm K-Means
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
    double Wcss
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
    int ExcludedUndiagnosedCount = 0
)
{
    public int TotalStudentsClustered => TotalStudents;
}
