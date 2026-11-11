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

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.StartMockExam;

public class StartMockExamCommandHandler : IRequestHandler<StartMockExamCommand, Result<StartMockExamResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<StartMockExamCommandHandler> _logger;

    public StartMockExamCommandHandler(
        IMockExamRepository mockExamRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<StartMockExamCommandHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<StartMockExamResponseDto>> Handle(
        StartMockExamCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Khởi tạo phiên thi thử Mock Exam cho Học sinh {StudentId}, Đề thi {ExamId}",
            request.StudentId, request.ExamId);

        // 1. Kiểm tra phiên thi đang tiến hành (IN_PROGRESS)
        var existingActive = await _mockExamRepository.GetActiveSubmissionAsync(
            request.StudentId, request.ExamId, cancellationToken);

        // 2. Nạp câu hỏi từ Content Service qua gRPC
        var questions = new List<MockExamQuestionDto>();
        string examTitle = "Đề Thi Thử Đánh Giá Năng Lực Chuẩn Hóa";
        int durationMinutes = 150;

        try
        {
            var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(
                skillId: Guid.Empty,
                examId: request.ExamId,
                questionCount: 120,
                ct: cancellationToken);

            if (quizResult != null && quizResult.Questions != null && quizResult.Questions.Count > 0)
            {
                examTitle = quizResult.Title;
                durationMinutes = quizResult.DurationMinutes > 0 ? quizResult.DurationMinutes : 150;

                questions = quizResult.Questions.Select(q => new MockExamQuestionDto(
                    QuestionId: q.QuestionId,
                    QuestionOrder: q.QuestionOrder,
                    Content: q.Content,
                    Options: q.Options.Select(o => new MockExamQuestionOptionDto(o.OptionId, o.Content)).ToList(),
                    DifficultyLevel: q.DifficultyLevel,
                    SkillId: q.SkillId,
                    SkillName: q.SkillName
                )).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối gRPC Content Service để nạp đề thi {ExamId}. Kích hoạt chế độ fallback mẫu.", request.ExamId);
        }

        // Cơ chế Fallback an toàn nếu chưa có ngân hàng đề thật từ Content Service
        if (questions.Count == 0)
        {
            questions = GenerateFallbackMockQuestions(request.ExamId);
        }

        // Nếu đã có bài thi đang làm dở và chưa hết 150 phút -> Cho phép tiếp tục làm tiếp
        if (existingActive != null)
        {
            var elapsedMinutes = (DateTime.UtcNow - existingActive.StartedAt).TotalMinutes;
            if (elapsedMinutes < durationMinutes)
            {
                _logger.LogInformation("Khôi phục phiên thi đang làm dở (SubmissionId: {SubmissionId}) cho học sinh {StudentId}",
                    existingActive.SubmissionId, request.StudentId);

                return Result<StartMockExamResponseDto>.Success(new StartMockExamResponseDto(
                    SubmissionId: existingActive.SubmissionId,
                    ExamId: existingActive.ExamId,
                    StudentId: existingActive.StudentId,
                    ExamTitle: examTitle,
                    DurationMinutes: (int)Math.Max(1, durationMinutes - elapsedMinutes),
                    TotalQuestions: existingActive.TotalQuestions > 0 ? existingActive.TotalQuestions : questions.Count,
                    StartedAt: existingActive.StartedAt,
                    Status: existingActive.Status,
                    Questions: questions
                ));
            }
        }

        // 3. Khởi tạo bản ghi ExamSubmission mới cho phòng thi thử
        var submission = new ExamSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = request.StudentId,
            ExamId = request.ExamId,
            ExamType = "MOCK_EXAM",
            TotalScore = 0,
            TotalCorrect = 0,
            TotalQuestions = questions.Count,
            TotalTimeSpentSeconds = 0,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddMinutes(durationMinutes),
            Status = "IN_PROGRESS",
            TabDepartureCount = 0,
            IntegrityStatus = "NORMAL",
            MentorVerdict = "PENDING"
        };

        await _mockExamRepository.AddAsync(submission, cancellationToken);
        await _mockExamRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Đã khởi tạo thành công phiên thi thử {SubmissionId} với {Count} câu hỏi",
            submission.SubmissionId, questions.Count);

        var response = new StartMockExamResponseDto(
            SubmissionId: submission.SubmissionId,
            ExamId: submission.ExamId,
            StudentId: submission.StudentId,
            ExamTitle: examTitle,
            DurationMinutes: durationMinutes,
            TotalQuestions: questions.Count,
            StartedAt: submission.StartedAt,
            Status: submission.Status,
            Questions: questions
        );

        return Result<StartMockExamResponseDto>.Success(response);
    }

    private static List<MockExamQuestionDto> GenerateFallbackMockQuestions(Guid examId)
    {
        var list = new List<MockExamQuestionDto>();
        for (int i = 1; i <= 10; i++)
        {
            list.Add(new MockExamQuestionDto(
                QuestionId: Guid.NewGuid(),
                QuestionOrder: i,
                Content: $"[Câu hỏi mô phỏng {i}] Một hộp đựng 5 viên bi đỏ và 3 viên bi xanh. Lấy ngẫu nhiên 2 viên bi. Xác suất để lấy được ít nhất 1 viên bi đỏ là bao nhiêu?",
                Options: new List<MockExamQuestionOptionDto>
                {
                    new("A", "13/14"),
                    new("B", "25/28"),
                    new("C", "15/28"),
                    new("D", "3/28")
                },
                DifficultyLevel: i <= 3 ? 1 : (i <= 7 ? 2 : 3),
                SkillId: Guid.Parse("6f3765db-943e-4810-bf74-d6a8bdc215da"),
                SkillName: "Xác suất & Tư duy Toán học"
            ));
        }
        return list;
    }
}
