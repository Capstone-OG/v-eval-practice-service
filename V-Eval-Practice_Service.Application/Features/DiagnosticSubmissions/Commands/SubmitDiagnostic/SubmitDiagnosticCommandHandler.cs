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
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Commands.SubmitDiagnostic;

public class SubmitDiagnosticCommandHandler : IRequestHandler<SubmitDiagnosticCommand, Result<SubmitDiagnosticResponseDto>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<SubmitDiagnosticCommandHandler> _logger;

    public SubmitDiagnosticCommandHandler(
        IExamSubmissionRepository submissionRepository,
        IIdentityGrpcClient identityGrpcClient,
        IContentGrpcClient contentGrpcClient,
        ILogger<SubmitDiagnosticCommandHandler> logger)
    {
        _submissionRepository = submissionRepository;
        _identityGrpcClient = identityGrpcClient;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitDiagnosticResponseDto>> Handle(
        SubmitDiagnosticCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing diagnostic submission for Student {StudentId}, Exam {ExamId}",
            request.StudentId, request.ExamId);

        // 1. Kiểm tra học sinh và điều kiện chọn Campus qua Identity gRPC
        var (exists, campusId, targetScore) = await _identityGrpcClient.GetStudentProfileAsync(request.StudentId, cancellationToken);
        if (!exists)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Student.NotFound", $"Không tìm thấy thông tin học sinh {request.StudentId} trong hệ thống."));
        }

        if (string.IsNullOrWhiteSpace(campusId))
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.Validation("Student.CampusRequired", "Học sinh cần chọn cơ sở đào tạo (Campus) trước khi thực hiện bài kiểm tra chẩn đoán năng lực."));
        }

        // 2. Lấy bảng đáp án và metadata từ Content Service gRPC
        var answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(request.ExamId, cancellationToken);
        if (answerKeys.Count == 0)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Exam.NotFound", $"Không tìm thấy bộ đề thi hoặc đề thi {request.ExamId} chưa có câu hỏi."));
        }

        // 3. Tiến hành chấm điểm và ghi nhận vi mô từng câu
        var studentAnswersMap = request.Answers.ToDictionary(a => a.QuestionId, a => a);
        var submissionAnswers = new List<SubmissionAnswer>();
        var questionResults = new List<QuestionResultDto>();

        int totalCorrect = 0;
        int totalTimeSpent = 0;

        var submissionId = Guid.NewGuid();

        foreach (var key in answerKeys.Values.OrderBy(k => k.QuestionOrder))
        {
            studentAnswersMap.TryGetValue(key.QuestionId, out var studentAns);

            string? selectedOption = studentAns?.SelectedOption?.Trim().ToUpperInvariant();
            int timeSpent = studentAns?.TimeSpentSeconds ?? 0;
            totalTimeSpent += timeSpent;

            bool isCorrect = !string.IsNullOrEmpty(selectedOption) &&
                             string.Equals(selectedOption, key.CorrectOption?.Trim().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                totalCorrect++;
            }

            var answerEntity = new SubmissionAnswer
            {
                AnswerId = Guid.NewGuid(),
                SubmissionId = submissionId,
                QuestionId = key.QuestionId,
                SelectedOption = selectedOption,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent
            };
            submissionAnswers.Add(answerEntity);

            questionResults.Add(new QuestionResultDto
            {
                QuestionId = key.QuestionId,
                QuestionOrder = key.QuestionOrder,
                SelectedOption = selectedOption,
                CorrectOption = key.CorrectOption ?? string.Empty,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpent,
                SkillId = key.SkillId,
                DifficultyLevel = key.DifficultyLevel
            });
        }

        int totalQuestions = answerKeys.Count;
        double accuracyPercentage = totalQuestions > 0
            ? Math.Round((totalCorrect / (double)totalQuestions) * 100, 2)
            : 0;

        // 4. Phân tích chẩn đoán theo Kỹ năng (Skill Breakdown & Weak Skills)
        var skillBreakdowns = questionResults
            .GroupBy(q => q.SkillId)
            .Select(g =>
            {
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                bool isWeak = pct < 60.0;
                return new SkillDiagnosticDto
                {
                    SkillId = g.Key,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct,
                    IsWeak = isWeak
                };
            })
            .ToList();

        var weakSkillIds = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => s.SkillId)
            .ToList();

        // 5. Phân tích chẩn đoán theo Độ khó (Difficulty Breakdown)
        var difficultyBreakdowns = questionResults
            .GroupBy(q => q.DifficultyLevel)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                string levelName = g.Key switch
                {
                    1 => "Dễ (Nhận biết)",
                    2 => "Trung bình (Thông hiểu)",
                    3 => "Khó (Vận dụng)",
                    4 => "Rất khó (Vận dụng cao)",
                    _ => $"Mức {g.Key}"
                };

                return new DifficultyBreakdownDto
                {
                    DifficultyLevel = g.Key,
                    LevelName = levelName,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct
                };
            })
            .ToList();

        // 6. Lưu trữ vào CSDL Supabase schema practice
        var submission = new ExamSubmission
        {
            SubmissionId = submissionId,
            StudentId = request.StudentId,
            ExamId = request.ExamId,
            ExamType = "DIAGNOSTIC",
            TotalScore = totalCorrect, // Thang điểm 0 - 30
            TotalCorrect = totalCorrect,
            TotalQuestions = totalQuestions,
            TotalTimeSpentSeconds = totalTimeSpent,
            StartedAt = request.StartedAt ?? DateTime.UtcNow.AddSeconds(-totalTimeSpent),
            CompletedAt = DateTime.UtcNow,
            Status = "COMPLETED",
            Answers = submissionAnswers
        };

        await _submissionRepository.AddAsync(submission, cancellationToken);
        await _submissionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Diagnostic submission {SubmissionId} saved successfully. Score: {Score}/{Total}",
            submissionId, totalCorrect, totalQuestions);

        // 7. Tạo DTO phản hồi hoàn chỉnh cho Client và AI Subsystem
        var responseDto = new SubmitDiagnosticResponseDto
        {
            SubmissionId = submission.SubmissionId,
            StudentId = submission.StudentId,
            ExamId = submission.ExamId,
            ExamType = submission.ExamType,
            TotalScore = submission.TotalScore,
            TotalCorrect = submission.TotalCorrect,
            TotalQuestions = submission.TotalQuestions,
            AccuracyPercentage = accuracyPercentage,
            TotalTimeSpentSeconds = submission.TotalTimeSpentSeconds,
            StartedAt = submission.StartedAt,
            CompletedAt = submission.CompletedAt,
            SkillBreakdowns = skillBreakdowns,
            WeakSkillIds = weakSkillIds,
            DifficultyBreakdowns = difficultyBreakdowns,
            Questions = questionResults
        };

        return Result<SubmitDiagnosticResponseDto>.Success(responseDto);
    }
}
