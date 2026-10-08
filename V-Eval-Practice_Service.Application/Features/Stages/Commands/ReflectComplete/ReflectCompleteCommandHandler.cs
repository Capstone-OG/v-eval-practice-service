using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.ReflectComplete;

public class ReflectCompleteCommandHandler : IRequestHandler<ReflectCompleteCommand, Result<ReflectCompleteResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly ILogger<ReflectCompleteCommandHandler> _logger;

    public ReflectCompleteCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILearningRoadmapRepository roadmapRepository,
        ILogger<ReflectCompleteCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _roadmapRepository = roadmapRepository;
        _logger = logger;
    }

    public async Task<Result<ReflectCompleteResponseDto>> Handle(ReflectCompleteCommand request, CancellationToken ct)
    {
        // 1. Kiểm tra tồn tại StageProgress
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<ReflectCompleteResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        // 2. Xác thực quyền sở hữu
        if (progress.StudentId != request.Request.StudentId)
        {
            return Result<ReflectCompleteResponseDto>.Failure(
                Error.Validation("StageProgress.Forbidden", "Tiến trình này không thuộc về học sinh hiện tại."));
        }

        // 3. Cho phép phản tư nếu chặng đang ở REFLECT hoặc đã đạt độ thành thạo mục tiêu
        if (progress.CurrentStep != "REFLECT" && progress.BktMasteryPlt < 0.80)
        {
            return Result<ReflectCompleteResponseDto>.Failure(
                Error.Validation("StageProgress.NotEligibleForReflect",
                    $"Chặng học hiện đang ở bước '{progress.CurrentStep}' (độ thành thạo P(Lt)={progress.BktMasteryPlt:F2}). Bạn cần hoàn thành phần luyện tập thích ứng trước khi phản tư kết thúc."));
        }

        // 4. Đánh dấu hoàn thành toàn diện chặng học
        progress.CurrentStep = "REFLECT";
        progress.Status = "COMPLETED";
        progress.UpdatedAt = DateTime.UtcNow;

        await _stageProgressRepository.UpdateAsync(progress, ct);

        // 5. Đồng bộ hoàn thành và Mở khóa chặng học kế tiếp trên Lộ trình (Roadmap)
        Guid? nextUnlockedNodeId = null;

        if (progress.RoadmapNode != null)
        {
            progress.RoadmapNode.Status = "COMPLETED";
            progress.RoadmapNode.CompletedAt = DateTime.UtcNow;
            progress.RoadmapNode.QuizScore = progress.BktMasteryPlt * 10.0;
            progress.RoadmapNode.IsQuizPassed = true;

            // Tìm chặng học tiếp theo đang bị khóa (LOCKED)
            var nextNode = await _roadmapRepository.GetNextLockedNodeAsync(
                progress.RoadmapNode.RoadmapId,
                progress.RoadmapNode.StepOrder,
                ct);

            if (nextNode != null)
            {
                nextNode.Status = "IN_PROGRESS";
                nextNode.UnlockedAt = DateTime.UtcNow;
                nextUnlockedNodeId = nextNode.NodeId;

                _logger.LogInformation("Đã tự động mở khóa chặng học tiếp theo {NextNodeId} (Thứ tự {StepOrder}) trên lộ trình {RoadmapId}.",
                    nextNode.NodeId, nextNode.StepOrder, progress.RoadmapNode.RoadmapId);
            }

            await _roadmapRepository.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Học sinh {StudentId} đã hoàn thành xuất sắc toàn bộ chu trình P-L-A-R cho chặng {RoadmapNodeId} với đánh giá {Rating} sao.",
            progress.StudentId, progress.RoadmapNodeId, request.Request.ConfidenceRating);

        string message = nextUnlockedNodeId.HasValue
            ? $"Chúc mừng bạn đã hoàn thành chặng học! Chặng tiếp theo ({nextUnlockedNodeId}) đã được tự động mở khóa trên lộ trình."
            : "Chúc mừng bạn đã hoàn thành toàn bộ lộ trình học tập thích ứng!";

        var response = new ReflectCompleteResponseDto(
            progress.Id,
            progress.RoadmapNodeId,
            progress.Status,
            request.Request.ConfidenceRating,
            progress.BktMasteryPlt,
            nextUnlockedNodeId,
            message
        );

        return Result<ReflectCompleteResponseDto>.Success(response);
    }
}
