using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CancelLiveSession;

public class CancelLiveSessionCommandHandler : IRequestHandler<CancelLiveSessionCommand, Result<CancelLiveSessionResponseDto>>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<CancelLiveSessionCommandHandler> _logger;

    public CancelLiveSessionCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        ILogger<CancelLiveSessionCommandHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<CancelLiveSessionResponseDto>> Handle(CancelLiveSessionCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Processing cancellation for Live Session {SessionId} by {TeacherId}. Reason: {Reason}",
            request.SessionId, request.TeacherId, request.Reason);

        var session = await _liveSessionRepository.GetByIdAsync(request.SessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("Live Session {SessionId} not found.", request.SessionId);
            return Result<CancelLiveSessionResponseDto>.Failure(
                Error.NotFound("LiveSessionNotFound", $"Không tìm thấy buổi học Live Q&A có ID {request.SessionId}."));
        }

        if (string.Equals(session.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Cannot cancel completed session {SessionId}.", request.SessionId);
            return Result<CancelLiveSessionResponseDto>.Failure(
                Error.Validation("SessionAlreadyCompleted", "Buổi học này đã hoàn thành, không thể thực hiện hủy bỏ."));
        }

        if (string.Equals(session.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Session {SessionId} is already cancelled.", request.SessionId);
            return Result<CancelLiveSessionResponseDto>.Failure(
                Error.Validation("SessionAlreadyCancelled", "Buổi học này đã được hủy trước đó."));
        }

        // Cập nhật trạng thái thành CANCELLED (không xóa vật lý theo phân quyền giáo viên)
        session.Status = "CANCELLED";
        var cancelTag = $"[ĐÃ HỦY: {request.Reason.Trim()}]";
        session.Description = string.IsNullOrWhiteSpace(session.Description)
            ? cancelTag
            : $"{cancelTag} {session.Description}";

        await _liveSessionRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Live Session {SessionId} successfully marked as CANCELLED.", session.SessionId);

        var response = new CancelLiveSessionResponseDto
        {
            SessionId = session.SessionId,
            Title = session.Title,
            Status = session.Status,
            Reason = request.Reason.Trim(),
            CancelledAt = DateTime.UtcNow,
            Message = $"Hủy buổi học thành công. Trạng thái buổi học đã chuyển sang 'CANCELLED'."
        };

        return Result<CancelLiveSessionResponseDto>.Success(response);
    }
}
