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

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Approval;

public record RoadmapReviewQueueItemDto(
    Guid RoadmapId,
    Guid StudentId,
    string StudentName,
    Guid? DiagnosticSubmissionId,
    int? DiagnosticScore,
    int? DiagnosticCorrect,
    int? DiagnosticTotal,
    string? PlacementClass,
    int TargetScore,
    int TotalMilestones,
    int ActiveMilestones,
    int PrunedMilestones,
    int CompletedMilestones,
    string Status,
    string? ReviewNote,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record TeacherReviewRoadmapRequestDto(
    int? TargetScore,
    string? TeacherNote,
    List<Guid>? PruneNodeIds,
    List<Guid>? RestoreNodeIds,
    List<Guid>? OrderedNodeIds,
    List<Guid>? AddSkillIds,
    bool SubmitForDirectorApproval = false
);

public record DirectorDecisionRequestDto(
    bool Approve,
    string? DirectorNote
);

public record RoadmapActionResponseDto(
    Guid RoadmapId,
    Guid StudentId,
    string Status,
    int TargetScore,
    int ActiveMilestones,
    int PrunedMilestones,
    string? ReviewNote,
    string Message
);

// =========================================================================
// 1. Query: Lấy danh sách lộ trình theo trạng thái duyệt (Teacher & Director)
// =========================================================================
public record GetRoadmapReviewQueueQuery(string? StatusFilter = null) : IRequest<Result<IReadOnlyList<RoadmapReviewQueueItemDto>>>;

public class GetRoadmapReviewQueueQueryHandler : IRequestHandler<GetRoadmapReviewQueueQuery, Result<IReadOnlyList<RoadmapReviewQueueItemDto>>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly ILogger<GetRoadmapReviewQueueQueryHandler> _logger;

    public GetRoadmapReviewQueueQueryHandler(
        ILearningRoadmapRepository roadmapRepository,
        IIdentityGrpcClient identityGrpcClient,
        ILogger<GetRoadmapReviewQueueQueryHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _identityGrpcClient = identityGrpcClient;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<RoadmapReviewQueueItemDto>>> Handle(
        GetRoadmapReviewQueueQuery request,
        CancellationToken ct)
    {
        var statuses = string.IsNullOrWhiteSpace(request.StatusFilter) || string.Equals(request.StatusFilter, "ALL", StringComparison.OrdinalIgnoreCase)
            ? new[] { "DRAFT_PENDING_TEACHER", "PENDING_DIRECTOR_APPROVAL", "ACTIVE" }
            : request.StatusFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var roadmaps = await _roadmapRepository.GetByStatusesAsync(statuses, ct);

        var studentNames = new Dictionary<Guid, string>();
        try
        {
            var ids = roadmaps.Select(r => r.StudentId).Distinct().ToList();
            if (ids.Count > 0)
            {
                studentNames = await _identityGrpcClient.GetStudentNamesAsync(ids, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tra cứu tên học sinh qua Identity gRPC cho hàng đợi duyệt lộ trình.");
        }

        var items = roadmaps.Select(r =>
        {
            string name = studentNames.TryGetValue(r.StudentId, out var resolved) && !string.IsNullOrWhiteSpace(resolved)
                ? resolved
                : $"Học sinh #{r.StudentId.ToString()[..8].ToUpperInvariant()}";

            int prunedCount = r.Nodes.Count(n => n.IsPruned || n.Status == "SKIPPED_PRUNED");
            int activeCount = r.Nodes.Count - prunedCount;
            int completedCount = r.Nodes.Count(n => n.Status == "COMPLETED");

            return new RoadmapReviewQueueItemDto(
                RoadmapId: r.RoadmapId,
                StudentId: r.StudentId,
                StudentName: name,
                DiagnosticSubmissionId: r.DiagnosticSubmissionId,
                DiagnosticScore: r.DiagnosticSubmission != null ? (int?)Math.Round((double)r.DiagnosticSubmission.TotalScore) : null,
                DiagnosticCorrect: r.DiagnosticSubmission?.TotalCorrect,
                DiagnosticTotal: r.DiagnosticSubmission?.TotalQuestions,
                PlacementClass: r.DiagnosticSubmission?.PlacementClass,
                TargetScore: r.TargetScore,
                TotalMilestones: r.Nodes.Count,
                ActiveMilestones: activeCount,
                PrunedMilestones: prunedCount,
                CompletedMilestones: completedCount,
                Status: r.Status,
                ReviewNote: r.PrunedReason,
                CreatedAt: r.CreatedAt,
                UpdatedAt: r.UpdatedAt
            );
        }).ToList();

        return Result<IReadOnlyList<RoadmapReviewQueueItemDto>>.Success(items);
    }
}

// =========================================================================
// 2. Query: Xem chi tiết lộ trình theo RoadmapId (cho Teacher & Director)
// =========================================================================
public record GetRoadmapByIdQuery(Guid RoadmapId) : IRequest<Result<RoadmapTimelineDto>>;

public class GetRoadmapByIdQueryHandler : IRequestHandler<GetRoadmapByIdQuery, Result<RoadmapTimelineDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IClassHeatmapRepository _heatmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetRoadmapByIdQueryHandler> _logger;

    public GetRoadmapByIdQueryHandler(
        ILearningRoadmapRepository roadmapRepository,
        IClassHeatmapRepository heatmapRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetRoadmapByIdQueryHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _heatmapRepository = heatmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<RoadmapTimelineDto>> Handle(GetRoadmapByIdQuery request, CancellationToken ct)
    {
        var roadmap = await _roadmapRepository.GetByIdAsync(request.RoadmapId, ct);
        if (roadmap == null)
        {
            return Result<RoadmapTimelineDto>.Failure(
                Error.NotFound("Roadmap.NotFound", $"Không tìm thấy lộ trình {request.RoadmapId}."));
        }

        IReadOnlyDictionary<Guid, SkillTreeNodeDto> skillMap = new Dictionary<Guid, SkillTreeNodeDto>();
        try
        {
            var skills = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            skillMap = skills.ToDictionary(s => s.SkillId, s => s);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể nạp metadata kỹ năng từ Content gRPC khi xem chi tiết Roadmap {RoadmapId}.", request.RoadmapId);
        }

        var heatmaps = await _heatmapRepository.GetHeatmapsByStudentIdAsync(roadmap.StudentId, ct);
        var heatmapBySkill = heatmaps
            .GroupBy(h => h.SkillId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var nodeDtos = roadmap.Nodes
            .OrderBy(n => n.StepOrder)
            .Select(n =>
            {
                skillMap.TryGetValue(n.SkillId, out var skillInfo);
                heatmapBySkill.TryGetValue(n.SkillId, out var hm);
                double plt = hm?.PltValue ?? 0.0;
                string assessStatus = n.Status == "COMPLETED"
                    ? "COMPLETED"
                    : hm != null
                        ? (plt >= 0.70 ? "MASTERED" : "WEAK")
                        : "UNASSESSED";
                int gradeLevel = GenerateRoadmapCommandHandler.InferGradeLevel(
                    skillInfo?.ChapterName,
                    skillInfo?.LessonName,
                    skillInfo?.Name);

                return new RoadmapNodeSummaryDto(
                    n.NodeId,
                    n.SkillId,
                    skillInfo?.Name ?? "Kỹ năng chuyên đề",
                    skillInfo?.DomainId ?? Guid.Empty,
                    !string.IsNullOrWhiteSpace(skillInfo?.DomainName) ? skillInfo.DomainName : "Lĩnh vực chung",
                    n.StepOrder,
                    n.MaterialId ?? skillInfo?.MaterialId,
                    n.QuizExamId,
                    n.Status,
                    n.IsPruned,
                    n.UnlockedAt,
                    n.CompletedAt,
                    skillInfo?.DomainCode ?? "",
                    skillInfo?.SubjectName ?? "",
                    skillInfo?.ChapterName ?? "",
                    skillInfo?.LessonName ?? "",
                    skillInfo?.VideoUrl ?? "",
                    Math.Round(plt, 4),
                    "",
                    assessStatus,
                    gradeLevel
                );
            })
            .ToList();

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

        int activeCount = nodeDtos.Count(n => !n.IsPruned);
        int completedCount = nodeDtos.Count(n => n.Status == "COMPLETED");
        double pct = activeCount > 0 ? Math.Round((double)completedCount / activeCount * 100.0, 2) : 0.0;

        return Result<RoadmapTimelineDto>.Success(new RoadmapTimelineDto(
            RoadmapId: roadmap.RoadmapId,
            StudentId: roadmap.StudentId,
            DiagnosticSubmissionId: roadmap.DiagnosticSubmissionId,
            TargetScore: roadmap.TargetScore,
            TotalMilestones: activeCount,
            CompletedMilestones: completedCount,
            ProgressPercentage: pct,
            IsPruned: roadmap.IsPruned,
            PrunedReason: roadmap.PrunedReason,
            Status: roadmap.Status,
            CreatedAt: roadmap.CreatedAt,
            UpdatedAt: roadmap.UpdatedAt,
            Stages: stageDtos,
            Nodes: nodeDtos
        ));
    }
}

// =========================================================================
// 3. Command: Giảng viên tinh chỉnh bản nháp & gửi lên Giám đốc phê duyệt
// =========================================================================
public record TeacherReviewRoadmapCommand(
    Guid RoadmapId,
    Guid ReviewerId,
    TeacherReviewRoadmapRequestDto Request
) : IRequest<Result<RoadmapActionResponseDto>>;

public class TeacherReviewRoadmapCommandHandler : IRequestHandler<TeacherReviewRoadmapCommand, Result<RoadmapActionResponseDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly ILogger<TeacherReviewRoadmapCommandHandler> _logger;

    public TeacherReviewRoadmapCommandHandler(
        ILearningRoadmapRepository roadmapRepository,
        ILogger<TeacherReviewRoadmapCommandHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _logger = logger;
    }

    public async Task<Result<RoadmapActionResponseDto>> Handle(
        TeacherReviewRoadmapCommand command,
        CancellationToken ct)
    {
        var roadmap = await _roadmapRepository.GetByIdAsync(command.RoadmapId, ct);
        if (roadmap == null)
        {
            return Result<RoadmapActionResponseDto>.Failure(
                Error.NotFound("Roadmap.NotFound", $"Không tìm thấy lộ trình {command.RoadmapId}."));
        }

        if (roadmap.Status == "ARCHIVED")
        {
            return Result<RoadmapActionResponseDto>.Failure(
                Error.Validation("Roadmap.Archived", "Không thể chỉnh sửa lộ trình đã lưu trữ."));
        }

        var req = command.Request;

        if (req.TargetScore.HasValue && req.TargetScore.Value >= 400 && req.TargetScore.Value <= 1200)
        {
            roadmap.TargetScore = req.TargetScore.Value;
        }

        var pruneSet = req.PruneNodeIds?.ToHashSet() ?? new HashSet<Guid>();
        var restoreSet = req.RestoreNodeIds?.ToHashSet() ?? new HashSet<Guid>();

        foreach (var node in roadmap.Nodes)
        {
            if (pruneSet.Contains(node.NodeId))
            {
                node.IsPruned = true;
                if (node.Status != "COMPLETED") node.Status = "SKIPPED_PRUNED";
            }
            else if (restoreSet.Contains(node.NodeId))
            {
                node.IsPruned = false;
                if (node.Status == "SKIPPED_PRUNED") node.Status = "UNLOCKED";
            }
        }

        // Thêm chặng kỹ năng mới theo đề xuất của Giảng viên
        if (req.AddSkillIds != null && req.AddSkillIds.Count > 0)
        {
            var existingSkillIds = roadmap.Nodes.Select(n => n.SkillId).ToHashSet();
            int maxStep = roadmap.Nodes.Any() ? roadmap.Nodes.Max(n => n.StepOrder) : 0;
            foreach (var skillId in req.AddSkillIds.Distinct())
            {
                if (skillId == Guid.Empty || existingSkillIds.Contains(skillId)) continue;
                var newNode = new RoadmapNode
                {
                    NodeId = Guid.NewGuid(),
                    RoadmapId = roadmap.RoadmapId,
                    SkillId = skillId,
                    StepOrder = ++maxStep,
                    Status = "UNLOCKED",
                    IsPruned = false,
                    UnlockedAt = DateTime.UtcNow
                };
                await _roadmapRepository.AddNodeAsync(newNode, ct);
                roadmap.Nodes.Add(newNode);
                existingSkillIds.Add(skillId);
            }
        }

        // Đổi thứ tự ưu tiên chặng học (đưa các node trong OrderedNodeIds lên đầu theo đúng thứ tự)
        if (req.OrderedNodeIds != null && req.OrderedNodeIds.Count > 0)
        {
            var orderIndex = new Dictionary<Guid, int>();
            for (int i = 0; i < req.OrderedNodeIds.Count; i++)
            {
                if (!orderIndex.ContainsKey(req.OrderedNodeIds[i]))
                {
                    orderIndex[req.OrderedNodeIds[i]] = i;
                }
            }

            var reordered = roadmap.Nodes
                .OrderBy(n => orderIndex.TryGetValue(n.NodeId, out var idx) ? idx : int.MaxValue)
                .ThenBy(n => n.StepOrder)
                .ToList();

            for (int i = 0; i < reordered.Count; i++)
            {
                reordered[i].StepOrder = i + 1;
            }
        }

        if (!string.IsNullOrWhiteSpace(req.TeacherNote))
        {
            roadmap.PrunedReason = $"[GV] {req.TeacherNote.Trim()}";
        }

        int prunedCount = roadmap.Nodes.Count(n => n.IsPruned || n.Status == "SKIPPED_PRUNED");
        int activeCount = roadmap.Nodes.Count - prunedCount;
        roadmap.IsPruned = prunedCount > 0;
        roadmap.TotalMilestones = activeCount;

        if (req.SubmitForDirectorApproval)
        {
            roadmap.Status = "PENDING_DIRECTOR_APPROVAL";
        }

        roadmap.UpdatedAt = DateTime.UtcNow;
        await _roadmapRepository.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Giảng viên {ReviewerId} đã cập nhật lộ trình {RoadmapId} -> Status={Status}, Active={Active}, Pruned={Pruned}.",
            command.ReviewerId, roadmap.RoadmapId, roadmap.Status, activeCount, prunedCount);

        string msg = req.SubmitForDirectorApproval
            ? "Đã lưu chỉnh sửa và gửi lộ trình lên Ban Giám đốc phê duyệt."
            : "Đã lưu bản chỉnh sửa nháp lộ trình của học sinh.";

        return Result<RoadmapActionResponseDto>.Success(new RoadmapActionResponseDto(
            roadmap.RoadmapId,
            roadmap.StudentId,
            roadmap.Status,
            roadmap.TargetScore,
            activeCount,
            prunedCount,
            roadmap.PrunedReason,
            msg
        ));
    }
}

// =========================================================================
// 4. Command: Giám đốc / Quản lý học thuật phê duyệt cuối cùng -> ACTIVE
// =========================================================================
public record DirectorDecisionCommand(
    Guid RoadmapId,
    Guid DirectorId,
    DirectorDecisionRequestDto Request
) : IRequest<Result<RoadmapActionResponseDto>>;

public class DirectorDecisionCommandHandler : IRequestHandler<DirectorDecisionCommand, Result<RoadmapActionResponseDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly ILogger<DirectorDecisionCommandHandler> _logger;

    public DirectorDecisionCommandHandler(
        ILearningRoadmapRepository roadmapRepository,
        ILogger<DirectorDecisionCommandHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _logger = logger;
    }

    public async Task<Result<RoadmapActionResponseDto>> Handle(
        DirectorDecisionCommand command,
        CancellationToken ct)
    {
        var roadmap = await _roadmapRepository.GetByIdAsync(command.RoadmapId, ct);
        if (roadmap == null)
        {
            return Result<RoadmapActionResponseDto>.Failure(
                Error.NotFound("Roadmap.NotFound", $"Không tìm thấy lộ trình {command.RoadmapId}."));
        }

        var req = command.Request;
        var nowUtc = DateTime.UtcNow;

        if (req.Approve)
        {
            // Archive các lộ trình ACTIVE cũ (nếu có) trước khi kích hoạt bản mới được phê duyệt
            var allStudentRoadmaps = await _roadmapRepository.GetByStudentIdAsync(roadmap.StudentId, ct);
            foreach (var old in allStudentRoadmaps)
            {
                if (old.RoadmapId != roadmap.RoadmapId && old.Status == "ACTIVE")
                {
                    old.Status = "ARCHIVED";
                    old.UpdatedAt = nowUtc;
                }
            }

            roadmap.Status = "ACTIVE";
            if (!string.IsNullOrWhiteSpace(req.DirectorNote))
            {
                roadmap.PrunedReason = string.IsNullOrWhiteSpace(roadmap.PrunedReason)
                    ? $"[Director] {req.DirectorNote.Trim()}"
                    : $"{roadmap.PrunedReason} | [Director] {req.DirectorNote.Trim()}";
            }

            // Đảm bảo chặng đầu tiên không bị prune ở trạng thái IN_PROGRESS để học sinh vào học ngay
            var firstActiveNode = roadmap.Nodes
                .Where(n => !n.IsPruned && n.Status != "SKIPPED_PRUNED" && n.Status != "COMPLETED")
                .OrderBy(n => n.StepOrder)
                .FirstOrDefault();

            if (firstActiveNode != null)
            {
                firstActiveNode.Status = "IN_PROGRESS";
                firstActiveNode.UnlockedAt ??= nowUtc;
            }
        }
        else
        {
            roadmap.Status = "DRAFT_PENDING_TEACHER";
            string note = string.IsNullOrWhiteSpace(req.DirectorNote)
                ? "Yêu cầu giảng viên rà soát và điều chỉnh lại lộ trình."
                : req.DirectorNote.Trim();
            roadmap.PrunedReason = $"[Director yêu cầu sửa] {note}";
        }

        roadmap.UpdatedAt = nowUtc;
        await _roadmapRepository.SaveChangesAsync(ct);

        int prunedCount = roadmap.Nodes.Count(n => n.IsPruned || n.Status == "SKIPPED_PRUNED");
        int activeCount = roadmap.Nodes.Count - prunedCount;

        _logger.LogInformation(
            "Director {DirectorId} đã {Action} lộ trình {RoadmapId} của học sinh {StudentId} -> Status={Status}.",
            command.DirectorId, req.Approve ? "PHÊ DUYỆT" : "TRẢ LẠI", roadmap.RoadmapId, roadmap.StudentId, roadmap.Status);

        string msg = req.Approve
            ? "Đã phê duyệt và kích hoạt lộ trình học tập (ACTIVE)! Học sinh có thể bắt đầu học P-L-A-R ngay."
            : "Đã trả lại lộ trình cho Giảng viên điều chỉnh thêm.";

        return Result<RoadmapActionResponseDto>.Success(new RoadmapActionResponseDto(
            roadmap.RoadmapId,
            roadmap.StudentId,
            roadmap.Status,
            roadmap.TargetScore,
            activeCount,
            prunedCount,
            roadmap.PrunedReason,
            msg
        ));
    }
}
