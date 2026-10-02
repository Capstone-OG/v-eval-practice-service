using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetRoadmapNodeDetail;

/// <summary>
/// Handler tra cứu thông tin chi tiết của 1 chặng học (Core Flow 2 - API 3)
/// </summary>
public class GetRoadmapNodeDetailQueryHandler : IRequestHandler<GetRoadmapNodeDetailQuery, Result<RoadmapNodeDetailDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetRoadmapNodeDetailQueryHandler> _logger;

    public GetRoadmapNodeDetailQueryHandler(
        ILearningRoadmapRepository roadmapRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetRoadmapNodeDetailQueryHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<RoadmapNodeDetailDto>> Handle(
        GetRoadmapNodeDetailQuery request,
        CancellationToken ct)
    {
        _logger.LogInformation("Lấy thông tin chi tiết chặng học {NodeId}", request.NodeId);

        var node = await _roadmapRepository.GetNodeByIdAsync(request.NodeId, ct);

        if (node == null)
        {
            return Result<RoadmapNodeDetailDto>.Failure(
                Error.NotFound("RoadmapNode.NotFound", $"Không tìm thấy chặng học {request.NodeId}."));
        }

        // Kiểm tra quyền sở hữu nếu có truyền StudentId từ Gateway hoặc Client
        if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty && node.Roadmap.StudentId != request.StudentId.Value)
        {
            return Result<RoadmapNodeDetailDto>.Failure(
                Error.Forbidden("RoadmapNode.Forbidden", "Bạn không có quyền truy cập thông tin chặng học của học sinh khác."));
        }

        // Nạp thông tin kỹ năng và miền năng lực từ Content Service qua gRPC
        string skillName = "Kỹ năng chuyên đề";
        Guid domainId = Guid.Empty;
        string domainName = "Lĩnh vực chung";
        string domainCode = "";

        try
        {
            var skillsTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            var skillInfo = skillsTree.FirstOrDefault(s => s.SkillId == node.SkillId);
            if (skillInfo != null)
            {
                skillName = skillInfo.Name;
                domainId = skillInfo.DomainId;
                domainName = !string.IsNullOrWhiteSpace(skillInfo.DomainName) ? skillInfo.DomainName : "Lĩnh vực chung";
                domainCode = skillInfo.DomainCode ?? "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối gRPC Content Service để lấy metadata cho kỹ năng {SkillId}", node.SkillId);
        }

        // Trích xuất chi tiết buổi học Live Q&A nếu có
        LiveSessionDetailDto? liveSessionDto = null;
        if (node.LiveSession != null)
        {
            var attendance = await _roadmapRepository.GetAttendanceAsync(node.LiveSession.SessionId, node.Roadmap.StudentId, ct);
            liveSessionDto = new LiveSessionDetailDto(
                SessionId: node.LiveSession.SessionId,
                Title: node.LiveSession.Title,
                Description: node.LiveSession.Description,
                ScheduledAt: node.LiveSession.ScheduledAt,
                DurationMinutes: node.LiveSession.DurationMinutes,
                MeetingUrl: node.LiveSession.MeetingUrl,
                RecordingUrl: node.LiveSession.RecordingUrl,
                IsRecorded: node.LiveSession.IsRecorded,
                Status: node.LiveSession.Status,
                AttendanceStatus: attendance?.AttendanceStatus,
                IsMakeupQuizPassed: attendance?.IsMakeupQuizPassed
            );
        }

        bool isQuizEligible = node.IsVideoCompleted ||
            (node.VideoTotalSeconds > 0 && (double)node.VideoWatchedSeconds / node.VideoTotalSeconds >= 0.8);

        var detailDto = new RoadmapNodeDetailDto(
            NodeId: node.NodeId,
            RoadmapId: node.RoadmapId,
            StudentId: node.Roadmap.StudentId,
            SkillId: node.SkillId,
            SkillName: skillName,
            DomainId: domainId,
            DomainName: domainName,
            StepOrder: node.StepOrder,
            Status: node.Status,
            IsPruned: node.IsPruned,
            UnlockedAt: node.UnlockedAt,
            CompletedAt: node.CompletedAt,
            MaterialId: node.MaterialId,
            VideoWatchedSeconds: node.VideoWatchedSeconds,
            VideoTotalSeconds: node.VideoTotalSeconds,
            IsVideoCompleted: node.IsVideoCompleted,
            IsQuizEligible: isQuizEligible,
            QuizExamId: node.QuizExamId,
            LiveSession: liveSessionDto,
            DomainCode: domainCode
        );

        return Result<RoadmapNodeDetailDto>.Success(detailDto);
    }
}
