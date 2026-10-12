using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.TrackVideo;

public class TrackVideoCommandHandler : IRequestHandler<TrackVideoCommand, Result<TrackVideoResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly ILogger<TrackVideoCommandHandler> _logger;

    public TrackVideoCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILogger<TrackVideoCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _logger = logger;
    }

    public async Task<Result<TrackVideoResponseDto>> Handle(TrackVideoCommand request, CancellationToken ct)
    {
        // 1. Kiểm tra tồn tại của StageProgress
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<TrackVideoResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        // 2. Xác thực quyền sở hữu
        if (progress.StudentId != request.Request.StudentId)
        {
            return Result<TrackVideoResponseDto>.Failure(
                Error.Validation("StageProgress.Forbidden", "Tiến trình này không thuộc về học sinh hiện tại."));
        }

        // 3. Tính toán phần trăm thời lượng đã xem (lũy tiến)
        var currentRatio = (double)request.Request.WatchedSeconds / request.Request.TotalSeconds * 100.0;
        var watchPercentage = Math.Min(100.00m, Math.Round((decimal)currentRatio, 2));

        // Không bao giờ giảm phần trăm nếu học sinh tua lại
        progress.VideoWatchPercentage = Math.Max(progress.VideoWatchPercentage, watchPercentage);
        progress.UpdatedAt = DateTime.UtcNow;

        bool isCompletedLearn = progress.VideoWatchPercentage >= 80.00m;

        // 4. Máy trạng thái: Nếu đang ở LEARN và đã xem >= 80% -> Tự động chuyển sang APPLY
        if (isCompletedLearn && progress.CurrentStep == "LEARN")
        {
            progress.CurrentStep = "APPLY";

            // Đồng bộ trạng thái hoàn thành học liệu lý thuyết sang RoadmapNode
            if (progress.RoadmapNode != null)
            {
                progress.RoadmapNode.VideoWatchedSeconds = request.Request.WatchedSeconds;
                progress.RoadmapNode.VideoTotalSeconds = request.Request.TotalSeconds;
                progress.RoadmapNode.IsVideoCompleted = true;
            }

            _logger.LogInformation("Học sinh {StudentId} đã hoàn thành xem video chặng {RoadmapNodeId} ({Percentage}%), chuyển sang bước APPLY.",
                progress.StudentId, progress.RoadmapNodeId, progress.VideoWatchPercentage);
        }

        await _stageProgressRepository.UpdateAsync(progress, ct);

        string nextAction = isCompletedLearn ? "START_ADAPTIVE_PRACTICE" : "CONTINUE_WATCHING";
        string message = isCompletedLearn
            ? $"Tuyệt vời! Bạn đã hoàn thành {progress.VideoWatchPercentage}% bài giảng lý thuyết. Bước APPLY (Luyện tập thích ứng IRT/BKT) đã sẵn sàng!"
            : $"Đã ghi nhận tiến độ: {progress.VideoWatchPercentage}%. Hãy tiếp tục xem để đạt tối thiểu 80% thời lượng.";

        var response = new TrackVideoResponseDto(
            progress.Id,
            progress.CurrentStep,
            progress.VideoWatchPercentage,
            isCompletedLearn,
            nextAction,
            message
        );

        return Result<TrackVideoResponseDto>.Success(response);
    }
}
