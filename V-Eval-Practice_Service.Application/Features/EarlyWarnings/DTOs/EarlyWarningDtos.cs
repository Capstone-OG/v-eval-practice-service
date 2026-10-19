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
