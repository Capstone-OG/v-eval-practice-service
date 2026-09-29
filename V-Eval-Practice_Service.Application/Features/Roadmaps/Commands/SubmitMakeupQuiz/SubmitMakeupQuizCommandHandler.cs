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

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.SubmitMakeupQuiz;

public class SubmitMakeupQuizCommandHandler : IRequestHandler<SubmitMakeupQuizCommand, Result<SubmitMakeupQuizResponseDto>>
{
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<SubmitMakeupQuizCommandHandler> _logger;

    public SubmitMakeupQuizCommandHandler(
        ILearningRoadmapRepository roadmapRepository,
        IExamSubmissionRepository submissionRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<SubmitMakeupQuizCommandHandler> logger)
    {
        _roadmapRepository = roadmapRepository;
        _submissionRepository = submissionRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitMakeupQuizResponseDto>> Handle(
        SubmitMakeupQuizCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra sự tồn tại của chặng học (Node)
        var node = await _roadmapRepository.GetNodeByIdAsync(request.NodeId, cancellationToken);
        if (node == null)
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.NotFound("Roadmap.NodeNotFound", $"Không tìm thấy chặng học {request.NodeId}."));
        }

        // 2. Kiểm tra phân quyền sở hữu chặng học
        if (request.StudentId.HasValue && request.StudentId.Value != Guid.Empty &&
            node.Roadmap.StudentId != request.StudentId.Value)
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Forbidden("Roadmap.Forbidden", "Bạn không có quyền nộp bài cho chặng học của học sinh khác."));
        }

        // 3. Kiểm tra trạng thái máy của chặng học
        if (node.Status == "LOCKED")
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Validation("Roadmap.NodeLocked", "Chặng học này đang bị khóa. Bạn phải hoàn thành các chặng học trước để mở khóa."));
        }

        if (node.Status == "SKIPPED_PRUNED")
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Validation("Roadmap.NodePruned", "Chặng học này đã được cắt tỉa (pruned) khỏi lộ trình học."));
        }

        // 4. Kiểm tra điều kiện tiên quyết xem video lý thuyết
        if (!node.IsVideoCompleted)
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Validation("Roadmap.VideoIncomplete", "Học sinh cần xem tối thiểu 80% thời lượng video bài giảng lý thuyết trước khi nộp bài Quiz bù."));
        }

        // 5. Kiểm tra liên kết buổi học Live Q&A và trạng thái vắng mặt (Unhappy Case 3 - Absenteeism Fallback)
        if (!node.LiveSessionId.HasValue || node.LiveSessionId.Value == Guid.Empty)
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Validation("Roadmap.NoLiveSession", "Chặng học này không liên kết với buổi học Live Q&A nào."));
        }

        var attendance = await _roadmapRepository.GetAttendanceAsync(
            node.LiveSessionId.Value,
            node.Roadmap.StudentId,
            cancellationToken);

        if (attendance == null || !string.Equals(attendance.AttendanceStatus, "ABSENT", StringComparison.OrdinalIgnoreCase))
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Validation("Roadmap.NotAbsent", "Học sinh không thuộc diện vắng mặt (ABSENT) buổi học Live Q&A nên không cần làm Quiz bù."));
        }

        if (attendance.IsMakeupQuizPassed)
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.Validation("Roadmap.MakeupAlreadyPassed", "Học sinh đã hoàn thành và vượt qua bài Quiz bù trước đó."));
        }

        // 6. Xác định mã đề MakeupQuizId (Tự động nạp đề 5 câu từ Content Service nếu chưa có)
        if (!attendance.MakeupQuizId.HasValue || attendance.MakeupQuizId.Value == Guid.Empty)
        {
            var quiz = await _contentGrpcClient.GetMilestoneQuizAsync(node.SkillId, null, 5, cancellationToken);
            if (quiz == null)
            {
                return Result<SubmitMakeupQuizResponseDto>.Failure(
                    Error.NotFound("Roadmap.QuizNotFound", $"Không tìm thấy bộ đề Quiz bù cho kỹ năng {node.SkillId}."));
            }
            attendance.MakeupQuizId = quiz.ExamId;
            await _roadmapRepository.SaveChangesAsync(cancellationToken);
        }

        // 7. Lấy bảng đáp án gốc bảo mật từ Content Service qua gRPC
        var answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(attendance.MakeupQuizId.Value, cancellationToken);
        if (answerKeys.Count == 0)
        {
            return Result<SubmitMakeupQuizResponseDto>.Failure(
                Error.NotFound("Roadmap.AnswerKeysNotFound", $"Không tìm thấy bảng đáp án cho bài Quiz bù {attendance.MakeupQuizId.Value} từ Content Service."));
        }

        // 8. Chấm điểm bài Quiz bù chi tiết từng câu
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

        // 9. Lưu bản ghi nộp bài vào ExamSubmissions (ExamType = "MAKEUP_QUIZ")
        var submission = new ExamSubmission
        {
            SubmissionId = submissionId,
            StudentId = node.Roadmap.StudentId,
            ExamId = attendance.MakeupQuizId.Value,
            ExamType = "MAKEUP_QUIZ",
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

        // 10. Cập nhật kết quả bài Quiz bù trên LiveSessionAttendance
        attendance.IsMakeupQuizPassed = isPassed;

        Guid? nextUnlockedNodeId = null;
        int? nextUnlockedStepOrder = null;
        string message;

        // 11. Kích hoạt State Machine: Gỡ phong tỏa chặng nếu cả Quiz củng cố & Quiz bù đều pass
        if (isPassed)
        {
            if (node.IsQuizPassed)
            {
                // Học sinh đã vượt qua cả bài Quiz củng cố VÀ bài Quiz bù
                node.Status = "COMPLETED";
                node.CompletedAt = DateTime.UtcNow;

                // Cập nhật tiến độ Roadmap
                node.Roadmap.CompletedMilestones++;
                if (node.Roadmap.CompletedMilestones >= node.Roadmap.TotalMilestones)
                {
                    node.Roadmap.Status = "COMPLETED";
                }
                node.Roadmap.UpdatedAt = DateTime.UtcNow;

                // Mở khóa chặng kế tiếp
                var nextNode = await _roadmapRepository.GetNextLockedNodeAsync(node.RoadmapId, node.StepOrder, cancellationToken);
                if (nextNode != null)
                {
                    nextNode.Status = "IN_PROGRESS";
                    nextNode.UnlockedAt = DateTime.UtcNow;
                    nextUnlockedNodeId = nextNode.NodeId;
                    nextUnlockedStepOrder = nextNode.StepOrder;

                    _logger.LogInformation("Roadmap node {NodeId} fully completed after Makeup Quiz. Unlocked next node {NextNodeId} (Step {StepOrder})",
                        node.NodeId, nextNode.NodeId, nextNode.StepOrder);
                }

                message = $"Chúc mừng bạn đã hoàn thành bài Quiz bù với {scorePercentage}% điểm ({totalCorrect}/{totalQuestions} câu đúng)! Điều kiện phong tỏa do vắng mặt đã được gỡ bỏ và chặng tiếp theo đã được mở khóa.";
            }
            else
            {
                // Vượt qua Quiz bù nhưng chưa làm/chưa pass Quiz củng cố chuyên đề
                message = $"Chúc mừng bạn đã vượt qua bài Quiz bù với {scorePercentage}% điểm! Vui lòng hoàn thành thêm bài Quiz củng cố chuyên đề để hoàn tất chặng học.";
            }
        }
        else
        {
            // Trượt bài Quiz bù (< 60%)
            _logger.LogWarning("Roadmap node {NodeId} makeup quiz failed with score {ScorePercentage}%.",
                node.NodeId, scorePercentage);

            message = $"Bạn đạt {scorePercentage}% điểm ({totalCorrect}/{totalQuestions} câu đúng) bài Quiz bù, chưa đạt ngưỡng 60% yêu cầu. Vui lòng xem lại video ghi hình buổi Live và thực hiện lại bài Quiz bù.";
        }

        await _roadmapRepository.SaveChangesAsync(cancellationToken);

        var responseDto = new SubmitMakeupQuizResponseDto
        {
            NodeId = node.NodeId,
            SessionId = node.LiveSessionId.Value,
            AttendanceId = attendance.AttendanceId,
            SubmissionId = submissionId,
            MakeupQuizId = attendance.MakeupQuizId.Value,
            TotalQuestions = totalQuestions,
            TotalCorrect = totalCorrect,
            ScorePercentage = scorePercentage,
            IsPassed = isPassed,
            IsMakeupQuizPassed = attendance.IsMakeupQuizPassed,
            CurrentNodeStatus = node.Status,
            NextUnlockedNodeId = nextUnlockedNodeId,
            NextUnlockedStepOrder = nextUnlockedStepOrder,
            Message = message,
            QuestionResults = questionResults
        };

        return Result<SubmitMakeupQuizResponseDto>.Success(responseDto);
    }
}
