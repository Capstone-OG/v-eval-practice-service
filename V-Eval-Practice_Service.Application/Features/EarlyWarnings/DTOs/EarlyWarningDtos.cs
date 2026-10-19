using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

public class SystemAlertDto
{
    public Guid AlertId { get; set; }
    public Guid StudentId { get; set; }
    public string AlertType { get; set; } = string.Empty; // GREEN, YELLOW, RED, UNREALISTIC_GOAL
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public Guid? HandledBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MyAlertsResponseDto
{
    public Guid StudentId { get; set; }
    public int TotalAlerts { get; set; }
    public int UnreadCount { get; set; }
    public int RedAlertsCount { get; set; }
    public int YellowAlertsCount { get; set; }
    public List<SystemAlertDto> Alerts { get; set; } = new();
}

public class MarkAlertAsReadResponseDto
{
    public Guid AlertId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AlertDashboardItemDto
{
    public Guid AlertId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty; // RED, YELLOW, UNREALISTIC_GOAL
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public int? CurrentPredictedScore { get; set; }
    public int? TargetScore { get; set; }
    public int? DeficitScore { get; set; }
    public Guid? HandledBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EarlyWarningsDashboardResponseDto
{
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int TotalAlerts { get; set; }
    public int ActiveAlertsCount { get; set; }
    public int RedAlertsCount { get; set; }
    public int YellowAlertsCount { get; set; }
    public int UnrealisticGoalCount { get; set; }
    public int ResolvedAlertsCount { get; set; }
    public List<AlertDashboardItemDto> Alerts { get; set; } = new();
}

public class ResolveAlertRequestDto
{
    [System.Text.Json.Serialization.JsonPropertyName("mentor_id")]
    public Guid? MentorId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("resolution_notes")]
    public string ResolutionNotes { get; set; } = string.Empty;
}

public class ResolveAlertResponseDto
{
    public Guid AlertId { get; set; }
    public Guid StudentId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public Guid? HandledBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}



