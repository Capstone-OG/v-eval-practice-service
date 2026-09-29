using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

public record ExamQuestionKeyDto(
    Guid QuestionId,
    int QuestionOrder,
    string CorrectOption,
    string SkillId,
    int DifficultyLevel,
    string SkillName = "",
    string DomainId = "",
    string DomainName = ""
);

public record SkillTreeNodeDto(
    Guid SkillId,
    string Name,
    double Weight,
    IReadOnlyList<Guid> PrerequisiteIds,
    string Description = "",
    Guid DomainId = default,
    string DomainName = ""
);

public record MilestoneQuizQuestionOptionDto(
    string OptionId,
    string Content
);

public record MilestoneQuizQuestionDto(
    Guid QuestionId,
    int QuestionOrder,
    string Content,
    IReadOnlyList<MilestoneQuizQuestionOptionDto> Options,
    int DifficultyLevel,
    Guid SkillId,
    string SkillName
);

public record MilestoneQuizResultDto(
    Guid ExamId,
    string Title,
    int DurationMinutes,
    int TotalQuestions,
    IReadOnlyList<MilestoneQuizQuestionDto> Questions
);

public interface IContentGrpcClient
{
    Task<IReadOnlyDictionary<Guid, ExamQuestionKeyDto>> GetExamAnswerKeysAsync(Guid examId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillTreeNodeDto>> GetSkillsTreeAsync(CancellationToken ct = default);
    Task<MilestoneQuizResultDto?> GetMilestoneQuizAsync(Guid skillId, Guid? examId, int questionCount = 5, CancellationToken ct = default);
}
