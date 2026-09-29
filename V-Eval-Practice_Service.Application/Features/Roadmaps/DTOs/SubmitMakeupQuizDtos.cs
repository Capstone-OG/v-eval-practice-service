using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// Request nộp bài Quiz bù cho học sinh vắng mặt buổi Live Q&A (Core Flow 2 - API 7)
/// </summary>
public class SubmitMakeupQuizRequestDto
{
    public List<MilestoneQuizAnswerItemDto> Answers { get; set; } = new();
    public int TotalTimeSpentSeconds { get; set; }
}

/// <summary>
/// Response kết quả chấm điểm bài Quiz bù và trạng thái mở khóa State Machine
/// </summary>
public class SubmitMakeupQuizResponseDto
{
    public Guid NodeId { get; set; }
    public Guid SessionId { get; set; }
    public Guid AttendanceId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid MakeupQuizId { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalCorrect { get; set; }
    public double ScorePercentage { get; set; }
    public bool IsPassed { get; set; }
    public bool IsMakeupQuizPassed { get; set; }
    public string CurrentNodeStatus { get; set; } = string.Empty;
    public Guid? NextUnlockedNodeId { get; set; }
    public int? NextUnlockedStepOrder { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MilestoneQuizQuestionResultDto> QuestionResults { get; set; } = new();
}
