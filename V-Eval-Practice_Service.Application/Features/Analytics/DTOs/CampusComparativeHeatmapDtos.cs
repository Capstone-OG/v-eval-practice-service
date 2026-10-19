using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

public record DomainAverageDto(
    string DomainCode,
    string DomainName,
    double AverageMastery
);

public record ClassDomainMasteryDto(
    string DomainCode,
    double AveragePlt,
    double StrugglingRate
);

public record ClassComparisonDto(
    Guid ClassId,
    string ClassName,
    string TeacherName,
    int TotalStudents,
    List<ClassDomainMasteryDto> DomainMasteries,
    string OverallRiskLevel
);

public record CampusComparativeHeatmapResponseDto(
    Guid CampusId,
    string CampusName,
    int TotalClasses,
    DateTime LastUpdated,
    List<DomainAverageDto> DomainAverages,
    List<ClassComparisonDto> ClassComparisons
);
