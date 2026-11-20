using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMyRoadmap;

/// <summary>
/// Handler tra cứu lộ trình học tập cá nhân hóa đang kích hoạt của học sinh (Core Flow 2 - API 2)
/// Tự động đánh giá lại (Re-evaluate) trạng thái từng chặng dựa trên toàn bộ bài test học sinh đã làm về sau
/// (Diagnostic 2/3, Đề thi thử MockExam, Quick Check Preview và Luyện tập Apply trong ClassSkillHeatmaps + StageProgress):
///   1. WEAK (Ưu tiên cải thiện ngay — xếp đầu lộ trình)
///   2. UNASSESSED (Chưa đánh giá — vẫn nối tiếp theo lộ trình học tuần tự từ đầu đến cuối Lớp 10 -> 11 -> 12, Chương 1..N, Bài 1..M)
///   3. MASTERED / COMPLETED (Đã làm bài test đạt điểm OK hoặc đã hoàn thành PLAR — đẩy xuống hiển thị ở sau cùng)
/// </summary>
public class GetMyRoadmapQueryHandler : IRequestHandler<GetMyRoadmapQuery, Result<RoadmapTimelineDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IClassHeatmapRepository _heatmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetMyRoadmapQueryHandler> _logger;

    public GetMyRoadmapQueryHandler(
        ILearningRoadmapRepository roadmapRepository,
        IStageProgressRepository stageProgressRepository,
        IClassHeatmapRepository heatmapRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetMyRoadmapQueryHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _stageProgressRepository = stageProgressRepository;
        _heatmapRepository = heatmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<RoadmapTimelineDto>> Handle(
        GetMyRoadmapQuery request,
        CancellationToken ct)
    {
        _logger.LogInformation("Tra cứu & đánh giá lại lộ trình học tập cá nhân hóa (ACTIVE) cho học sinh {StudentId}", request.StudentId);

        var roadmap = await _roadmapRepository.GetActiveByStudentIdAsync(request.StudentId, ct);

        if (roadmap == null)
        {
            return Result<RoadmapTimelineDto>.Failure(
                Error.NotFound("Roadmap.NotFound",
                    $"Học sinh {request.StudentId} chưa có lộ trình học tập nào đang kích hoạt (ACTIVE). Vui lòng hoàn thành bài thi chẩn đoán và khởi tạo lộ trình."));
        }

        // Nạp metadata kỹ năng, môn, chương, bài từ Content Service qua gRPC
        IReadOnlyDictionary<Guid, SkillTreeNodeDto> skillMap = new Dictionary<Guid, SkillTreeNodeDto>();
        try
        {
            var skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            skillMap = skillsTree.ToDictionary(s => s.SkillId, s => s);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối gRPC Content Service để lấy metadata kỹ năng cho Roadmap {RoadmapId}.", roadmap.RoadmapId);
        }

        // Nạp trạng thái PLAR thực tế của từng node & điểm đánh giá trực tiếp từ các bài test (ClassSkillHeatmaps)
        var nodeIds = roadmap.Nodes.Select(n => n.NodeId).ToList();
        var progresses = await _stageProgressRepository.GetByStudentAndNodeIdsAsync(request.StudentId, nodeIds, ct);
        var progressByNodeId = progresses
            .GroupBy(p => p.RoadmapNodeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var heatmaps = await _heatmapRepository.GetHeatmapsByStudentIdAsync(request.StudentId, ct);
        var heatmapBySkillId = heatmaps
            .GroupBy(h => h.SkillId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var evaluatedEntries = roadmap.Nodes
            .Select(n =>
            {
                skillMap.TryGetValue(n.SkillId, out var skillInfo);
                progressByNodeId.TryGetValue(n.NodeId, out var stageProg);
                heatmapBySkillId.TryGetValue(n.SkillId, out var heatmapCell);

                bool isCompleted = string.Equals(n.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stageProg?.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

                bool hasActivePlarAssessment = stageProg != null && (
                    string.Equals(stageProg.CurrentStep, "LEARN", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stageProg.CurrentStep, "APPLY", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stageProg.CurrentStep, "REFLECT", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stageProg.Status, "REMEDIAL_REQUIRED", StringComparison.OrdinalIgnoreCase));

                string assessmentStatus;
                double masteryPlt;

                if (isCompleted)
                {
                    assessmentStatus = "COMPLETED";
                    masteryPlt = stageProg?.BktMasteryPlt ?? heatmapCell?.PltValue ?? 0.90;
                }
                else if (heatmapCell != null)
                {
                    // Đã được kiểm tra trực tiếp (qua bài Diagnostic đầu vào, các bài Test/Thi thử làm về sau, hoặc Quick Check/Apply)
                    masteryPlt = hasActivePlarAssessment
                        ? Math.Max(stageProg!.BktMasteryPlt, heatmapCell.PltValue)
                        : heatmapCell.PltValue;
                    assessmentStatus = masteryPlt >= 0.70 ? "MASTERED" : "WEAK";
                }
                else if (hasActivePlarAssessment)
                {
                    masteryPlt = stageProg!.BktMasteryPlt;
                    assessmentStatus = masteryPlt >= 0.70 ? "MASTERED" : "WEAK";
                }
                else
                {
                    // Chưa từng gặp trong bài test nào -> giữ nguyên trong mạch lộ trình học tuần tự từ đầu đến cuối
                    assessmentStatus = "UNASSESSED";
                    masteryPlt = 0.0;
                }

                string currentPlarStep = stageProg != null
                    ? (stageProg.Status == "REMEDIAL_REQUIRED" ? "REMEDIAL" : stageProg.CurrentStep)
                    : (isCompleted ? "COMPLETED" : n.Status == "IN_PROGRESS" ? "PREVIEW" : "");

                int gradeLevel = GenerateRoadmapCommandHandler.InferGradeLevel(
                    skillInfo?.ChapterName,
                    skillInfo?.LessonName,
                    skillInfo?.Name);

                return new
                {
                    Entity = n,
                    SkillInfo = skillInfo,
                    AssessmentStatus = assessmentStatus,
                    MasteryPlt = Math.Round(masteryPlt, 4),
                    CurrentPlarStep = currentPlarStep,
                    GradeLevel = gradeLevel,
                    IsCompleted = isCompleted
                };
            })
            // Sắp xếp mạch học tuần tự từ đầu đến cuối:
            //   0 = WEAK (Ưu tiên cải thiện ngay)
            //   1 = UNASSESSED (Chưa đánh giá — học tuần tự theo SGK: Môn -> Lớp 10..12 -> Chương 1..N -> Bài 1..M)
            //   2 = MASTERED (Đã làm bài test đạt điểm OK — hiển thị sau)
            //   3 = COMPLETED (Đã hoàn thành trọn vẹn PLAR — hiển thị sau)
            .OrderBy(x => GetAssessmentZoneOrder(x.AssessmentStatus))
            .ThenBy(x => x.AssessmentStatus == "WEAK" ? x.MasteryPlt : 0.0)
            .ThenBy(x => GenerateRoadmapCommandHandler.GetSubjectSortOrder(x.SkillInfo?.SubjectName))
            .ThenBy(x => x.GradeLevel == 0 ? 99 : x.GradeLevel)
            .ThenBy(x => GenerateRoadmapCommandHandler.ExtractChapterNumber(x.SkillInfo?.ChapterName))
            .ThenBy(x => x.SkillInfo?.ChapterName ?? "")
            .ThenBy(x => GenerateRoadmapCommandHandler.ExtractLessonNumber(x.SkillInfo?.LessonName))
            .ThenBy(x => x.SkillInfo?.LessonName ?? "")
            .ThenBy(x => x.Entity.StepOrder)
            .ToList();

        // Đồng bộ lại StepOrder tuần tự 1..N khi có sự đánh giá lại từ các bài test về sau (ví dụ UNASSESSED -> MASTERED hoặc WEAK)
        bool stepOrderChanged = false;
        var nodeDtos = new List<RoadmapNodeSummaryDto>(evaluatedEntries.Count);

        for (int idx = 0; idx < evaluatedEntries.Count; idx++)
        {
            var item = evaluatedEntries[idx];
            int newStepOrder = idx + 1;
            if (item.Entity.StepOrder != newStepOrder)
            {
                item.Entity.StepOrder = newStepOrder;
                stepOrderChanged = true;
            }

            if (item.IsCompleted && item.Entity.Status != "COMPLETED")
            {
                item.Entity.Status = "COMPLETED";
                stepOrderChanged = true;
            }

            nodeDtos.Add(new RoadmapNodeSummaryDto(
                item.Entity.NodeId,
                item.Entity.SkillId,
                item.SkillInfo?.Name ?? "Kỹ năng chuyên đề",
                item.SkillInfo?.DomainId ?? Guid.Empty,
                !string.IsNullOrWhiteSpace(item.SkillInfo?.DomainName) ? item.SkillInfo.DomainName : "Lĩnh vực chung",
                newStepOrder,
                item.Entity.MaterialId ?? item.SkillInfo?.MaterialId,
                item.Entity.QuizExamId,
                item.IsCompleted ? "COMPLETED" : item.Entity.Status,
                item.Entity.IsPruned,
                item.Entity.UnlockedAt,
                item.Entity.CompletedAt,
                item.SkillInfo?.DomainCode ?? "",
                item.SkillInfo?.SubjectName ?? "",
                item.SkillInfo?.ChapterName ?? "",
                item.SkillInfo?.LessonName ?? "",
                item.SkillInfo?.VideoUrl ?? "",
                item.MasteryPlt,
                item.CurrentPlarStep,
                item.AssessmentStatus,
                item.GradeLevel
            ));
        }

        if (stepOrderChanged)
        {
            try
            {
                roadmap.CompletedMilestones = roadmap.Nodes.Count(n => n.Status == "COMPLETED");
                roadmap.UpdatedAt = DateTime.UtcNow;
                await _roadmapRepository.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lưu cập nhật thứ tự tái đánh giá StepOrder cho Roadmap {RoadmapId}.", roadmap.RoadmapId);
            }
        }

        var stageDtos = nodeDtos
            .GroupBy(n => new { n.DomainId, n.DomainName, n.DomainCode })
            .Select(g => new RoadmapStageDto(
                g.Key.DomainId,
                g.Key.DomainName,
                g.Count(),
                g.Count(n => n.Status == "COMPLETED"),
                g.OrderBy(n => n.StepOrder).ToList(),
                g.Key.DomainCode
            ))
            .ToList();

        int completedMilestones = nodeDtos.Count(n => n.Status == "COMPLETED");
        int totalActiveMilestones = nodeDtos.Count(n => !n.IsPruned);
        if (totalActiveMilestones == 0) totalActiveMilestones = roadmap.TotalMilestones;

        double progressPercentage = totalActiveMilestones > 0
            ? Math.Round((double)completedMilestones / totalActiveMilestones * 100.0, 2)
            : 0.0;

        var timelineDto = new RoadmapTimelineDto(
            RoadmapId: roadmap.RoadmapId,
            StudentId: roadmap.StudentId,
            DiagnosticSubmissionId: roadmap.DiagnosticSubmissionId,
            TargetScore: roadmap.TargetScore,
            TotalMilestones: totalActiveMilestones,
            CompletedMilestones: completedMilestones,
            ProgressPercentage: progressPercentage,
            IsPruned: roadmap.IsPruned,
            PrunedReason: roadmap.PrunedReason,
            Status: roadmap.Status,
            CreatedAt: roadmap.CreatedAt,
            UpdatedAt: roadmap.UpdatedAt,
            Stages: stageDtos,
            Nodes: nodeDtos
        );

        return Result<RoadmapTimelineDto>.Success(timelineDto);
    }

    private static int GetAssessmentZoneOrder(string assessmentStatus) => assessmentStatus switch
    {
        "WEAK" => 0,        // Phần 1: Ưu tiên cải thiện ngay (hiển thị đầu tiên)
        "UNASSESSED" => 1,  // Phần 2: Chưa đánh giá — nối tiếp lộ trình học tuần tự từ đầu đến cuối Lớp 10-12
        "MASTERED" => 2,    // Phần 3a: Đã làm bài test đạt điểm OK (hiển thị ở sau)
        "COMPLETED" => 3,   // Phần 3b: Đã hoàn thành chu trình PLAR (hiển thị ở sau)
        _ => 1
    };
}
