using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// Câu trả lời của học sinh cho từng câu trong bài Quiz củng cố
/// </summary>
public class MilestoneQuizAnswerItemDto
{
    public Guid QuestionId { get; set; }
    public string? SelectedOption { get; set; }
    public int TimeSpentSeconds { get; set; }
}

/// <summary>
/// Request nộp bài Quiz củng cố chuyên đề (Core Flow 2 - API 6)
/// </summary>
public class SubmitMilestoneQuizRequestDto
{
    public List<MilestoneQuizAnswerItemDto> Answers { get; set; } = new();
    public int TotalTimeSpentSeconds { get; set; }
}

/// <summary>
/// Chi tiết kết quả chấm điểm từng câu hỏi trong bài Quiz
/// </summary>
public class MilestoneQuizQuestionResultDto
{
    public Guid QuestionId { get; set; }
    public int QuestionOrder { get; set; }
    public string? SelectedOption { get; set; }
    public string CorrectOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }
    public string SkillId { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
}

/// <summary>
/// Response kết quả chấm điểm Quiz củng cố và trạng thái mở khóa State Machine
/// </summary>
public class SubmitMilestoneQuizResponseDto
{
    public Guid NodeId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid QuizExamId { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalCorrect { get; set; }
    public double ScorePercentage { get; set; }
    public bool IsPassed { get; set; }
    public string CurrentNodeStatus { get; set; } = string.Empty;
    public Guid? NextUnlockedNodeId { get; set; }
    public int? NextUnlockedStepOrder { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MilestoneQuizQuestionResultDto> QuestionResults { get; set; } = new();
}
