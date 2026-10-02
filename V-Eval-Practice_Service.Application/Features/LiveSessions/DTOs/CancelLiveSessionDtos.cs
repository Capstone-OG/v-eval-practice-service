using System;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

public class CancelLiveSessionRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class CancelLiveSessionResponseDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "CANCELLED";
    public string? Reason { get; set; }
    public DateTime CancelledAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
