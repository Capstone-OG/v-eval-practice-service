using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

/// <summary>
/// DTO tùy chọn đáp án của câu hỏi cứu trợ
/// </summary>
public record RemedialOptionDto(string Key, string Text);

/// <summary>
/// DTO câu hỏi cơ bản trong gói cứu trợ (độ khó b &lt; 0.0)
/// </summary>
public record RemedialQuestionDto(
    Guid QuestionId,
    int QuestionOrder,
    string Content,
    List<RemedialOptionDto> Options,
    double DifficultyB,
    string CognitiveLevel
);

/// <summary>
/// DTO gói cứu trợ trả về cho học sinh khi bị kích hoạt BR-03
/// </summary>
public record RemedialPackageResponseDto(
    Guid StageProgressId,
    Guid RoadmapNodeId,
    Guid? SkillId,
    string CurrentStep,
    string Status,
    int ConsecutiveIncorrect,
    string Title,
    string SummaryFormula,
    string RemedialVideoUrl,
    string Instruction,
    List<RemedialQuestionDto> Questions
);

/// <summary>
/// DTO nộp đáp án cho từng câu hỏi cứu trợ
/// </summary>
public record RemedialAnswerItemDto(Guid QuestionId, string SelectedOption);

/// <summary>
/// Request nộp bài cứu trợ
/// </summary>
public record SubmitRemedialRequestDto(List<RemedialAnswerItemDto> Answers);

/// <summary>
/// DTO kết quả từng câu hỏi cứu trợ kèm lời giải thích
/// </summary>
public record RemedialQuestionResultDto(
    Guid QuestionId,
    string SelectedOption,
    string CorrectOption,
    bool IsCorrect,
    string Explanation
);

/// <summary>
/// Response sau khi hoàn thành gói cứu trợ và khôi phục trạng thái
/// </summary>
public record SubmitRemedialResponseDto(
    Guid StageProgressId,
    int Score,
    int TotalQuestions,
    bool IsRemedialPassed,
    string CurrentStatus,
    string CurrentStep,
    int ConsecutiveIncorrect,
    string Message,
    string NextAction,
    List<RemedialQuestionResultDto> Results
);
