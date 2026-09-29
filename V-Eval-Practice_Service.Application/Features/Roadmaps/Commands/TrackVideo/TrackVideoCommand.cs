using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.TrackVideo;

/// <summary>
/// Command ghi nhận thời gian xem video bài giảng lý thuyết (Core Flow 2 - API 4)
/// </summary>
public record TrackVideoCommand(
    Guid NodeId,
    int WatchedDurationSeconds,
    int TotalDurationSeconds,
    Guid? StudentId = null
) : IRequest<Result<TrackVideoResponseDto>>;
