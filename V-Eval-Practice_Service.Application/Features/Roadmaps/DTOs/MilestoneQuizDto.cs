using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// Lựa chọn đáp án của câu hỏi Quiz (đã ẩn hoàn toàn đáp án đúng để chống gian lận)
/// </summary>
public record MilestoneQuizOptionDto(
    string OptionId,
    string Content
);

/// <summary>
/// Chi tiết câu hỏi trong đề thi Quiz củng cố của chặng học
/// </summary>
public record MilestoneQuizQuestionItemDto(
    Guid QuestionId,
    int QuestionOrder,
    string Content,
    IReadOnlyList<MilestoneQuizOptionDto> Options,
    int DifficultyLevel,
    Guid SkillId,
    string SkillName
);

/// <summary>
/// DTO Đề thi Quiz củng cố chuyên đề của chặng học (Core Flow 2 - API 5)
/// </summary>
public record MilestoneQuizDto(
    Guid ExamId,
    Guid NodeId,
    Guid SkillId,
    string SkillName,
    string Title,
    int DurationMinutes,
    int TotalQuestions,
    string NodeStatus,
    bool IsVideoCompleted,
    IReadOnlyList<MilestoneQuizQuestionItemDto> Questions
);
