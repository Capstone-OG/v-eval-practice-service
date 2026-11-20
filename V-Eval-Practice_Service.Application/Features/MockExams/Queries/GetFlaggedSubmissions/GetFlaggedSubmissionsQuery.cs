using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Queries.GetFlaggedSubmissions;

public record GetFlaggedSubmissionsQuery(Guid? ClassId = null) : IRequest<Result<IReadOnlyList<FlaggedMockExamDto>>>;

public class GetFlaggedSubmissionsQueryHandler : IRequestHandler<GetFlaggedSubmissionsQuery, Result<IReadOnlyList<FlaggedMockExamDto>>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly ILogger<GetFlaggedSubmissionsQueryHandler> _logger;

    public GetFlaggedSubmissionsQueryHandler(
        IMockExamRepository mockExamRepository,
        ILogger<GetFlaggedSubmissionsQueryHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<FlaggedMockExamDto>>> Handle(
        GetFlaggedSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truy vấn danh sách bài thi thử bị gắn cờ cần Mentor hậu kiểm (ClassId: {ClassId})", request.ClassId);

        var submissions = await _mockExamRepository.GetFlaggedSubmissionsAsync(request.ClassId, cancellationToken);

        var dtos = submissions.Select(s => new FlaggedMockExamDto(
            s.SubmissionId,
            s.ExamId,
            s.StudentId,
            s.EnrolledClassId,
            s.TotalScore,
            s.TotalCorrect,
            s.TotalQuestions,
            s.TabDepartureCount,
            s.IntegrityStatus,
            s.MentorVerdict,
            s.MentorNotes,
            s.StartedAt,
            s.CompletedAt,
            s.ProctoringSnapshots?.Count ?? 0,
            s.Status
        )).ToList();

        return Result<IReadOnlyList<FlaggedMockExamDto>>.Success(dtos);
    }
}

