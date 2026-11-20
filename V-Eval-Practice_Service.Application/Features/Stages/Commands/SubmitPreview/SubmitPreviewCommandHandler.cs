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
        string resolvedDomainCode = "DOM_MATH";
        string resolvedSkillName = string.Empty;

        foreach (var ans in request.Answers)
        {
            string selected = (ans.SelectedOption ?? string.Empty).Trim().ToUpperInvariant();
            string correctOpt = string.Empty;
            string explanation = string.Empty;
            bool isCorrect = false;

            try
            {
                var detail = await _contentGrpcClient.GetQuestionDetailAsync(ans.QuestionId, ct);
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
        progress.UpdatedAt = DateTime.UtcNow;
        await _stageProgressRepository.UpdateAsync(progress, ct);
        _logger.LogInformation("Tiến trình {StageProgressId} hoàn thành PREVIEW ({Correct}/{Total}, P(Lt)={Plt}), chuyển sang LEARN.",
            progress.Id, totalCorrect, totalQuestions, progress.BktMasteryPlt);

        bool isEvaluatedOk = totalQuestions > 0 && (double)totalCorrect / totalQuestions >= 0.70;
        string evalNotice = isEvaluatedOk
            ? $" Chuyên đề này đã được đánh giá lại ở mức Đạt chuẩn OK (BKT {progress.BktMasteryPlt * 100:F0}%) trên lộ trình."
            : $" Chuyên đề đã được đánh giá năng lực thực tế (BKT {progress.BktMasteryPlt * 100:F0}%).";

        string message = !string.IsNullOrWhiteSpace(videoUrl)
            ? $"Hoàn thành bài đánh giá nhanh ({totalCorrect}/{totalQuestions} câu đúng)!{evalNotice} Hãy xem bài giảng & lý thuyết SGK (tối thiểu 80%) để mở khóa bước Luyện tập thích ứng APPLY."
            : $"Hoàn thành bài đánh giá nhanh ({totalCorrect}/{totalQuestions} câu đúng)!{evalNotice} Hãy nghiên cứu kỹ tài liệu Lý thuyết & Phương pháp giải SGK ở bước LEARN để mở khóa bước Luyện tập thích ứng APPLY.";

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
