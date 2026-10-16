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

public record ReportInteractionRequestPayload(
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("student_id")] Guid? StudentId
);

public record ReportInteractionResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("student_reported")] bool StudentReported,
    [property: JsonPropertyName("mentor_resolution")] string? MentorResolution,
    [property: JsonPropertyName("message")] string Message
);

public record TutorInteractionTranscriptItemDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("option")] string? Option,
    [property: JsonPropertyName("option_text")] string? OptionText,
    [property: JsonPropertyName("is_fallback")] bool? IsFallback
);

public record TutorInteractionItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("question_id")] string QuestionId,
    [property: JsonPropertyName("archetype_pattern_id")] string? ArchetypePatternId,
    [property: JsonPropertyName("student_selected_option")] string StudentSelectedOption,
    [property: JsonPropertyName("correct_option")] string CorrectOption,
    [property: JsonPropertyName("matched_trap_id")] string? MatchedTrapId,
    [property: JsonPropertyName("ai_guidance_transcript")] List<TutorInteractionTranscriptItemDto> AiGuidanceTranscript,
    [property: JsonPropertyName("validation_status")] string ValidationStatus,
    [property: JsonPropertyName("student_reported")] bool StudentReported,
    [property: JsonPropertyName("mentor_resolution")] string? MentorResolution,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("pattern_code")] string? PatternCode,
    [property: JsonPropertyName("pattern_name")] string? PatternName,
    [property: JsonPropertyName("core_theory_latex")] string? CoreTheoryLatex,
    [property: JsonPropertyName("trap_code")] string? TrapCode,
    [property: JsonPropertyName("trap_name")] string? TrapName,
    [property: JsonPropertyName("misconception_explanation")] string? MisconceptionExplanation
);

public interface IAiTutorClient
{
    IAsyncEnumerable<string> StreamSocraticGuidanceAsync(
        SocraticAskRequestPayload payload,
        CancellationToken cancellationToken = default);

    Task<SocraticAskResponseDto?> AskSocraticGuidanceAsync(
        SocraticAskRequestPayload payload,
        CancellationToken cancellationToken = default);

    Task<ReportInteractionResponseDto?> ReportInteractionAsync(
        Guid interactionId,
        string? reason = null,
        Guid? studentId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TutorInteractionItemDto>> GetInteractionsAsync(
        bool reportedOnly = false,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
