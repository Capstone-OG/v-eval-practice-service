using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class ExamSubmissionRepository : IExamSubmissionRepository
{
    private readonly PracticeDbContext _context;

    public ExamSubmissionRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<ExamSubmission?> GetByIdAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await _context.ExamSubmissions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
    }

    public async Task<IReadOnlyList<ExamSubmission>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.ExamSubmissions
            .Include(s => s.Answers)
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.CompletedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ExamSubmission submission, CancellationToken ct = default)
    {
        await _context.ExamSubmissions.AddAsync(submission, ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
