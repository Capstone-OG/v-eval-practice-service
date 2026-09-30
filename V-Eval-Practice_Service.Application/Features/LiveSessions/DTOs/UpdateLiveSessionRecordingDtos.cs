using System;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

public class UpdateLiveSessionRecordingRequestDto
{
    public string RecordingUrl { get; set; } = string.Empty;
}

public class UpdateLiveSessionRecordingResponseDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RecordingUrl { get; set; } = string.Empty;
    public bool IsRecorded { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
