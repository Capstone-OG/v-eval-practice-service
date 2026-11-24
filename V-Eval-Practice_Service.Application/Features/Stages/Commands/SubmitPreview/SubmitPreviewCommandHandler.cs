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

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitPreview;

public class SubmitPreviewCommandHandler : IRequestHandler<SubmitPreviewCommand, Result<SubmitPreviewResponseDto>>
{
    private const string DefaultCampusId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string DefaultCampusName = "Cơ sở 1: ĐHQG TP.HCM";

    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly Application.Common.Interfaces.Services.IClassHeatmapAggregatorService _aggregatorService;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<SubmitPreviewCommandHandler> _logger;

    public SubmitPreviewCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILearningProfileRepository learningProfileRepository,
        IClassEnrollmentRepository classEnrollmentRepository,
        Application.Common.Interfaces.Services.IClassHeatmapAggregatorService aggregatorService,
        IContentGrpcClient contentGrpcClient,
        ILogger<SubmitPreviewCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _learningProfileRepository = learningProfileRepository;
        _classEnrollmentRepository = classEnrollmentRepository;
        _aggregatorService = aggregatorService;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitPreviewResponseDto>> Handle(SubmitPreviewCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Xử lý nộp bài khởi động & đánh giá nhanh Preview cho tiến trình {StageProgressId}", request.StageProgressId);

        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<SubmitPreviewResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        // Chấm điểm THẬT qua Content Service gRPC và đánh giá lại năng lực cho các chặng chưa đánh giá (UNASSESSED)
        int totalQuestions = request.Answers.Count;
        int totalCorrect = 0;
        var feedbacks = new List<PreviewQuestionFeedbackDto>();
        var savedPreviewQuestions = new List<object>();
        var savedPreviewAnswers = new Dictionary<string, string>();
        string resolvedDomainCode = "DOM_MATH";
        string resolvedSkillName = string.Empty;
        int questionOrder = 1;

        foreach (var ans in request.Answers)
        {
            string selected = (ans.SelectedOption ?? string.Empty).Trim().ToUpperInvariant();
            string correctOpt = string.Empty;
            string explanation = string.Empty;
            bool isCorrect = false;
            QuestionDetailDto? detail = null;

            try
            {
                detail = await _contentGrpcClient.GetQuestionDetailAsync(ans.QuestionId, ct);
                if (detail != null && detail.Options.Count > 0)
                {
                    correctOpt = detail.Options.FirstOrDefault(o => o.IsCorrect)?.OptionId ?? string.Empty;
                    explanation = detail.Explanation ?? string.Empty;
                    isCorrect = !string.IsNullOrEmpty(correctOpt)
                        && string.Equals(selected, correctOpt, StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(detail.DomainCode))
                    {
                        resolvedDomainCode = detail.DomainCode;
                    }
                    if (!string.IsNullOrWhiteSpace(detail.Title))
                    {
                        resolvedSkillName = detail.Title;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể chấm chi tiết câu Preview {QuestionId} qua gRPC.", ans.QuestionId);
            }

            if (isCorrect) totalCorrect++;
            savedPreviewAnswers[ans.QuestionId.ToString()] = selected;
            savedPreviewQuestions.Add(new
            {
                questionId = ans.QuestionId,
                questionOrder = questionOrder++,
                content = detail?.Content ?? string.Empty,
                options = (detail?.Options ?? Array.Empty<QuestionDetailOptionDto>())
                    .Select(o => new { optionId = o.OptionId, content = o.Content })
                    .ToList(),
                difficultyLevel = 2,
                skillId = detail?.SkillId ?? (progress.RoadmapNode?.SkillId ?? Guid.Empty),
                skillName = detail?.Title ?? resolvedSkillName
            });
            feedbacks.Add(new PreviewQuestionFeedbackDto(
                ans.QuestionId,
                selected,
                correctOpt,
                isCorrect,
                explanation
            ));
        }

        // Tra cứu MaterialId, VideoUrl, SkillName và DomainCode thật từ Content Service
        Guid? materialId = progress.RoadmapNode?.MaterialId;
        string? videoUrl = null;
        Guid skillId = progress.RoadmapNode?.SkillId ?? Guid.Empty;

        if (skillId != Guid.Empty)
        {
            try
            {
                var skills = await _contentGrpcClient.GetSkillsTreeAsync(ct);
                var matched = skills.FirstOrDefault(s => s.SkillId == skillId);
                if (matched != null)
                {
                    materialId ??= matched.MaterialId;
                    if (!string.IsNullOrWhiteSpace(matched.VideoUrl))
                    {
                        videoUrl = matched.VideoUrl;
                    }
                    if (!string.IsNullOrWhiteSpace(matched.Name))
                    {
                        resolvedSkillName = matched.Name;
                    }
                    if (!string.IsNullOrWhiteSpace(matched.DomainCode))
                    {
                        resolvedDomainCode = matched.DomainCode;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lấy Material/VideoUrl thật từ Content Service cho Skill {SkillId}.", skillId);
            }
        }

        // Đánh giá lại năng lực thực tế từ kết quả bài test nhanh Preview:
        // Nếu học sinh làm đạt điểm tốt (3/3 câu đúng hoặc >= 70%) -> đánh giá đạt chuẩn OK (P(Lt) >= 0.78 -> MASTERED)
        // Nếu chưa đạt (< 70%) -> ghi nhận P(Lt) thực tế vào vùng cần cải thiện (WEAK)
        if (totalQuestions > 0)
        {
            double accuracy = (double)totalCorrect / totalQuestions;
            double evaluatedPlt = accuracy >= 0.70
                ? Math.Max(progress.BktMasteryPlt, Math.Round(Math.Clamp(0.75 + (accuracy - 0.70) * 0.30, 0.75, 0.85), 4))
                : Math.Round(Math.Clamp(accuracy * 0.85, 0.20, 0.65), 4);

            progress.BktMasteryPlt = evaluatedPlt;

            if (skillId != Guid.Empty)
            {
                try
                {
                    await _learningProfileRepository.UpsertSkillPriorsAsync(
                        progress.StudentId,
                        new List<(Guid SkillId, double MasteryScore)> { (skillId, evaluatedPlt) },
                        ct);

                    var enrollment = await _classEnrollmentRepository.GetEnrollmentByStudentIdAsync(progress.StudentId, ct);
                    Guid classId = enrollment?.ClassId ?? Guid.Empty;
                    if (classId == Guid.Empty)
                    {
                        var enrolled = await _classEnrollmentRepository.EnrollStudentAsync(
                            progress.StudentId,
                            DefaultCampusId,
                            "ACCELERATION",
                            progress.Id,
                            DefaultCampusName,
                            ct);
                        classId = enrolled.ClassId;
                    }

                    if (classId != Guid.Empty)
                    {
                        await _aggregatorService.UpdateCellWriteThroughAsync(
                            classId,
                            progress.StudentId,
                            skillId,
                            skillName: !string.IsNullOrWhiteSpace(resolvedSkillName)
                                ? resolvedSkillName
                                : $"Kỹ năng {skillId.ToString()[..8]}",
                            domainCode: resolvedDomainCode,
                            pltValue: evaluatedPlt,
                            assessmentType: "PREVIEW_QUIZ",
                            ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể đồng bộ kết quả đánh giá lại Preview vào Heatmap cho học sinh {StudentId}", progress.StudentId);
                }
            }
        }

        // Chuyển PREVIEW -> LEARN (không hạ cấp nếu học sinh đã ở APPLY / REFLECT)
        if (string.Equals(progress.CurrentStep, "PREVIEW", StringComparison.OrdinalIgnoreCase))
        {
            progress.CurrentStep = "LEARN";
        }

        bool isEvaluatedOk = totalQuestions > 0 && (double)totalCorrect / totalQuestions >= 0.70;
        string evalNotice = isEvaluatedOk
            ? " Bạn đã nắm khá vững kiến thức nền tảng của chuyên đề này."
            : " Hãy xem lại đáp án chi tiết rồi chuyển sang phần Lý thuyết trọng tâm nhé.";

        string message = !string.IsNullOrWhiteSpace(videoUrl)
            ? $"Hoàn thành bài khởi động ({totalCorrect}/{totalQuestions} câu đúng)!{evalNotice}"
            : $"Hoàn thành bài khởi động ({totalCorrect}/{totalQuestions} câu đúng)!{evalNotice}";

        progress.PreviewSubmissionJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            previewQuestions = savedPreviewQuestions,
            previewAnswers = savedPreviewAnswers,
            previewResult = new
            {
                totalQuestions,
                totalCorrect,
                message,
                feedbacks = feedbacks.Select(f => new
                {
                    questionId = f.QuestionId,
                    selectedOption = f.SelectedOption,
                    correctOption = f.CorrectOption,
                    isCorrect = f.IsCorrect,
                    explanation = f.Explanation
                }).ToList()
            }
        });

        progress.UpdatedAt = DateTime.UtcNow;
        await _stageProgressRepository.UpdateAsync(progress, ct);
        _logger.LogInformation("Tiến trình {StageProgressId} hoàn thành PREVIEW ({Correct}/{Total}, P(Lt)={Plt}), đã lưu vào DB.",
            progress.Id, totalCorrect, totalQuestions, progress.BktMasteryPlt);

        var response = new SubmitPreviewResponseDto(
            progress.Id,
            progress.RoadmapNodeId,
            progress.CurrentStep,
            progress.Status,
            totalQuestions,
            totalCorrect,
            message,
            !string.IsNullOrWhiteSpace(videoUrl) ? "WATCH_VIDEO" : "STUDY_MATERIAL",
            materialId,
            videoUrl,
            feedbacks
        );

        return Result<SubmitPreviewResponseDto>.Success(response);
    }
}
