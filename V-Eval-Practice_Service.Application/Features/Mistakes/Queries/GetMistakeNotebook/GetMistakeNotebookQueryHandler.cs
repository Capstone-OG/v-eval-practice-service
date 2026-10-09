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
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Queries.GetMistakeNotebook;

public class GetMistakeNotebookQueryHandler : IRequestHandler<GetMistakeNotebookQuery, Result<MistakeNotebookListResponseDto>>
{
    private readonly IMistakeNotebookRepository _mistakeNotebookRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetMistakeNotebookQueryHandler> _logger;

    public GetMistakeNotebookQueryHandler(
        IMistakeNotebookRepository mistakeNotebookRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetMistakeNotebookQueryHandler> logger)
    {
        _mistakeNotebookRepository = mistakeNotebookRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<MistakeNotebookListResponseDto>> Handle(GetMistakeNotebookQuery request, CancellationToken ct)
    {
        int pageIndex = request.PageIndex > 0 ? request.PageIndex : 1;
        int pageSize = request.PageSize > 0 ? request.PageSize : 10;

        var (items, totalCount) = await _mistakeNotebookRepository.GetByFilterAsync(
            request.StudentId,
            request.SkillId,
            request.CognitiveErrorTag,
            request.IsMastered,
            pageIndex,
            pageSize,
            ct);

        // Tra cứu tên chuyên đề từ Content Service SkillTree
        var skillNames = new Dictionary<Guid, string>();
        try
        {
            var skillTree = await _contentGrpcClient.GetSkillsTreeAsync(ct);
            if (skillTree != null)
            {
                foreach (var s in skillTree)
                {
                    skillNames[s.SkillId] = s.Name;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy SkillTree khi duyệt Sổ tay lỗi sai.");
        }

        var dtoList = items.Select(m => new MistakeNotebookItemDto(
            m.Id,
            m.StudentId,
            m.QuestionId,
            m.SkillId,
            SkillName: skillNames.TryGetValue(m.SkillId, out var sName) ? sName : "Chuyên đề trọng tâm",
            m.PatternId,
            m.CognitiveErrorTag,
            m.StudentNotes,
            m.NextReviewDate,
            m.ReviewCount,
            m.ConsecutiveCorrectReviews,
            m.IntervalDays,
            m.EaseFactor,
            m.IsMastered,
            m.LastReviewedAt,
            m.CreatedAt,
            m.UpdatedAt
        )).ToList();

        int masteredCount = items.Count(i => i.IsMastered);
        int unmasteredCount = items.Count(i => !i.IsMastered);

        var response = new MistakeNotebookListResponseDto(
            Items: dtoList,
            TotalCount: totalCount,
            PageIndex: pageIndex,
            PageSize: pageSize,
            MasteredCount: masteredCount,
            UnmasteredCount: unmasteredCount
        );

        return Result<MistakeNotebookListResponseDto>.Success(response);
    }
}
