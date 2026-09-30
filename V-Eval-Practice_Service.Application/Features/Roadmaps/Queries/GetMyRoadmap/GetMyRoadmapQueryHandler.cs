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
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMyRoadmap;

/// <summary>
/// Handler tra cứu lộ trình học tập cá nhân hóa đang kích hoạt của học sinh (Core Flow 2 - API 2)
/// </summary>
public class GetMyRoadmapQueryHandler : IRequestHandler<GetMyRoadmapQuery, Result<RoadmapTimelineDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetMyRoadmapQueryHandler> _logger;

    public GetMyRoadmapQueryHandler(
        ILearningRoadmapRepository roadmapRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetMyRoadmapQueryHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<RoadmapTimelineDto>> Handle(
        GetMyRoadmapQuery request,
        CancellationToken ct)
    {
        _logger.LogInformation("Tra cứu lộ trình học tập cá nhân hóa (ACTIVE) cho học sinh {StudentId}", request.StudentId);

        var roadmap = await _roadmapRepository.GetActiveByStudentIdAsync(request.StudentId, ct);

        if (roadmap == null)
        {
            return Result<RoadmapTimelineDto>.Failure(
                Error.NotFound("Roadmap.NotFound",
                    $"Học sinh {request.StudentId} chưa có lộ trình học tập nào đang kích hoạt (ACTIVE). Vui lòng hoàn thành bài thi chẩn đoán và khởi tạo lộ trình."));
        }

        // Nạp metadata kỹ năng và miền năng lực từ Content Service qua gRPC
        IReadOnlyDictionary<Guid, SkillTreeNodeDto> skillMap = new Dictionary<Guid, SkillTreeNodeDto>();
        try
        {
            var skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            skillMap = skillsTree.ToDictionary(s => s.SkillId, s => s);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối gRPC Content Service để lấy metadata kỹ năng cho Roadmap {RoadmapId}. Sử dụng dữ liệu mặc định.", roadmap.RoadmapId);
        }

        // Map sang danh sách DTO của từng chặng học
        var nodeDtos = roadmap.Nodes
            .OrderBy(n => n.StepOrder)
            .Select(n =>
            {
                skillMap.TryGetValue(n.SkillId, out var skillInfo);
                return new RoadmapNodeSummaryDto(
                    n.NodeId,
                    n.SkillId,
                    skillInfo?.Name ?? "Kỹ năng chuyên đề",
                    skillInfo?.DomainId ?? Guid.Empty,
                    !string.IsNullOrWhiteSpace(skillInfo?.DomainName) ? skillInfo.DomainName : "Lĩnh vực chung",
                    n.StepOrder,
                    n.MaterialId,
                    n.QuizExamId,
                    n.LiveSessionId,
                    n.Status,
                    n.IsPruned,
                    n.UnlockedAt,
                    n.CompletedAt,
                    skillInfo?.DomainCode ?? ""
                );
            })
            .ToList();

        // Gom nhóm theo Miền năng lực / Môn học (Stages / Group by Domain)
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

        // Tính toán số lượng mốc và tiến độ phần trăm hoàn thành thực tế
        int completedMilestones = roadmap.Nodes.Count(n => n.Status == "COMPLETED");
        int totalActiveMilestones = roadmap.Nodes.Count(n => !n.IsPruned);
        if (totalActiveMilestones == 0) totalActiveMilestones = roadmap.TotalMilestones;

        double progressPercentage = totalActiveMilestones > 0
            ? Math.Round((double)completedMilestones / totalActiveMilestones * 100.0, 2)
            : 0.0;

        var timelineDto = new RoadmapTimelineDto(
            RoadmapId: roadmap.RoadmapId,
            StudentId: roadmap.StudentId,
            DiagnosticSubmissionId: roadmap.DiagnosticSubmissionId,
            TargetScore: roadmap.TargetScore,
            TotalMilestones: roadmap.TotalMilestones,
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
}
