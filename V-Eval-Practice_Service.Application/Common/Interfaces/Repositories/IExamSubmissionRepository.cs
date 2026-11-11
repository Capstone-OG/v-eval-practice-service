using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

public interface IExamSubmissionRepository
{
    Task<ExamSubmission?> GetByIdAsync(Guid submissionId, CancellationToken ct = default);
    Task<IReadOnlyList<ExamSubmission>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task AddAsync(ExamSubmission submission, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
