using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository quản lý phiên thi thử mô phỏng (Mock Exam) và ảnh chụp giám sát (Proctoring)
/// </summary>
public interface IMockExamRepository
{
    Task<ExamSubmission?> GetByIdAsync(Guid submissionId, CancellationToken ct = default);
    Task<ExamSubmission?> GetByIdWithDetailsAsync(Guid submissionId, CancellationToken ct = default);
    Task<ExamSubmission?> GetActiveSubmissionAsync(Guid studentId, Guid examId, CancellationToken ct = default);
    Task AddAsync(ExamSubmission submission, CancellationToken ct = default);
    Task UpdateAsync(ExamSubmission submission, CancellationToken ct = default);
    Task AddSnapshotAsync(ProctoringSnapshot snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<ProctoringSnapshot>> GetSnapshotsBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
