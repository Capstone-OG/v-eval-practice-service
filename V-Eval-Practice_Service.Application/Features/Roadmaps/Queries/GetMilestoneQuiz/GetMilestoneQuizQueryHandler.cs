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

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Queries.GetMilestoneQuiz;

/// <summary>
/// Handler xử lý lấy đề thi Quiz củng cố của chặng học (Core Flow 2 - API 5)
/// </summary>
public class GetMilestoneQuizQueryHandler : IRequestHandler<GetMilestoneQuizQuery, Result<MilestoneQuizDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetMilestoneQuizQueryHandler> _logger;

    public GetMilestoneQuizQueryHandler(
        ILearningRoadmapRepository roadmapRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetMilestoneQuizQueryHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<MilestoneQuizDto>> Handle(
        GetMilestoneQuizQuery request,
        CancellationToken ct)
    {
        _logger.LogInformation("Xử lý lấy đề thi Quiz củng cố cho chặng học {NodeId}", request.NodeId);

        var node = await _roadmapRepository.GetNodeByIdAsync(request.NodeId, ct);

        if (node == null)
        {
            return Result<MilestoneQuizDto>.Failure(
                Error.NotFound("RoadmapNode.NotFound", $"Không tìm thấy chặng học {request.NodeId}."));
        }

        // 1. Kiểm tra quyền sở hữu học sinh
        if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty && node.Roadmap.StudentId != request.StudentId.Value)
        {
            return Result<MilestoneQuizDto>.Failure(
                Error.Forbidden("RoadmapNode.Forbidden", "Bạn không có quyền truy cập bài kiểm tra chặng học của học sinh khác."));
        }

        // 2. Kiểm tra trạng thái máy chặng học (State Machine)
        if (node.Status == "LOCKED")
        {
            return Result<MilestoneQuizDto>.Failure(
                Error.Validation("RoadmapNode.Locked",
                    "Chặng học này đang bị khóa (LOCKED). Bạn cần hoàn thành bài kiểm tra của các chặng trước để mở khóa."));
        }

        if (node.Status == "SKIPPED_PRUNED")
        {
            return Result<MilestoneQuizDto>.Failure(
                Error.Validation("RoadmapNode.Pruned",
                    "Chặng học này đã được hệ thống cắt tỉa tối ưu (SKIPPED_PRUNED), không yêu cầu làm bài Quiz củng cố."));
        }

        // 3. Kiểm tra điều kiện tiên quyết xem video lý thuyết (Prerequisite Check)
        bool isVideoDone = node.IsVideoCompleted ||
            (node.VideoTotalSeconds > 0 && (double)node.VideoWatchedSeconds / node.VideoTotalSeconds >= 0.8);

        if (!isVideoDone)
        {
            double watchPct = node.VideoTotalSeconds > 0
                ? Math.Round((double)node.VideoWatchedSeconds / node.VideoTotalSeconds * 100.0, 1)
                : 0.0;

            return Result<MilestoneQuizDto>.Failure(
                Error.Validation("RoadmapNode.VideoNotCompleted",
                    $"Bạn mới theo dõi {watchPct}% thời lượng video lý thuyết. Vui lòng xem tối thiểu 80% bài giảng trước khi làm bài Quiz củng cố."));
        }

        // 4. Gọi Content Service gRPC để lấy bộ câu hỏi Quiz củng cố (ẩn hoàn toàn đáp án đúng)
        var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(node.SkillId, node.QuizExamId, 5, ct);

        if (quizResult == null || quizResult.Questions.Count == 0)
        {
            return Result<MilestoneQuizDto>.Failure(
                Error.NotFound("MilestoneQuiz.NotFound",
                    $"Không thể nạp danh sách câu hỏi cho bài Quiz củng cố kỹ năng {node.SkillId} từ Content Service."));
        }

        // 5. Nếu chặng học chưa được gán QuizExamId từ trước, gắn kết nguyên tử và lưu vào CSDL
        if (node.QuizExamId == null && quizResult.ExamId != Guid.Empty)
        {
            node.QuizExamId = quizResult.ExamId;
            await _roadmapRepository.SaveChangesAsync(ct);
            _logger.LogInformation("Đã liên kết QuizExamId {ExamId} vào chặng học {NodeId}.", quizResult.ExamId, node.NodeId);
        }

        // 6. Map dữ liệu sang DTO trả về cho Client
        var questionItems = quizResult.Questions.Select(q => new MilestoneQuizQuestionItemDto(
            QuestionId: q.QuestionId,
            QuestionOrder: q.QuestionOrder,
            Content: q.Content,
            Options: q.Options.Select(opt => new MilestoneQuizOptionDto(opt.OptionId, opt.Content)).ToList(),
            DifficultyLevel: q.DifficultyLevel,
            SkillId: q.SkillId,
            SkillName: q.SkillName
        )).ToList();

        string skillName = questionItems.FirstOrDefault()?.SkillName ?? "Kỹ năng chuyên đề";

        var responseDto = new MilestoneQuizDto(
            ExamId: quizResult.ExamId,
            NodeId: node.NodeId,
            SkillId: node.SkillId,
            SkillName: skillName,
            Title: quizResult.Title,
            DurationMinutes: quizResult.DurationMinutes,
            TotalQuestions: quizResult.TotalQuestions,
            NodeStatus: node.Status,
            IsVideoCompleted: true,
            Questions: questionItems
        );

        return Result<MilestoneQuizDto>.Success(responseDto);
    }
}
