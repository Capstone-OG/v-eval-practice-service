using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.TrackVideo;

/// <summary>
/// Handler ghi nhận thời gian xem video bài giảng lý thuyết của chặng học (Core Flow 2 - API 4)
/// </summary>
public class TrackVideoCommandHandler : IRequestHandler<TrackVideoCommand, Result<TrackVideoResponseDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly ILogger<TrackVideoCommandHandler> _logger;

    public TrackVideoCommandHandler(
        ILearningRoadmapRepository roadmapRepository,
        ILogger<TrackVideoCommandHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _logger = logger;
    }

    public async Task<Result<TrackVideoResponseDto>> Handle(
        TrackVideoCommand request,
        CancellationToken ct)
    {
        _logger.LogInformation("Ghi nhận tiến độ xem video bài giảng cho chặng học {NodeId}: {WatchedSeconds}/{TotalSeconds}s",
            request.NodeId, request.WatchedDurationSeconds, request.TotalDurationSeconds);

        var node = await _roadmapRepository.GetNodeByIdAsync(request.NodeId, ct);

        if (node == null)
        {
            return Result<TrackVideoResponseDto>.Failure(
                Error.NotFound("RoadmapNode.NotFound", $"Không tìm thấy chặng học {request.NodeId}."));
        }

        // Kiểm tra bảo mật quyền sở hữu học sinh
        if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty && node.Roadmap.StudentId != request.StudentId.Value)
        {
            return Result<TrackVideoResponseDto>.Failure(
                Error.Forbidden("RoadmapNode.Forbidden", "Bạn không có quyền cập nhật tiến độ học của học sinh khác."));
        }

        // Kiểm tra trạng thái máy chặng học (State Machine)
        if (node.Status == "LOCKED")
        {
            return Result<TrackVideoResponseDto>.Failure(
                Error.Validation("RoadmapNode.Locked",
                    "Chặng học này đang bị khóa (LOCKED). Bạn cần hoàn thành bài kiểm tra của các chặng trước để mở khóa."));
        }

        if (node.Status == "SKIPPED_PRUNED")
        {
            return Result<TrackVideoResponseDto>.Failure(
                Error.Validation("RoadmapNode.Pruned",
                    "Chặng học này đã được hệ thống cắt tỉa tối ưu (SKIPPED_PRUNED), không yêu cầu học lý thuyết."));
        }

        // Cập nhật tiến độ xem lũy tiến (lấy giá trị thời gian xem cao nhất)
        int updatedWatched = Math.Max(node.VideoWatchedSeconds, request.WatchedDurationSeconds);
        node.VideoWatchedSeconds = updatedWatched;
        node.VideoTotalSeconds = request.TotalDurationSeconds;

        double watchPercentage = request.TotalDurationSeconds > 0
            ? Math.Min(100.0, Math.Round((double)updatedWatched / request.TotalDurationSeconds * 100.0, 2))
            : 0.0;

        // Quy tắc mở khóa bài Quiz củng cố: Yêu cầu xem >= 80% thời lượng bài giảng lý thuyết
        bool isEligible = watchPercentage >= 80.0;
        if (isEligible && !node.IsVideoCompleted)
        {
            node.IsVideoCompleted = true;
        }

        await _roadmapRepository.SaveChangesAsync(ct);

        string message = isEligible
            ? "Bạn đã xem đủ thời lượng video bài giảng (>= 80%). Bạn đã đủ điều kiện thực hiện bài Quiz củng cố!"
            : $"Bạn đã xem {watchPercentage}% thời lượng video. Vui lòng theo dõi tối thiểu 80% thời lượng bài giảng để đủ điều kiện làm bài Quiz củng cố.";

        var responseDto = new TrackVideoResponseDto(
            NodeId: node.NodeId,
            WatchedDurationSeconds: node.VideoWatchedSeconds,
            TotalDurationSeconds: node.VideoTotalSeconds,
            WatchPercentage: watchPercentage,
            IsQuizEligible: isEligible,
            Status: node.Status,
            Message: message
        );

        return Result<TrackVideoResponseDto>.Success(responseDto);
    }
}
