using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

public record PedagogicalAlertDto(
    Guid SkillId,
    string SkillName,
    string DomainCode,
    double StrugglingRate,
    int StrugglingCount,
    double AveragePlt,
    string AlertLevel,
    string SuggestedAction
);

public record HeatmapColumnDto(
    Guid SkillId,
    string SkillName,
    string DomainCode,
    double ClassAveragePlt,
    string AlertStatus,
    bool IsBimodal
);

public record HeatmapCellDto(
    Guid SkillId,
    double Plt,
    string MasteryLevel,
    DateTime LastUpdated
);

public record HeatmapRowDto(
    Guid StudentId,
    string StudentName,
    string? AvatarUrl,
    double ThetaScore,
    string RiskStatus,
    List<HeatmapCellDto> SkillCells
);

public record ClassHeatmapResponseDto(
    Guid ClassId,
    string ClassName,
    int TotalStudents,
    DateTime LastAggregatedAt,
    List<PedagogicalAlertDto> PedagogicalAlerts,
    List<HeatmapColumnDto> Columns,
    List<HeatmapRowDto> Rows
);

public record ForceAggregateResponseDto(
    Guid ClassId,
    DateTime AggregatedAt,
    int TotalSkillsCalculated,
    string Message
);
