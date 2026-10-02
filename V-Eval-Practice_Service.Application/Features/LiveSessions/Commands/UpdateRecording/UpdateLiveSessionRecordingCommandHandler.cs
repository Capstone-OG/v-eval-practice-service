using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.UpdateRecording;

public class UpdateLiveSessionRecordingCommandHandler : IRequestHandler<UpdateLiveSessionRecordingCommand, Result<UpdateLiveSessionRecordingResponseDto>>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<UpdateLiveSessionRecordingCommandHandler> _logger;

    public UpdateLiveSessionRecordingCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        ILogger<UpdateLiveSessionRecordingCommandHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<UpdateLiveSessionRecordingResponseDto>> Handle(UpdateLiveSessionRecordingCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Updating recording URL for Session {SessionId} by {UpdatedBy}", request.SessionId, request.UpdatedBy);

        var session = await _liveSessionRepository.GetByIdAsync(request.SessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("Live Session {SessionId} not found.", request.SessionId);
            return Result<UpdateLiveSessionRecordingResponseDto>.Failure(
                Error.NotFound("LiveSessionNotFound", $"Không tìm thấy buổi học Live Q&A có ID {request.SessionId}."));
        }

        if (string.Equals(session.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Cannot update recording URL for cancelled session {SessionId}.", request.SessionId);
            return Result<UpdateLiveSessionRecordingResponseDto>.Failure(
                Error.Validation("SessionCancelled", $"Buổi học Live Q&A {request.SessionId} đã bị hủy bỏ, không thể cập nhật video ghi hình."));
        }

        session.RecordingUrl = request.RecordingUrl.Trim();
        session.IsRecorded = true;
        if (session.Status == "SCHEDULED")
        {
            session.Status = "COMPLETED";
        }

        await _liveSessionRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Recording URL updated successfully for Session {SessionId}: {RecordingUrl}",
            session.SessionId, session.RecordingUrl);

        var response = new UpdateLiveSessionRecordingResponseDto
        {
            SessionId = session.SessionId,
            Title = session.Title,
            RecordingUrl = session.RecordingUrl,
            IsRecorded = session.IsRecorded,
            Status = session.Status,
            UpdatedAt = DateTime.UtcNow,
            Message = "Cập nhật đường dẫn video ghi hình buổi học trực tuyến thành công."
        };

        return Result<UpdateLiveSessionRecordingResponseDto>.Success(response);
    }
}
