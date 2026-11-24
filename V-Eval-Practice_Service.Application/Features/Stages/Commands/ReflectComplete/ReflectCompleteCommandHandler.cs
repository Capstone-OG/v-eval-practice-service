using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly ILogger<ReflectCompleteCommandHandler> _logger;

    public ReflectCompleteCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILearningRoadmapRepository roadmapRepository,
        ILearningProfileRepository learningProfileRepository,
        ILogger<ReflectCompleteCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _roadmapRepository = roadmapRepository;
        _learningProfileRepository = learningProfileRepository;
        _logger = logger;
    }

    public async Task<Result<ReflectCompleteResponseDto>> Handle(ReflectCompleteCommand request, CancellationToken ct)
    {
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<ReflectCompleteResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        if (progress.StudentId != request.Request.StudentId)
        {
            return Result<ReflectCompleteResponseDto>.Failure(
                Error.Validation("StageProgress.Forbidden", "Tiến trình này không thuộc về học sinh hiện tại."));
        }

        if (progress.CurrentStep != "REFLECT" && progress.BktMasteryPlt < 0.80)
        {
            return Result<ReflectCompleteResponseDto>.Failure(
                Error.Validation("StageProgress.NotEligibleForReflect",
                    $"Chặng học hiện đang ở bước '{progress.CurrentStep}' (độ thành thạo P(Lt)={progress.BktMasteryPlt * 100:F1}%). Bạn cần hoàn thành phần luyện tập thích ứng trước khi phản tư kết thúc."));
        }

        progress.CurrentStep = "REFLECT";
        progress.Status = "COMPLETED";
        progress.ConfidenceRating = Math.Clamp(request.Request.ConfidenceRating, 1, 5);
        progress.LearnedSummary = request.Request.LearnedSummary?.Trim();
        progress.MistakeNotes = request.Request.MistakeNotes?.Trim();
        progress.UpdatedAt = DateTime.UtcNow;

        await _stageProgressRepository.UpdateAsync(progress, ct);

        Guid? nextUnlockedNodeId = null;

        if (progress.RoadmapNode != null)
        {
            progress.RoadmapNode.Status = "COMPLETED";
            progress.RoadmapNode.CompletedAt = DateTime.UtcNow;
            progress.RoadmapNode.QuizScore = Math.Round(progress.BktMasteryPlt * 10.0, 2);
            progress.RoadmapNode.IsQuizPassed = true;

            // Đồng bộ CompletedMilestones thật vào bảng LearningRoadmaps để Dashboard hiển thị đúng tiến độ!
            if (progress.RoadmapNode.Roadmap != null)
            {
                var roadmap = progress.RoadmapNode.Roadmap;
                roadmap.CompletedMilestones = roadmap.Nodes.Count(n => n.Status == "COMPLETED");
                roadmap.UpdatedAt = DateTime.UtcNow;
            }

            // Mở khóa chặng học tiếp theo đang bị khóa (LOCKED)
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

            // Đồng bộ mastery cuối cùng sang LearningProfiles
            if (progress.RoadmapNode.SkillId != Guid.Empty)
            {
                try
                {
                    await _learningProfileRepository.UpsertSkillPriorsAsync(
                        progress.StudentId,
                        new List<(Guid SkillId, double MasteryScore)> { (progress.RoadmapNode.SkillId, Math.Round(Math.Clamp(progress.BktMasteryPlt, 0.05, 0.99), 4)) },
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể cập nhật LearningProfiles khi hoàn thành REFLECT cho Skill {SkillId}.", progress.RoadmapNode.SkillId);
                }
            }
        }

        _logger.LogInformation(
            "Học sinh {StudentId} hoàn thành P-L-A-R chặng {RoadmapNodeId}: Rating={Rating}/5, Summary='{Summary}', MistakeNotes='{Notes}'",
            progress.StudentId,
            progress.RoadmapNodeId,
            request.Request.ConfidenceRating,
            request.Request.LearnedSummary ?? "",
            request.Request.MistakeNotes ?? "");

        string message = nextUnlockedNodeId.HasValue
            ? "Chúc mừng bạn đã hoàn thành chặng học theo chuẩn chu trình P-L-A-R! Chặng học kế tiếp trên lộ trình đã được tự động mở khóa."
            : "Chúc mừng bạn đã hoàn thành toàn bộ lộ trình học tập thích ứng cá nhân hóa!";

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
