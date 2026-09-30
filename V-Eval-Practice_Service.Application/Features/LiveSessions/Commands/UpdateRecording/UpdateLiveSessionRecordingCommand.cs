using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.UpdateRecording;

public record UpdateLiveSessionRecordingCommand(
    Guid SessionId,
    string RecordingUrl,
    Guid? UpdatedBy
) : IRequest<Result<UpdateLiveSessionRecordingResponseDto>>;
