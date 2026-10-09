using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

/// <summary>
/// DTO thông tin chi tiết một bản ghi trong Sổ tay lỗi sai
/// </summary>
public record MistakeNotebookItemDto(
    Guid Id,
    Guid StudentId,
    Guid QuestionId,
    Guid SkillId,
    string SkillName,
    string PatternId,
    string? CognitiveErrorTag,
    string? StudentNotes,
    DateTime NextReviewDate,
    int ReviewCount,
    int ConsecutiveCorrectReviews,
    int IntervalDays,
    double EaseFactor,
    bool IsMastered,
    DateTime? LastReviewedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// DTO danh sách sổ tay lỗi sai kèm phân trang và thống kê
/// </summary>
public record MistakeNotebookListResponseDto(
    List<MistakeNotebookItemDto> Items,
    int TotalCount,
    int PageIndex,
    int PageSize,
    int MasteredCount,
    int UnmasteredCount
);

/// <summary>
/// DTO phương án của câu hỏi biến thể ôn tập
/// </summary>
public record DailyReviewOptionDto(string Key, string Text);

/// <summary>
/// DTO một câu hỏi ôn tập ngắt quãng (Isomorphic Variant) cho ngày hôm nay
/// </summary>
public record DailyReviewQuestionDto(
    Guid MistakeNotebookId,
    Guid OriginalQuestionId,
    Guid ReviewQuestionId,
    Guid SkillId,
    string SkillName,
    string PatternId,
    string Content,
    List<DailyReviewOptionDto> Options,
    int DifficultyLevel,
    int ReviewCount,
    int ConsecutiveCorrect,
    string? CognitiveErrorTag,
    string? StudentNotes,
    bool IsIsomorphicVariant
);

/// <summary>
/// DTO danh sách các câu hỏi ôn tập đến hạn hôm nay
/// </summary>
public record DailyReviewListResponseDto(
    Guid StudentId,
    DateTime ReviewDate,
    int TotalDueQuestions,
    List<DailyReviewQuestionDto> Questions
);

/// <summary>
/// Request gắn nhãn nhận thức nguyên nhân sai
/// </summary>
public record TagCognitiveErrorRequestDto(
    string CognitiveErrorTag,
    string? StudentNotes
);

/// <summary>
/// Response sau khi gắn nhãn nhận thức
/// </summary>
public record TagCognitiveErrorResponseDto(
    Guid Id,
    string CognitiveErrorTag,
    string? StudentNotes,
    string Message
);

/// <summary>
/// Request nộp bài câu hỏi ôn tập
/// </summary>
public record SubmitDailyReviewRequestDto(
    string SelectedOption,
    int TimeSpentSeconds
);

/// <summary>
/// Response kết quả làm bài ôn tập và cập nhật lịch SM-2
/// </summary>
public record SubmitDailyReviewResponseDto(
    Guid MistakeNotebookId,
    bool IsCorrect,
    string CorrectOption,
    string Explanation,
    int ConsecutiveCorrectReviews,
    int IntervalDays,
    double EaseFactor,
    bool IsMastered,
    DateTime NextReviewDate,
    string Message
);
