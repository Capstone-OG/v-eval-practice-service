using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho ảnh chụp định kỳ từ webcam phục vụ giám sát liêm chính học thuật trong phòng thi thử (Core Flow 3 - Module 3)
/// </summary>
public class ProctoringSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public string SnapshotUrl { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public bool FaceDetected { get; set; } = true;

    // Navigation property
    public virtual ExamSubmission Submission { get; set; } = null!;
}
