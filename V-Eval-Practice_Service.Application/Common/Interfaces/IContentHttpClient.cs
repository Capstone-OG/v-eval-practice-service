using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

public record CreateAdaptiveQuizRequest(
    string? Title,
    List<Guid> SkillIds,
    int QuestionCount = 5,
    int MinBloomLevel = 1,
    int MaxBloomLevel = 3
);

public record CreateAdaptiveQuizResponse(
    Guid ExamId,
    string Title,
    int DurationMinutes,
    int TotalQuestions,
    string ExamCategory,
    List<Guid> TargetSkillIds,
    DateTime CreatedAt,
    string Message
);

public interface IContentHttpClient
{
    Task<CreateAdaptiveQuizResponse?> CreateAdaptiveQuizAsync(
        CreateAdaptiveQuizRequest request,
        CancellationToken ct = default);
}
