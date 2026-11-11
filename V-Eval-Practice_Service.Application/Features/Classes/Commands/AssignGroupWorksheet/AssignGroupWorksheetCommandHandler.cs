using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignGroupWorksheet;

public class AssignGroupWorksheetCommandHandler : IRequestHandler<AssignGroupWorksheetCommand, Result<AssignWorksheetResponseDto>>
{
    private readonly IClassGroupRepository _classGroupRepository;
    private readonly IContentHttpClient _contentHttpClient;
    private readonly ILogger<AssignGroupWorksheetCommandHandler> _logger;

    public AssignGroupWorksheetCommandHandler(
        IClassGroupRepository classGroupRepository,
        IContentHttpClient contentHttpClient,
        ILogger<AssignGroupWorksheetCommandHandler> logger)
    {
        _classGroupRepository = classGroupRepository;
        _contentHttpClient = contentHttpClient;
        _logger = logger;
    }

    public async Task<Result<AssignWorksheetResponseDto>> Handle(
        AssignGroupWorksheetCommand request,
        CancellationToken cancellationToken)
    {
        var group = await _classGroupRepository.GetGroupByIdAsync(request.GroupId, cancellationToken);
        if (group == null || group.ClassId != request.ClassId)
        {
            return Result<AssignWorksheetResponseDto>.Failure(
                Error.NotFound("ClassGroup.NotFound", $"Không tìm thấy nhóm học tập với ID {request.GroupId} trong lớp {request.ClassId}."));
        }

        string targetWorksheetId;
        string targetWorksheetTitle;

        // Trích xuất hoặc Tự động đóng gói qua Content Service API 19
        if (string.IsNullOrWhiteSpace(request.WorksheetId))
        {
            _logger.LogInformation("WorksheetId không được cung cấp. Kích hoạt luồng tự động đóng gói đề thích ứng cho nhóm {GroupName} ({GroupId})",
                group.GroupName, group.GroupId);

            var skillGuids = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(group.CommonWeakSkillIds))
            {
                try
                {
                    var trimmed = group.CommonWeakSkillIds.Trim();
                    if (trimmed.StartsWith("["))
                    {
                        var idStrings = JsonSerializer.Deserialize<List<string>>(trimmed);
                        if (idStrings != null)
                        {
                            foreach (var s in idStrings)
                            {
                                if (Guid.TryParse(s, out var g)) skillGuids.Add(g);
                            }
                        }
                    }
                    else
                    {
                        foreach (var part in trimmed.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (Guid.TryParse(part.Trim(), out var g)) skillGuids.Add(g);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi phân tích CommonWeakSkillIds của nhóm {GroupId}: {Raw}", 
                        group.GroupId, group.CommonWeakSkillIds);
                }
            }

            if (skillGuids.Count == 0)
            {
                return Result<AssignWorksheetResponseDto>.Failure(
                    Error.Validation("ClassGroup.NoWeakSkills", 
                        $"Nhóm '{group.GroupName}' chưa có thông tin vùng trũng kiến thức (CommonWeakSkillIds). Vui lòng cung cấp WorksheetId thủ công hoặc chạy phân vùng nhóm lại."));
            }

            string quizTitle = !string.IsNullOrWhiteSpace(request.WorksheetTitle)
                ? request.WorksheetTitle
                : (!string.IsNullOrWhiteSpace(group.RecommendedWorksheetTitle)
                    ? group.RecommendedWorksheetTitle
                    : $"Phiếu ôn tập củng cố - {group.GroupName}");

            var quizRequest = new CreateAdaptiveQuizRequest(
                Title: quizTitle,
                SkillIds: skillGuids,
                QuestionCount: request.QuestionCount > 0 ? request.QuestionCount : 5,
                MinBloomLevel: 1,
                MaxBloomLevel: 3
            );

            var quizResponse = await _contentHttpClient.CreateAdaptiveQuizAsync(quizRequest, cancellationToken);
            if (quizResponse == null)
            {
                return Result<AssignWorksheetResponseDto>.Failure(
                    Error.Failure("ContentService.QuizCreationFailed", 
                        $"Không thể tự động khởi tạo bộ đề củng cố thích ứng từ Content Service cho nhóm '{group.GroupName}'."));
            }

            targetWorksheetId = quizResponse.ExamId.ToString();
            targetWorksheetTitle = quizResponse.Title;

            _logger.LogInformation("Tự động tạo thành công bộ đề thích ứng: WorksheetId={Id}, Title='{Title}' cho nhóm {GroupName}",
                targetWorksheetId, targetWorksheetTitle, group.GroupName);
        }
        else
        {
            targetWorksheetId = request.WorksheetId.Trim();
            targetWorksheetTitle = !string.IsNullOrWhiteSpace(request.WorksheetTitle)
                ? request.WorksheetTitle.Trim()
                : (!string.IsNullOrWhiteSpace(group.RecommendedWorksheetTitle) 
                    ? group.RecommendedWorksheetTitle 
                    : $"Đề bài tập - {group.GroupName}");
        }

        bool success = await _classGroupRepository.AssignWorksheetAsync(
            request.GroupId,
            targetWorksheetId,
            targetWorksheetTitle,
            cancellationToken);

        if (!success)
        {
            return Result<AssignWorksheetResponseDto>.Failure(
                Error.Failure("ClassGroup.AssignFailed", "Không thể gán đề luyện tập cho nhóm học tập."));
        }

        _logger.LogInformation("Đã phân phối đề '{Title}' ({Id}) cho nhóm {GroupName} ({GroupId})",
            targetWorksheetTitle, targetWorksheetId, group.GroupName, group.GroupId);

        var response = new AssignWorksheetResponseDto(
            GroupId: group.GroupId,
            GroupName: group.GroupName,
            AssignedWorksheetId: targetWorksheetId,
            AssignedWorksheetTitle: targetWorksheetTitle,
            WorksheetAssignedAt: DateTime.UtcNow,
            Message: $"Đã phân phối thành công đề luyện tập '{targetWorksheetTitle}' tới {group.GroupName} ({group.Members.Count} học sinh)."
        );

        return Result<AssignWorksheetResponseDto>.Success(response);
    }
}
