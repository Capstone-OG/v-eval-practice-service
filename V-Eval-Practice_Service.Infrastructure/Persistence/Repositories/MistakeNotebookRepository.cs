using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class MistakeNotebookRepository : IMistakeNotebookRepository
{
    private readonly PracticeDbContext _context;

    public MistakeNotebookRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<MistakeNotebook?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MistakeNotebooks
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<MistakeNotebook?> GetByStudentAndQuestionAsync(Guid studentId, Guid questionId, CancellationToken ct = default)
    {
        return await _context.MistakeNotebooks
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.QuestionId == questionId, ct);
    }

    public async Task<List<MistakeNotebook>> GetDailyReviewItemsAsync(Guid studentId, DateTime reviewDate, CancellationToken ct = default)
    {
        var targetDate = DateTime.SpecifyKind(reviewDate.Date, DateTimeKind.Utc);
        return await _context.MistakeNotebooks
            .Where(m => m.StudentId == studentId && m.NextReviewDate <= targetDate && !m.IsMastered)
            .OrderBy(m => m.NextReviewDate)
            .ToListAsync(ct);
    }

    public async Task<(List<MistakeNotebook> Items, int TotalCount)> GetByFilterAsync(
        Guid studentId,
        Guid? skillId,
        string? cognitiveErrorTag,
        bool? isMastered,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.MistakeNotebooks
            .Where(m => m.StudentId == studentId);

        if (skillId.HasValue && skillId.Value != Guid.Empty)
        {
            query = query.Where(m => m.SkillId == skillId.Value);
        }

        if (!string.IsNullOrWhiteSpace(cognitiveErrorTag))
        {
            query = query.Where(m => m.CognitiveErrorTag == cognitiveErrorTag.ToUpperInvariant());
        }

        if (isMastered.HasValue)
        {
            query = query.Where(m => m.IsMastered == isMastered.Value);
        }

        int totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.UpdatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(MistakeNotebook item, CancellationToken ct = default)
    {
        item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc);
        item.UpdatedAt = DateTime.SpecifyKind(item.UpdatedAt, DateTimeKind.Utc);
        item.NextReviewDate = DateTime.SpecifyKind(item.NextReviewDate.Date, DateTimeKind.Utc);
        if (item.LastReviewedAt.HasValue)
        {
            item.LastReviewedAt = DateTime.SpecifyKind(item.LastReviewedAt.Value, DateTimeKind.Utc);
        }

        await _context.MistakeNotebooks.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MistakeNotebook item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc);
        item.NextReviewDate = DateTime.SpecifyKind(item.NextReviewDate.Date, DateTimeKind.Utc);
        if (item.LastReviewedAt.HasValue)
        {
            item.LastReviewedAt = DateTime.SpecifyKind(item.LastReviewedAt.Value, DateTimeKind.Utc);
        }

        _context.MistakeNotebooks.Update(item);
        await _context.SaveChangesAsync(ct);
    }
}
