using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

public record SocraticAskRequestPayload(
    [property: JsonPropertyName("student_id")] Guid StudentId,
    [property: JsonPropertyName("question_id")] Guid QuestionId,
    [property: JsonPropertyName("question_latex")] string QuestionLatex,
    [property: JsonPropertyName("options")] Dictionary<string, string> Options,
    [property: JsonPropertyName("student_selected_option")] string StudentSelectedOption,
    [property: JsonPropertyName("correct_option")] string CorrectOption,
    [property: JsonPropertyName("skill_id")] Guid? SkillId
);

public record SocraticAskResponseDto(
    [property: JsonPropertyName("interaction_id")] string? InteractionId,
    [property: JsonPropertyName("guidance_text")] string GuidanceText,
    [property: JsonPropertyName("is_fallback")] bool IsFallback,
    [property: JsonPropertyName("validation_status")] string ValidationStatus,
    [property: JsonPropertyName("rejection_reason")] string RejectionReason,
    [property: JsonPropertyName("archetype_code")] string? ArchetypeCode,
    [property: JsonPropertyName("matched_trap_code")] string? MatchedTrapCode,
    [property: JsonPropertyName("subgraph_status")] string SubgraphStatus
);

public interface IAiTutorClient
{
    IAsyncEnumerable<string> StreamSocraticGuidanceAsync(
        SocraticAskRequestPayload payload,
        CancellationToken cancellationToken = default);

    Task<SocraticAskResponseDto?> AskSocraticGuidanceAsync(
        SocraticAskRequestPayload payload,
        CancellationToken cancellationToken = default);
}
