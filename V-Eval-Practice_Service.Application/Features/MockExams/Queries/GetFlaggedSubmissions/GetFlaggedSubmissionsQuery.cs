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
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Queries.GetFlaggedSubmissions;

public record GetFlaggedSubmissionsQuery(Guid? ClassId = null) : IRequest<Result<IReadOnlyList<FlaggedMockExamDto>>>;

public class GetFlaggedSubmissionsQueryHandler : IRequestHandler<GetFlaggedSubmissionsQuery, Result<IReadOnlyList<FlaggedMockExamDto>>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly ILogger<GetFlaggedSubmissionsQueryHandler> _logger;

    public GetFlaggedSubmissionsQueryHandler(
        IMockExamRepository mockExamRepository,
        IIdentityGrpcClient identityGrpcClient,
        ILogger<GetFlaggedSubmissionsQueryHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _identityGrpcClient = identityGrpcClient;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<FlaggedMockExamDto>>> Handle(
        GetFlaggedSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truy vấn danh sách bài thi thử bị gắn cờ cần Mentor hậu kiểm (ClassId: {ClassId})", request.ClassId);

        var submissions = await _mockExamRepository.GetFlaggedSubmissionsAsync(request.ClassId, cancellationToken);

        var studentIds = submissions
            .Select(s => s.StudentId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var studentNames = new Dictionary<Guid, string>();
        if (studentIds.Count > 0)
        {
            try
            {
                studentNames = await _identityGrpcClient.GetStudentNamesAsync(studentIds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tra cứu họ tên thí sinh từ Identity gRPC cho {Count} bài thi bị gắn cờ.", studentIds.Count);
            }
        }

        var dtos = submissions.Select(s =>
        {
            var resolvedName = studentNames.TryGetValue(s.StudentId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : string.Empty;
            DateTime? submittedAt = string.Equals(s.Status, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase)
                ? null
                : s.CompletedAt;

            return new FlaggedMockExamDto(
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
                s.Status,
                resolvedName,
                submittedAt
            );
        }).ToList();

        return Result<IReadOnlyList<FlaggedMockExamDto>>.Success(dtos);
    }
}

