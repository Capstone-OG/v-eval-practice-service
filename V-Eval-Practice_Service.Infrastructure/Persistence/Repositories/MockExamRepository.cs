using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class MockExamRepository : IMockExamRepository
{
    private readonly PracticeDbContext _context;

    public MockExamRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<ExamSubmission?> GetByIdAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await _context.ExamSubmissions
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
    }

    public async Task<ExamSubmission?> GetByIdWithDetailsAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await _context.ExamSubmissions
            .Include(s => s.Answers)
            .Include(s => s.ProctoringSnapshots)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
    }

    public async Task<ExamSubmission?> GetActiveSubmissionAsync(Guid studentId, Guid examId, CancellationToken ct = default)
    {
        return await _context.ExamSubmissions
            .Where(s => s.StudentId == studentId && s.ExamId == examId && s.ExamType == "MOCK_EXAM" && s.Status == "IN_PROGRESS")
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ExamSubmission submission, CancellationToken ct = default)
    {
        await _context.ExamSubmissions.AddAsync(submission, ct);
    }

    public Task UpdateAsync(ExamSubmission submission, CancellationToken ct = default)
    {
        _context.Entry(submission).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task AddSnapshotAsync(ProctoringSnapshot snapshot, CancellationToken ct = default)
    {
        await _context.ProctoringSnapshots.AddAsync(snapshot, ct);
    }

    public async Task AddAnswersAsync(IEnumerable<SubmissionAnswer> answers, CancellationToken ct = default)
    {
        await _context.SubmissionAnswers.AddRangeAsync(answers, ct);
    }

    public async Task<IReadOnlyList<ProctoringSnapshot>> GetSnapshotsBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await _context.ProctoringSnapshots
            .Where(p => p.SubmissionId == submissionId)
            .OrderBy(p => p.CapturedAt)
            .ToListAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
