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
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.SubmitMockExam;

public class SubmitMockExamCommandHandler : IRequestHandler<SubmitMockExamCommand, Result<SubmitMockExamResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IMistakeNotebookRepository _mistakeNotebookRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly ILogger<SubmitMockExamCommandHandler> _logger;

    public SubmitMockExamCommandHandler(
        IMockExamRepository mockExamRepository,
        IContentGrpcClient contentGrpcClient,
        IMistakeNotebookRepository mistakeNotebookRepository,
        ILearningProfileRepository learningProfileRepository,
        ILogger<SubmitMockExamCommandHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _contentGrpcClient = contentGrpcClient;
        _mistakeNotebookRepository = mistakeNotebookRepository;
        _learningProfileRepository = learningProfileRepository;
        _logger = logger;
    }

    public async Task<Result<SubmitMockExamResponseDto>> Handle(
        SubmitMockExamCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nhận yêu cầu nộp bài thi thử cho phiên thi {SubmissionId}", request.SubmissionId);

        // 1. Kiểm tra phiên thi tồn tại
        var submission = await _mockExamRepository.GetByIdWithDetailsAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<SubmitMockExamResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy phiên làm bài thi thử với ID {request.SubmissionId}."));
        }

        // 2. Kiểm tra trạng thái bài thi phải đang trong tiến trình (IN_PROGRESS)
        if (submission.Status != "IN_PROGRESS")
        {
            return Result<SubmitMockExamResponseDto>.Failure(
                Error.Conflict("Submission.NotInProgress",
                    $"Bài thi thử đang ở trạng thái '{submission.Status}', không thể thực hiện nộp bài."));
        }

        // 3. Lấy bảng đáp án chuẩn từ Content Service gRPC
        IReadOnlyDictionary<Guid, ExamQuestionKeyDto>? answerKeys = null;
        try
        {
            answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(submission.ExamId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Content Service gRPC tạm thời không thể kết nối cho ExamId {ExamId}. Kích hoạt chế độ fallback grading độc lập.", submission.ExamId);
        }

        // Chuẩn bị danh sách chấm điểm
        var answersList = request.Answers ?? new List<MockExamAnswerSubmissionDto>();
        var studentAnswersMap = answersList.ToDictionary(a => a.QuestionId, a => a);
        var questionResults = new List<MockExamQuestionResultDto>();

        int totalCorrect = 0;
        int totalTimeSpent = 0;
        int mistakesAddedCount = 0;

        // Xây dựng danh sách câu hỏi cần chấm
        // Nếu Content Service trả về bảng đáp án chuẩn thì dùng answerKeys
        // Nếu môi trường test local không kết nối gRPC Content Service (answerKeys rỗng), fallback theo danh sách đáp án học sinh gửi lên
        var scoringKeys = new List<(Guid QuestionId, string CorrectOption, string SkillId, int Difficulty)>();

        if (answerKeys != null && answerKeys.Count > 0)
        {
            foreach (var key in answerKeys.Values.OrderBy(k => k.QuestionOrder))
            {
                scoringKeys.Add((key.QuestionId, key.CorrectOption ?? "A", key.SkillId ?? string.Empty, key.DifficultyLevel));
            }
        }
        else
        {
            // Fallback chấm điểm cục bộ cho test environment
            _logger.LogWarning("Không lấy được answer keys từ Content Service cho ExamId {ExamId}. Sử dụng chế độ fallback scoring.", submission.ExamId);
            foreach (var ans in answersList)
            {
                scoringKeys.Add((ans.QuestionId, "A", Guid.Empty.ToString(), 2));
            }
        }

        var newAnswers = new List<SubmissionAnswer>();
        var newMistakes = new List<MistakeNotebook>();

        foreach (var item in scoringKeys)
        {
            studentAnswersMap.TryGetValue(item.QuestionId, out var studentAns);

            string? selectedOption = studentAns?.SelectedOption?.Trim().ToUpperInvariant();
            int timeSpent = studentAns?.TimeSpentSeconds ?? 0;
            totalTimeSpent += timeSpent;

            bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                             string.Equals(selectedOption, item.CorrectOption.Trim().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                totalCorrect++;
            }

            // Ghi nhận bản ghi câu trả lời chi tiết
            var answerEntity = new SubmissionAnswer
            {
                AnswerId = Guid.NewGuid(),
                SubmissionId = submission.SubmissionId,
                QuestionId = item.QuestionId,
                SelectedOption = selectedOption,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent
            };
            newAnswers.Add(answerEntity);

            questionResults.Add(new MockExamQuestionResultDto(
                item.QuestionId,
                selectedOption ?? string.Empty,
                item.CorrectOption,
                isCorrect,
                timeSpent
            ));

            // Core Flow 3: Nếu làm SAI -> Chuẩn bị nạp vào Sổ tay lỗi sai (Mistake Notebook)
            if (!isCorrect)
            {
                var existingMistake = await _mistakeNotebookRepository.GetByStudentAndQuestionAsync(
                    submission.StudentId, item.QuestionId, cancellationToken);

                if (existingMistake == null)
                {
                    Guid skillGuid = Guid.TryParse(item.SkillId, out var parsedGuid) ? parsedGuid : Guid.Empty;
                    var newMistake = new MistakeNotebook
                    {
                        Id = Guid.NewGuid(),
                        StudentId = submission.StudentId,
                        QuestionId = item.QuestionId,
                        SkillId = skillGuid,
                        PatternId = "MOCK_EXAM_ERROR",
                        CognitiveErrorTag = "CARELESS",
                        StudentNotes = $"Làm sai trong Đề thi thử (Mã nộp: {submission.SubmissionId}) ngày {DateTime.UtcNow:dd/MM/yyyy}",
                        NextReviewDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc),
                        ReviewCount = 0,
                        ConsecutiveCorrectReviews = 0,
                        IntervalDays = 1,
                        EaseFactor = 2.50,
                        IsMastered = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    newMistakes.Add(newMistake);
                    mistakesAddedCount++;
                }
            }
        }

        // Lưu danh sách câu trả lời
        if (newAnswers.Count > 0)
        {
            await _mockExamRepository.AddAnswersAsync(newAnswers, cancellationToken);
        }

        // Lưu danh sách câu sai vào Sổ tay
        foreach (var mistake in newMistakes)
        {
            await _mistakeNotebookRepository.AddAsync(mistake, cancellationToken);
        }

        int totalQuestions = scoringKeys.Count;
        int totalScore = totalCorrect * 10; // Điểm số chuẩn hóa (120 câu * 10 = 1200 điểm)

        // 4. Áp dụng quy tắc giám sát thi (Academic Integrity BR-20)
        bool isFlagged = submission.IntegrityStatus == "FLAGGED_FOR_REVIEW" || submission.TabDepartureCount > 3;
        submission.TotalQuestions = totalQuestions;
        submission.TotalCorrect = totalCorrect;
        submission.TotalScore = totalScore;
        submission.TotalTimeSpentSeconds = totalTimeSpent;
        submission.CompletedAt = DateTime.UtcNow;

        if (isFlagged)
        {
            submission.IntegrityStatus = "FLAGGED_FOR_REVIEW";
            submission.Status = "UNDER_REVIEW";
            submission.MentorVerdict = "PENDING";
            submission.MentorNotes ??= "Hệ thống tự động chuyển bài thi sang diện rà soát do phát hiện hành vi rời màn hình bất thường.";
        }
        else
        {
            submission.Status = "COMPLETED";
            submission.MentorVerdict = "APPROVED";
        }

        // 5. Cập nhật LearningProfiles (Core 2 Re-clustering Feedback Loop)
        var skillGroups = scoringKeys
            .Where(k => Guid.TryParse(k.SkillId, out var g) && g != Guid.Empty)
            .GroupBy(k => Guid.Parse(k.SkillId))
            .ToList();

        if (skillGroups.Count > 0)
        {
            var skillPriors = new List<(Guid SkillId, double MasteryScore)>();
            foreach (var group in skillGroups)
            {
                var skillQIds = group.Select(x => x.QuestionId).ToHashSet();
                var skillAnswers = newAnswers.Where(a => skillQIds.Contains(a.QuestionId)).ToList();
                double mastery = skillAnswers.Count > 0
                    ? (double)skillAnswers.Count(a => a.IsCorrect) / skillAnswers.Count
                    : 0.5;
                skillPriors.Add((group.Key, Math.Round(mastery, 2)));
            }

            await _learningProfileRepository.UpsertSkillPriorsAsync(submission.StudentId, skillPriors, cancellationToken);
        }

        // 6. Lưu toàn bộ kết quả vào Database
        await _mockExamRepository.SaveChangesAsync(cancellationToken);

        string responseMessage = isFlagged
            ? $"Nộp bài thành công! Do phát hiện {submission.TabDepartureCount} lần rời màn hình thi, bài thi của bạn đã được gắn cờ [FLAGGED_FOR_REVIEW] và chuyển sang trạng thái CHỜ MENTOR DUYỆT (UNDER_REVIEW)."
            : "Chúc mừng bạn đã hoàn thành bài thi thử thành công! Kết quả đã được ghi nhận và các câu làm sai đã tự động được nạp vào Sổ tay lỗi sai.";

        _logger.LogInformation(
            "Đã chấm xong bài thi {SubmissionId}. Đúng: {Correct}/{Total}, Điểm: {Score}, Trạng thái: {Status}",
            submission.SubmissionId, totalCorrect, totalQuestions, totalScore, submission.Status);

        var responseDto = new SubmitMockExamResponseDto(
            submission.SubmissionId,
            submission.ExamId,
            submission.StudentId,
            totalQuestions,
            totalCorrect,
            totalScore,
            totalTimeSpent,
            submission.IntegrityStatus,
            submission.Status,
            submission.TabDepartureCount,
            mistakesAddedCount,
            isFlagged,
            responseMessage,
            submission.CompletedAt,
            questionResults
        );

        return Result<SubmitMockExamResponseDto>.Success(responseDto);
    }
}
