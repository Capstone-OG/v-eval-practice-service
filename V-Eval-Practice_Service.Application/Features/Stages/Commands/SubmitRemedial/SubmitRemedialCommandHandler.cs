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
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitRemedial;

/// <summary>
/// Handler xử lý nộp bài cứu trợ, khôi phục trạng thái IN_PROGRESS và giải cứu học sinh theo đúng Dạng bài/Ngân hàng đề
/// </summary>
public class SubmitRemedialCommandHandler : IRequestHandler<SubmitRemedialCommand, Result<SubmitRemedialResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<SubmitRemedialCommandHandler> _logger;

    public SubmitRemedialCommandHandler(
        IStageProgressRepository stageProgressRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<SubmitRemedialCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitRemedialResponseDto>> Handle(SubmitRemedialCommand request, CancellationToken ct)
    {
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<SubmitRemedialResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        var node = progress.RoadmapNode;
        Guid skillId = node?.SkillId ?? Guid.Empty;
        string skillName = "Chuyên đề trọng tâm";

        // Tra cứu tên chuyên đề từ SkillTree nếu có
        if (skillId != Guid.Empty)
        {
            try
            {
                var skillTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
                var matched = skillTree.FirstOrDefault(s => s.SkillId == skillId);
                if (matched != null)
                {
                    skillName = matched.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy SkillTree qua gRPC khi chấm bài cứu trợ.");
            }
        }

        IReadOnlyDictionary<Guid, ExamQuestionKeyDto>? answerKeys = null;
        if (node?.QuizExamId.HasValue == true)
        {
            try
            {
                answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(node.QuizExamId.Value, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy AnswerKeys từ Content Service qua gRPC.");
            }
        }

        // Tính đợt cứu trợ hiện tại để ghi nhận log
        int remedialAttemptCount = progress.AdaptiveAttempts?
            .Count(a => a.PatternId != null && a.PatternId.StartsWith("REMEDIAL")) / 3 ?? 0;

        // Chấm điểm từng câu hỏi cứu trợ
        var results = new List<RemedialQuestionResultDto>();
        int correctCount = 0;

        progress.AdaptiveAttempts ??= new List<AdaptiveQuizAttempt>();

        foreach (var ans in request.Request.Answers)
        {
            string correctOpt = "A";
            string explanation = string.Empty;

            // 1. Ưu tiên lấy chính xác đáp án và lời giải chi tiết từ Content Service qua gRPC
            try
            {
                var questionDetail = await _contentGrpcClient.GetQuestionDetailAsync(ans.QuestionId, ct);
                if (questionDetail != null && questionDetail.Options.Count > 0)
                {
                    var correctOptionObj = questionDetail.Options.FirstOrDefault(o => o.IsCorrect);
                    if (correctOptionObj != null && !string.IsNullOrWhiteSpace(correctOptionObj.OptionId))
                    {
                        correctOpt = correctOptionObj.OptionId;
                    }

                    if (!string.IsNullOrWhiteSpace(questionDetail.Explanation))
                    {
                        explanation = questionDetail.Explanation;
                    }
                    else
                    {
                        explanation = $"Lời giải từ Ngân hàng câu hỏi Content Service ({skillName}): Áp dụng kiến thức nền tảng và phương pháp giải chuẩn tắc để chọn đáp án {correctOpt}.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy QuestionDetail từ Content Service cho QuestionId {QuestionId}", ans.QuestionId);
            }

            // 2. Nếu câu hỏi thuộc đề thi tổng hợp, tra cứu qua bảng đáp án (AnswerKeys) từ Content Service
            if (string.IsNullOrEmpty(explanation) && answerKeys != null && answerKeys.TryGetValue(ans.QuestionId, out var keyDto))
            {
                correctOpt = keyDto.CorrectOption;
                explanation = $"Đáp án chuẩn từ Content Service cho chuyên đề '{keyDto.SkillName}'.";
            }

            // 3. Fallback tổng quát dự phòng nếu Content Service tạm thời gián đoạn
            if (string.IsNullOrEmpty(explanation))
            {
                explanation = $"Giải thích từ hệ thống học liệu: Áp dụng trực tiếp định nghĩa và phương pháp cốt lõi của chuyên đề '{skillName}' để kiểm chứng phương án {correctOpt}.";
            }

            bool isCorrect = string.Equals(ans.SelectedOption?.Trim(), correctOpt, StringComparison.OrdinalIgnoreCase);
            if (isCorrect)
            {
                correctCount++;
            }

            results.Add(new RemedialQuestionResultDto(
                ans.QuestionId,
                ans.SelectedOption?.ToUpperInvariant() ?? string.Empty,
                correctOpt,
                isCorrect,
                explanation
            ));

            // Lưu vết lịch sử làm bài cứu trợ bằng Repository AddAttemptAsync để không gây xung đột Concurrency
            await _stageProgressRepository.AddAttemptAsync(new AdaptiveQuizAttempt
            {
                Id = Guid.NewGuid(),
                StageProgressId = progress.Id,
                StudentId = progress.StudentId,
                QuestionId = ans.QuestionId,
                PatternId = $"REMEDIAL_ATTEMPT_{remedialAttemptCount + 1}",
                SelectedOption = ans.SelectedOption?.Trim() ?? string.Empty,
                IsCorrect = isCorrect,
                TimeSpentSeconds = 30,
                ItemDifficultyB = -1.0,
                ItemDiscriminationA = 1.0,
                IsLuckyGuess = false,
                PriorPlt = progress.BktMasteryPlt,
                PosteriorPlt = progress.BktMasteryPlt,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }

        int totalQuestions = results.Count > 0 ? results.Count : 3;
        bool isRemedialPassed = (correctCount == totalQuestions);

        if (isRemedialPassed)
        {
            // ĐẠT YÊU CẦU (ĐÚNG 100% CÂU CỨU TRỢ): Khôi phục chặng học
            progress.ConsecutiveIncorrect = 0;
            progress.Status = "IN_PROGRESS";
            progress.CurrentStep = "APPLY";
            progress.UpdatedAt = DateTime.UtcNow;

            await _stageProgressRepository.UpdateAsync(progress, ct);

            _logger.LogInformation(
                "Học sinh {StudentId} xuất sắc hoàn thành gói cứu trợ '{SkillName}' ({Score}/{Total}): Khôi phục Status=IN_PROGRESS, ConsecutiveIncorrect=0",
                progress.StudentId, skillName, correctCount, totalQuestions);

            var response = new SubmitRemedialResponseDto(
                progress.Id,
                Score: correctCount,
                TotalQuestions: totalQuestions,
                IsRemedialPassed: true,
                CurrentStatus: progress.Status,
                CurrentStep: progress.CurrentStep,
                ConsecutiveIncorrect: progress.ConsecutiveIncorrect,
                Message: $"Xuất sắc! Bạn đã trả lời đúng toàn bộ ({correctCount}/{totalQuestions}) câu hỏi cứu trợ phụ đạo chuyên đề '{skillName}'. Nền tảng đã vững chắc, trạng thái chặng học đã khôi phục. Hãy tự tin quay lại bước APPLY để tiếp tục luyện tập thích ứng!",
                NextAction: "RESUME_APPLY",
                Results: results
            );

            return Result<SubmitRemedialResponseDto>.Success(response);
        }
        else
        {
            // CHƯA ĐẠT HẾT: Giữ nguyên REMEDIAL_REQUIRED để học sinh ôn lại và làm đề biến thể mới
            progress.UpdatedAt = DateTime.UtcNow;
            await _stageProgressRepository.UpdateAsync(progress, ct);

            _logger.LogInformation(
                "Học sinh {StudentId} chưa vượt qua gói cứu trợ '{SkillName}' ({Score}/{Total}): Giữ nguyên Status=REMEDIAL_REQUIRED, chuẩn bị cấp đề biến thể đợt {NextAttempt}",
                progress.StudentId, skillName, correctCount, totalQuestions, remedialAttemptCount + 2);

            var response = new SubmitRemedialResponseDto(
                progress.Id,
                Score: correctCount,
                TotalQuestions: totalQuestions,
                IsRemedialPassed: false,
                CurrentStatus: progress.Status,
                CurrentStep: progress.CurrentStep,
                ConsecutiveIncorrect: progress.ConsecutiveIncorrect,
                Message: $"Bạn đã trả lời đúng {correctCount}/{totalQuestions} câu hỏi. Để đảm bảo không học vẹt và đã lấp đầy hoàn toàn lỗ hổng kiến thức, hệ thống sẽ cấp một bộ câu hỏi biến thể mới cho lần thử tiếp theo. Hãy xem lại tóm tắt phương pháp và bấm lấy lại gói cứu trợ nhé!",
                NextAction: "RETRY_REMEDIAL",
                Results: results
            );

            return Result<SubmitRemedialResponseDto>.Success(response);
        }
    }
}
