using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho một chặng học (Milestone) trong lộ trình tích hợp 3 thành phần: Video, Quiz, và Live Session
/// </summary>
public class RoadmapNode
{
    public Guid NodeId { get; set; } = Guid.NewGuid();
    public Guid RoadmapId { get; set; }
    public Guid SkillId { get; set; }
    public int StepOrder { get; set; }

    // 3 Thành phần chính của một chặng học:
    // Thành phần 1: Bài giảng lý thuyết (content.Materials)
    public Guid? MaterialId { get; set; }
    // Thành phần 2: Bài Quiz củng cố 5-10 câu
    public Guid? QuizExamId { get; set; }
    // Thành phần 3: Buổi Live Q&A trực tuyến cơ sở
    public Guid? LiveSessionId { get; set; }

    public string Status { get; set; } = "LOCKED"; // LOCKED, IN_PROGRESS, COMPLETED, SKIPPED_PRUNED
    public bool IsPruned { get; set; } = false;
    public DateTime? UnlockedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Tiến độ học video bài giảng lý thuyết (Core Flow 2 - API 4)
    public int VideoWatchedSeconds { get; set; } = 0;
    public int VideoTotalSeconds { get; set; } = 0;
    public bool IsVideoCompleted { get; set; } = false;

    // Kết quả bài Quiz củng cố chuyên đề (Core Flow 2 - API 6)
    public double? QuizScore { get; set; } = 0.0;
    public bool IsQuizPassed { get; set; } = false;

    // Navigation Properties
    public virtual LearningRoadmap Roadmap { get; set; } = null!;
    public virtual LiveSession? LiveSession { get; set; }
}
