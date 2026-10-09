using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

public interface IMistakeNotebookRepository
{
    Task<MistakeNotebook?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MistakeNotebook?> GetByStudentAndQuestionAsync(Guid studentId, Guid questionId, CancellationToken ct = default);
    Task<List<MistakeNotebook>> GetDailyReviewItemsAsync(Guid studentId, DateTime reviewDate, CancellationToken ct = default);
    Task<(List<MistakeNotebook> Items, int TotalCount)> GetByFilterAsync(
        Guid studentId,
        Guid? skillId,
        string? cognitiveErrorTag,
        bool? isMastered,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(MistakeNotebook item, CancellationToken ct = default);
    Task UpdateAsync(MistakeNotebook item, CancellationToken ct = default);
}
