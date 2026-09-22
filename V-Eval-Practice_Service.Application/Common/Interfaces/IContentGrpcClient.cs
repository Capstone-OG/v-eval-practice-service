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

public interface IContentGrpcClient
{
    Task<IReadOnlyDictionary<Guid, ExamQuestionKeyDto>> GetExamAnswerKeysAsync(Guid examId, CancellationToken ct = default);
}
