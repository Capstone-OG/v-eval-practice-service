using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Graph;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;

/// <summary>
/// Handler điều phối chuỗi 7 bước sinh lộ trình học tập cá nhân hóa (Core Flow 2 - API 1)
/// </summary>
public class GenerateRoadmapCommandHandler : IRequestHandler<GenerateRoadmapCommand, Result<GenerateRoadmapResponseDto>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly TarjanCycleDetector _tarjanDetector;
    private readonly PathPruner _pathPruner;
    private readonly TopologicalSorter _topologicalSorter;
    private readonly MilestoneBinder _milestoneBinder;
    private readonly ILogger<GenerateRoadmapCommandHandler> _logger;

    public GenerateRoadmapCommandHandler(
        IExamSubmissionRepository submissionRepository,
        ILearningProfileRepository learningProfileRepository,
        ILearningRoadmapRepository roadmapRepository,
        IContentGrpcClient contentGrpcClient,
        TarjanCycleDetector tarjanDetector,
        PathPruner pathPruner,
        TopologicalSorter topologicalSorter,
        MilestoneBinder milestoneBinder,
        ILogger<GenerateRoadmapCommandHandler> logger)
    {
        _submissionRepository = submissionRepository;
        _learningProfileRepository = learningProfileRepository;
        _roadmapRepository = roadmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _tarjanDetector = tarjanDetector;
        _pathPruner = pathPruner;
        _topologicalSorter = topologicalSorter;
        _milestoneBinder = milestoneBinder;
        _logger = logger;
    }

    public async Task<Result<GenerateRoadmapResponseDto>> Handle(
        GenerateRoadmapCommand request,
        CancellationToken ct)
    {
        _logger.LogInformation("Bắt đầu sinh lộ trình học tập cho học sinh {StudentId} từ bài thi chẩn đoán {SubmissionId}",
            request.StudentId, request.DiagnosticSubmissionId);

        // =====================================================================
        // Bước 1: Trích xuất hồ sơ năng lực đầu vào (Core Flow 1 Transition)
        // =====================================================================
        var submission = await _submissionRepository.GetByIdAsync(request.DiagnosticSubmissionId, ct);

        if (submission == null)
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.NotFound("ExamSubmission.NotFound", $"Không tìm thấy bài thi chẩn đoán {request.DiagnosticSubmissionId}"));
        }

        if (submission.StudentId != request.StudentId)
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Forbidden("ExamSubmission.Forbidden", "Bạn không có quyền tạo lộ trình từ bài nộp của học sinh khác."));
        }

        if (submission.Status != "COMPLETED")
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Validation("ExamSubmission.NotCompleted",
                    $"Bài thi chẩn đoán đang ở trạng thái '{submission.Status}'. Chỉ được sinh lộ trình từ bài thi đã hoàn thành (COMPLETED)."));
        }

        int targetScore = 800; // Ngưỡng điểm mục tiêu mặc định chuẩn ĐGNL ĐHQG-HCM

        // Lấy danh sách điểm số thành thạo P(L0) từ bảng LearningProfiles
        var learningProfiles = await _learningProfileRepository.GetByStudentIdAsync(request.StudentId, ct);
        var skillMasteryPL0 = learningProfiles.ToDictionary(lp => lp.SkillId, lp => lp.MasteryScore);

        // =====================================================================
        // Bước 2: Nạp đồ thị tri thức từ Content Service qua gRPC
        // =====================================================================
        IReadOnlyList<SkillTreeNodeDto> skillsTree;
        try
        {
            skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi gRPC GetSkillsTree sang Content Service.");
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("ContentService.GrpcUnavailable", "Không thể nạp Cây khung năng lực từ Content Service qua gRPC."));
        }

        if (skillsTree == null || skillsTree.Count == 0)
        {
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("ContentService.EmptySkillsTree", "Danh sách kỹ năng từ Content Service rỗng."));
        }

        var skillNameMap = skillsTree.ToDictionary(s => s.SkillId, s => s.Name);
        var candidateSkillIds = skillsTree.Select(s => s.SkillId).ToList();
        var skillWeights = skillsTree.ToDictionary(s => s.SkillId, s => s.Weight);
        var prerequisitesMap = skillsTree.ToDictionary(s => s.SkillId, s => s.PrerequisiteIds.ToList());

        // =====================================================================
        // Bước 3: Kiểm tra tính hợp lệ của đồ thị (Cycle Detection)
        // =====================================================================
        // Xây dựng danh sách kề dependentsGraph (skill -> danh sách kỹ năng phụ thuộc) cho Tarjan
        var adjacencyList = new Dictionary<Guid, List<Guid>>();
        foreach (var skillId in candidateSkillIds)
        {
            adjacencyList[skillId] = new List<Guid>();
        }

        foreach (var (skillId, prereqs) in prerequisitesMap)
        {
            foreach (var prereq in prereqs)
            {
                if (adjacencyList.ContainsKey(prereq))
                {
                    adjacencyList[prereq].Add(skillId);
                }
            }
        }

        var cycles = _tarjanDetector.DetectCycles(adjacencyList);
        if (cycles.Count > 0)
        {
            var cycleDescription = string.Join(" | ", cycles.Select(c => string.Join(" -> ", c)));
            _logger.LogError("Phát hiện chu trình kín trong Cây khung năng lực: {Cycles}", cycleDescription);
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("SkillGraph.CycleDetected",
                    $"Phát hiện lỗi vòng lặp phụ thuộc trong Cây khung năng lực: {cycleDescription}"));
        }

        // =====================================================================
        // Bước 4: Phân tích quỹ thời gian & Kích hoạt cắt tỉa (Path Pruning)
        // =====================================================================
        var pruningContext = new PruningContext(
            ExamDate: request.ExamDate,
            Now: DateTime.UtcNow,
            StudyHoursPerDay: request.StudyHoursPerDay,
            TargetScore: targetScore,
            SkillWeights: skillWeights,
            SkillMasteryPL0: skillMasteryPL0
        );

        var pruningOutcome = _pathPruner.Analyze(pruningContext, candidateSkillIds);
        var prunedSkillIds = pruningOutcome.SkillResults
            .Where(r => r.IsPruned)
            .Select(r => r.SkillId)
            .ToHashSet();

        // =====================================================================
        // Bước 5: Sắp xếp thứ tự học sư phạm (Topological Sort)
        // =====================================================================
        var metadataMap = new Dictionary<Guid, SkillSortMetadata>();
        foreach (var skillId in candidateSkillIds)
        {
            double pl0 = skillMasteryPL0.GetValueOrDefault(skillId, 0.5);
            double weight = skillWeights.GetValueOrDefault(skillId, 0.05);
            bool isWeak = pl0 < 0.6;
            metadataMap[skillId] = new SkillSortMetadata(skillId, pl0, weight, isWeak);
        }

        var sortedSkillIds = _topologicalSorter.Sort(candidateSkillIds, prerequisitesMap, metadataMap);

        // =====================================================================
        // Bước 6: Đóng gói chặng học 3 thành phần & Khởi tạo State Machine (Milestone Binding)
        // =====================================================================
        // Tìm kiếm buổi LiveSession có sẵn của lớp học được phân bổ (nếu có)
        LiveSession? upcomingLiveSession = null;
        if (submission.EnrolledClassId.HasValue)
        {
            upcomingLiveSession = await _roadmapRepository.GetUpcomingLiveSessionAsync(submission.EnrolledClassId.Value, ct);
        }

        var resourceBindings = new Dictionary<Guid, SkillResourceBinding>();
        foreach (var skillId in sortedSkillIds)
        {
            resourceBindings[skillId] = new SkillResourceBinding(
                SkillId: skillId,
                MaterialId: null,
                QuizExamId: null,
                LiveSessionId: upcomingLiveSession?.SessionId
            );
        }

        var roadmapId = Guid.NewGuid();
        var nodes = _milestoneBinder.BindMilestones(
            roadmapId,
            sortedSkillIds,
            resourceBindings,
            prunedSkillIds
        );

        // Khởi tạo thực thể LearningRoadmap
        var roadmap = new LearningRoadmap
        {
            RoadmapId = roadmapId,
            StudentId = request.StudentId,
            DiagnosticSubmissionId = request.DiagnosticSubmissionId,
            TargetScore = targetScore,
            TotalMilestones = nodes.Count(n => !n.IsPruned),
            CompletedMilestones = 0,
            IsPruned = pruningOutcome.IsOverloaded,
            PrunedReason = pruningOutcome.PrunedReason,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Nodes = nodes
        };

        // =====================================================================
        // Bước 7: Lưu trữ nguyên tử (Atomic Database Transaction)
        // =====================================================================
        try
        {
            // Lưu vết: Đánh dấu các lộ trình ACTIVE cũ của học sinh sang ARCHIVED
            await _roadmapRepository.ArchiveExistingActiveRoadmapsAsync(request.StudentId, ct);

            // Thêm mới lộ trình
            await _roadmapRepository.AddAsync(roadmap, ct);
            await _roadmapRepository.SaveChangesAsync(ct);

            _logger.LogInformation("Khởi tạo thành công Roadmap {RoadmapId} với {TotalNodes} chặng cho học sinh {StudentId}",
                roadmap.RoadmapId, nodes.Count, request.StudentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu trữ lộ trình học tập vào CSDL.");
            return Result<GenerateRoadmapResponseDto>.Failure(
                Error.Failure("Database.SaveFailed", "Không thể lưu trữ lộ trình học tập vào CSDL."));
        }

        // Map sang DTO phản hồi và gom nhóm theo Miền năng lực (Stages / Group by Domain)
        var skillMap = skillsTree.ToDictionary(s => s.SkillId, s => s);

        var nodeDtos = roadmap.Nodes
            .OrderBy(n => n.StepOrder)
            .Select(n =>
            {
                var skillInfo = skillMap.GetValueOrDefault(n.SkillId);
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

        var responseDto = new GenerateRoadmapResponseDto(
            roadmap.RoadmapId,
            roadmap.StudentId,
            roadmap.DiagnosticSubmissionId,
            roadmap.TargetScore,
            roadmap.TotalMilestones,
            roadmap.CompletedMilestones,
            roadmap.IsPruned,
            roadmap.PrunedReason,
            roadmap.Status,
            roadmap.CreatedAt,
            stageDtos,
            nodeDtos,
            submission.PlacementClass ?? "ACCELERATION"
        );

        return Result<GenerateRoadmapResponseDto>.Success(responseDto);
    }
}
