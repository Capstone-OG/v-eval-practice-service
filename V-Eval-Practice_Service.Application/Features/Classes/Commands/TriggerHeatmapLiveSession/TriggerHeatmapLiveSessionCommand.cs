using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.TriggerHeatmapLiveSession;

/// <summary>
/// Command 1-Click tạo buổi Live Q&A chuyên đề cứu trợ trực tiếp từ cảnh báo đỏ của Heatmap (Core Flow 5 - API 3)
/// </summary>
public record TriggerHeatmapLiveSessionCommand(
    Guid ClassId,
    Guid SkillId,
    string SessionTitle,
    DateTime ScheduledAt,
    int DurationMinutes,
    string? MeetingUrl
) : IRequest<Result<TriggerLiveSessionResponseDto>>;
