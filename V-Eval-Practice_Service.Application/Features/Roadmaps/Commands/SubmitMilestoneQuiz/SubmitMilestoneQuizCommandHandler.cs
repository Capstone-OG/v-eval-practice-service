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
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.SubmitMilestoneQuiz;

public class SubmitMilestoneQuizCommandHandler : IRequestHandler<SubmitMilestoneQuizCommand, Result<SubmitMilestoneQuizResponseDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<SubmitMilestoneQuizCommandHandler> _logger;

    public SubmitMilestoneQuizCommandHandler(
        ILearningRoadmapRepository roadmapRepository,
        IExamSubmissionRepository submissionRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<SubmitMilestoneQuizCommandHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _submissionRepository = submissionRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitMilestoneQuizResponseDto>> Handle(
        SubmitMilestoneQuizCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra sự tồn tại của chặng học (Node)
        var node = await _roadmapRepository.GetNodeByIdAsync(request.NodeId, cancellationToken);
        if (node == null)
        {
            return Result<SubmitMilestoneQuizResponseDto>.Failure(
                Error.NotFound("Roadmap.NodeNotFound", $"Không tìm thấy chặng học {request.NodeId}."));
        }

        // 2. Kiểm tra phân quyền sở hữu chặng học
        if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty &&
            node.Roadmap.StudentId != request.StudentId.Value)
        {
            return Result<SubmitMilestoneQuizResponseDto>.Failure(
                Error.Forbidden("Roadmap.Forbidden", "Bạn không có quyền nộp bài cho chặng học của học sinh khác."));
        }

        // 3. Kiểm tra trạng thái máy của chặng học
        if (node.Status == "LOCKED")
        {
            return Result<SubmitMilestoneQuizResponseDto>.Failure(
                Error.Validation("Roadmap.NodeLocked", "Chặng học này đang bị khóa. Bạn phải hoàn thành các chặng học trước để mở khóa."));
        }

        if (node.Status == "SKIPPED_PRUNED")
        {
            return Result<SubmitMilestoneQuizResponseDto>.Failure(
                Error.Validation("Roadmap.NodePruned", "Chặng học này đã được cắt tỉa (pruned) khỏi lộ trình học."));
        }

        // 4. Kiểm tra điều kiện tiên quyết: Học sinh phải xem tối thiểu 80% thời lượng video lý thuyết
        if (!node.IsVideoCompleted)
        {
            return Result<SubmitMilestoneQuizResponseDto>.Failure(
                Error.Validation("Roadmap.VideoIncomplete", "Học sinh cần xem tối thiểu 80% thời lượng video bài giảng lý thuyết trước khi nộp bài Quiz củng cố."));
        }

        // 5. Xác định mã đề QuizExamId (Tự động nạp nếu chưa gán)
        if (!node.QuizExamId.HasValue || node.QuizExamId.Value == Guid.Empty)
        {
            var quiz = await _contentGrpcClient.GetMilestoneQuizAsync(node.SkillId, null, 5, cancellationToken);
            if (quiz == null)
            {
                return Result<SubmitMilestoneQuizResponseDto>.Failure(
                    Error.NotFound("Roadmap.QuizNotFound", $"Không tìm thấy bộ đề Quiz củng cố cho kỹ năng {node.SkillId}."));
            }
            node.QuizExamId = quiz.ExamId;
            await _roadmapRepository.SaveChangesAsync(cancellationToken);
        }

        // 6. Lấy bảng đáp án gốc bảo mật từ Content Service qua gRPC
        var answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(node.QuizExamId.Value, cancellationToken);
        if (answerKeys.Count == 0)
        {
            return Result<SubmitMilestoneQuizResponseDto>.Failure(
                Error.NotFound("Roadmap.AnswerKeysNotFound", $"Không tìm thấy bảng đáp án cho bài Quiz {node.QuizExamId.Value} từ Content Service."));
        }

        // 7. Chấm điểm bài làm chi tiết từng câu
        var answersList = request.Answers ?? new List<MilestoneQuizAnswerItemDto>();
        var studentAnswersMap = answersList.ToDictionary(a => a.QuestionId, a => a);
        var submissionAnswers = new List<SubmissionAnswer>();
        var questionResults = new List<MilestoneQuizQuestionResultDto>();

        int totalCorrect = 0;
        var submissionId = Guid.NewGuid();

        foreach (var key in answerKeys.Values.OrderBy(k => k.QuestionOrder))
        {
            studentAnswersMap.TryGetValue(key.QuestionId, out var studentAns);

            string? selectedOption = studentAns?.SelectedOption?.Trim().ToUpperInvariant();
            int timeSpent = studentAns?.TimeSpentSeconds ?? 0;

            bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                             string.Equals(selectedOption, key.CorrectOption?.Trim().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                totalCorrect++;
            }

            submissionAnswers.Add(new SubmissionAnswer
            {
                AnswerId = Guid.NewGuid(),
                SubmissionId = submissionId,
                QuestionId = key.QuestionId,
                SelectedOption = selectedOption,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent
            });

            questionResults.Add(new MilestoneQuizQuestionResultDto
            {
                QuestionId = key.QuestionId,
                QuestionOrder = key.QuestionOrder,
                SelectedOption = selectedOption,
                CorrectOption = key.CorrectOption ?? string.Empty,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent,
                SkillId = key.SkillId,
                SkillName = key.SkillName
            });
        }

        int totalQuestions = answerKeys.Count;
        double scorePercentage = totalQuestions > 0
            ? Math.Round((totalCorrect / (double)totalQuestions) * 100.0, 2)
            : 0.0;

        bool isPassed = scorePercentage >= 60.0;

        // 8. Lưu lịch sử làm bài vào ExamSubmissions (ExamType = "QUIZ_MILESTONE")
        var submission = new ExamSubmission
        {
            SubmissionId = submissionId,
            StudentId = node.Roadmap.StudentId,
            ExamId = node.QuizExamId.Value,
            ExamType = "QUIZ_MILESTONE",
            TotalScore = totalCorrect,
            TotalCorrect = totalCorrect,
            TotalQuestions = totalQuestions,
            TotalTimeSpentSeconds = request.TotalTimeSpentSeconds,
            StartedAt = DateTime.UtcNow.AddSeconds(-request.TotalTimeSpentSeconds),
            CompletedAt = DateTime.UtcNow,
            Status = "COMPLETED",
            Answers = submissionAnswers
        };

        await _submissionRepository.AddAsync(submission, cancellationToken);
        await _submissionRepository.SaveChangesAsync(cancellationToken);

        // 9. Cập nhật kết quả bài Quiz trên RoadmapNode
        node.QuizScore = scorePercentage;
        node.IsQuizPassed = isPassed;

        Guid? nextUnlockedNodeId = null;
        int? nextUnlockedStepOrder = null;
        string message;

        // 10. Kích hoạt Máy trạng thái hữu hạn (Finite State Machine Unlock)
        if (isPassed)
        {
            // Kiểm tra Invariant 2: Nếu học sinh bị ABSENT buổi Live Q&A, bắt buộc phải vượt qua cả bài Quiz bù
            bool isBlockedByAbsent = false;
            if (node.LiveSessionId.HasValue)
            {
                var attendance = await _roadmapRepository.GetAttendanceAsync(node.LiveSessionId.Value, node.Roadmap.StudentId, cancellationToken);
                if (attendance != null && attendance.AttendanceStatus == "ABSENT" && !attendance.IsMakeupQuizPassed)
                {
                    isBlockedByAbsent = true;
                }
            }

            if (isBlockedByAbsent)
            {
                // Chặng học chưa được hoàn thành vì vắng mặt buổi Live và chưa vượt qua Quiz bù
                message = $"Chúc mừng bạn đã đạt {scorePercentage}% bài Quiz củng cố! Tuy nhiên, do bạn vắng mặt (ABSENT) tại buổi học Live Q&A cơ sở, bạn cần hoàn thành thêm bài Quiz bù (Makeup Quiz) để mở khóa chặng tiếp theo.";
            }
            else
            {
                node.Status = "COMPLETED";
                node.CompletedAt = DateTime.UtcNow;

                // Cập nhật tiến độ Roadmap
                node.Roadmap.CompletedMilestones++;
                if (node.Roadmap.CompletedMilestones >= node.Roadmap.TotalMilestones)
                {
                    node.Roadmap.Status = "COMPLETED";
                }
                node.Roadmap.UpdatedAt = DateTime.UtcNow;

                // Tìm chặng học tiếp theo đang ở trạng thái LOCKED
                var nextNode = await _roadmapRepository.GetNextLockedNodeAsync(node.RoadmapId, node.StepOrder, cancellationToken);
                if (nextNode != null)
                {
                    nextNode.Status = "IN_PROGRESS";
                    nextNode.UnlockedAt = DateTime.UtcNow;
                    nextUnlockedNodeId = nextNode.NodeId;
                    nextUnlockedStepOrder = nextNode.StepOrder;

                    _logger.LogInformation("Roadmap node {NodeId} completed. Unlocked next node {NextNodeId} (Step {StepOrder})",
                        node.NodeId, nextNode.NodeId, nextNode.StepOrder);
                }
                else
                {
                    _logger.LogInformation("Roadmap node {NodeId} completed. All nodes finished for Roadmap {RoadmapId}",
                        node.NodeId, node.RoadmapId);
                }

                message = $"Chúc mừng bạn đã hoàn thành bài Quiz củng cố với điểm số {scorePercentage}% ({totalCorrect}/{totalQuestions} câu đúng)! Chặng học tiếp theo đã được mở khóa.";
            }
        }
        else
        {
            // Điểm < 60%: Giữ nguyên chặng ở IN_PROGRESS, không mở khóa chặng sau
            _logger.LogWarning("Roadmap node {NodeId} quiz failed with score {ScorePercentage}%. Node remains IN_PROGRESS.",
                node.NodeId, scorePercentage);

            message = $"Bạn đạt {scorePercentage}% ({totalCorrect}/{totalQuestions} câu đúng), chưa đạt ngưỡng 60% yêu cầu. Vui lòng xem lại video bài giảng và thực hiện lại bài Quiz củng cố.";
        }

        await _roadmapRepository.SaveChangesAsync(cancellationToken);

        var responseDto = new SubmitMilestoneQuizResponseDto
        {
            NodeId = node.NodeId,
            SubmissionId = submissionId,
            QuizExamId = node.QuizExamId.Value,
            TotalQuestions = totalQuestions,
            TotalCorrect = totalCorrect,
            ScorePercentage = scorePercentage,
            IsPassed = isPassed,
            CurrentNodeStatus = node.Status,
            NextUnlockedNodeId = nextUnlockedNodeId,
            NextUnlockedStepOrder = nextUnlockedStepOrder,
            Message = message,
            QuestionResults = questionResults
        };

        return Result<SubmitMilestoneQuizResponseDto>.Success(responseDto);
    }
}
