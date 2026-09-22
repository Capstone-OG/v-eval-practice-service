using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionById;

public record GetDiagnosticSubmissionByIdQuery(Guid SubmissionId)
    : IRequest<Result<SubmitDiagnosticResponseDto>>;

public class GetDiagnosticSubmissionByIdHandler
    : IRequestHandler<GetDiagnosticSubmissionByIdQuery, Result<SubmitDiagnosticResponseDto>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IContentGrpcClient _contentGrpcClient;

    public GetDiagnosticSubmissionByIdHandler(
        IExamSubmissionRepository submissionRepository,
        IContentGrpcClient contentGrpcClient)
    {
        _submissionRepository = submissionRepository;
        _contentGrpcClient = contentGrpcClient;
    }

    public async Task<Result<SubmitDiagnosticResponseDto>> Handle(
        GetDiagnosticSubmissionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy bài nộp có ID {request.SubmissionId}."));
        }

        // Lấy bảng đáp án từ Content Service để làm giàu dữ liệu chi tiết
        var answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(submission.ExamId, cancellationToken);

        var questionResults = submission.Answers
            .Select(a =>
            {
                answerKeys.TryGetValue(a.QuestionId, out var key);
                return new QuestionResultDto
                {
                    QuestionId = a.QuestionId,
                    QuestionOrder = key?.QuestionOrder ?? 0,
                    SelectedOption = a.SelectedOption,
                    CorrectOption = key?.CorrectOption ?? string.Empty,
                    IsCorrect = a.IsCorrect,
                    TimeSpentSeconds = a.TimeSpentSeconds,
                    SkillId = key?.SkillId ?? string.Empty,
                    SkillName = key?.SkillName ?? string.Empty,
                    DomainId = key?.DomainId ?? string.Empty,
                    DomainName = key?.DomainName ?? string.Empty,
                    DifficultyLevel = key?.DifficultyLevel ?? 0
                };
            })
            .OrderBy(q => q.QuestionOrder)
            .ToList();

        // Phân tích Kỹ năng
        var skillBreakdowns = questionResults
            .Where(q => !string.IsNullOrEmpty(q.SkillId))
            .GroupBy(q => q.SkillId)
            .Select(g =>
            {
                var first = g.First();
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                return new SkillDiagnosticDto
                {
                    SkillId = g.Key,
                    SkillName = first.SkillName,
                    DomainId = first.DomainId,
                    DomainName = first.DomainName,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct,
                    IsWeak = pct < 60.0
                };
            })
            .ToList();

        var weakSkillIds = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => s.SkillId)
            .ToList();

        var weakSkills = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => new WeakSkillDto
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName,
                DomainName = s.DomainName,
                TotalQuestions = s.TotalQuestions,
                CorrectCount = s.CorrectCount,
                AccuracyPercentage = s.AccuracyPercentage
            })
            .ToList();

        // Phân tích Độ khó
        var difficultyBreakdowns = questionResults
            .Where(q => q.DifficultyLevel > 0)
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

        double accuracy = submission.TotalQuestions > 0
            ? Math.Round((submission.TotalCorrect / (double)submission.TotalQuestions) * 100, 2)
            : 0;

        var dto = new SubmitDiagnosticResponseDto
        {
            SubmissionId = submission.SubmissionId,
            StudentId = submission.StudentId,
            ExamId = submission.ExamId,
            ExamType = submission.ExamType,
            TotalScore = submission.TotalScore,
            TotalCorrect = submission.TotalCorrect,
            TotalQuestions = submission.TotalQuestions,
            AccuracyPercentage = accuracy,
            TotalTimeSpentSeconds = submission.TotalTimeSpentSeconds,
            StartedAt = submission.StartedAt,
            CompletedAt = submission.CompletedAt,
            SkillBreakdowns = skillBreakdowns,
            WeakSkillIds = weakSkillIds,
            WeakSkills = weakSkills,
            DifficultyBreakdowns = difficultyBreakdowns,
            Questions = questionResults,
            Theta0 = submission.Theta0 ?? 0.0,
            PlacementClass = submission.PlacementClass ?? "ACCELERATION",
            ClassId = submission.EnrolledClassId,
            AiCommentary = submission.AiCommentary ?? string.Empty
        };

        return Result<SubmitDiagnosticResponseDto>.Success(dto);
    }
}
