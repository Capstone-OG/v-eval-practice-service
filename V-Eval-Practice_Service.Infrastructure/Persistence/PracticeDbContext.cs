using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence;

public class PracticeDbContext : DbContext
{
    public PracticeDbContext(DbContextOptions<PracticeDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExamSubmission> ExamSubmissions => Set<ExamSubmission>();
    public DbSet<SubmissionAnswer> SubmissionAnswers => Set<SubmissionAnswer>();
    public DbSet<LearningProfile> LearningProfiles => Set<LearningProfile>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();
    public DbSet<LearningRoadmap> LearningRoadmaps => Set<LearningRoadmap>();
    public DbSet<RoadmapNode> RoadmapNodes => Set<RoadmapNode>();
    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();
    public DbSet<LiveSessionAttendance> LiveSessionAttendances => Set<LiveSessionAttendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("v_eval_practice");

        // Cấu hình bảng exam_submissions thuộc schema v_eval_practice
        modelBuilder.Entity<ExamSubmission>(entity =>
        {
            entity.ToTable("ExamSubmissions", "v_eval_practice");
            entity.HasKey(e => e.SubmissionId);

            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.ExamId).HasColumnName("exam_id").IsRequired();
            entity.Property(e => e.ExamType).HasColumnName("exam_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.TotalScore).HasColumnName("total_score");
            entity.Property(e => e.TotalCorrect).HasColumnName("total_correct");
            entity.Property(e => e.TotalQuestions).HasColumnName("total_questions");
            entity.Property(e => e.TotalTimeSpentSeconds).HasColumnName("total_time_spent_seconds");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();

            entity.Property(e => e.Theta0).HasColumnName("theta_0");
            entity.Property(e => e.PlacementClass).HasColumnName("placement_class").HasMaxLength(50);
            entity.Property(e => e.AiCommentary).HasColumnName("ai_commentary");
            entity.Property(e => e.EnrolledClassId).HasColumnName("enrolled_class_id");

            entity.HasMany(e => e.Answers)
                  .WithOne(a => a.Submission)
                  .HasForeignKey(a => a.SubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng submission_answers thuộc schema v_eval_practice
        modelBuilder.Entity<SubmissionAnswer>(entity =>
        {
            entity.ToTable("SubmissionAnswers", "v_eval_practice");
            entity.HasKey(e => e.AnswerId);

            entity.Property(e => e.AnswerId).HasColumnName("answer_id");
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.SelectedOption).HasColumnName("selected_option").HasMaxLength(10);
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.TimeSpentSeconds).HasColumnName("time_spent_seconds");
        });

        // Cấu hình bảng LearningProfiles lưu tiên nghiệm BKT P(L0)
        modelBuilder.Entity<LearningProfile>(entity =>
        {
            entity.ToTable("LearningProfiles");
            entity.HasKey(e => e.ProfileId);

            entity.Property(e => e.ProfileId).HasColumnName("profile_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.SkillId).HasColumnName("skill_id").IsRequired();
            entity.Property(e => e.MasteryScore).HasColumnName("mastery_score");
            entity.Property(e => e.LastUpdated).HasColumnName("last_updated");
        });

        // Cấu hình bảng Classes
        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("Classes");
            entity.HasKey(e => e.ClassId);

            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CampusId).HasColumnName("campus_id").IsRequired();
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.Property(e => e.ClassType).HasColumnName("class_type").HasDefaultValue(0);
            entity.Property(e => e.DomainId).HasColumnName("domain_id");
            entity.Property(e => e.DomainCode).HasColumnName("domain_code").HasMaxLength(50);
            entity.Property(e => e.ClusterIndex).HasColumnName("cluster_index");
        });

        // Cấu hình bảng ClassEnrollments
        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.ToTable("ClassEnrollments");
            entity.HasKey(e => e.EnrollmentId);

            entity.Property(e => e.EnrollmentId).HasColumnName("enrollment_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.DiagnosticSubmissionId).HasColumnName("diagnostic_submission_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.EnrolledAt).HasColumnName("enrolled_at");

            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Cấu hình bảng LearningRoadmaps (Core Flow 2)
        modelBuilder.Entity<LearningRoadmap>(entity =>
        {
            entity.ToTable("LearningRoadmaps");
            entity.HasKey(e => e.RoadmapId);

            entity.Property(e => e.RoadmapId).HasColumnName("roadmap_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.DiagnosticSubmissionId).HasColumnName("diagnostic_submission_id");
            entity.Property(e => e.TargetScore).HasColumnName("target_score");
            entity.Property(e => e.TotalMilestones).HasColumnName("total_milestones");
            entity.Property(e => e.CompletedMilestones).HasColumnName("completed_milestones");
            entity.Property(e => e.IsPruned).HasColumnName("is_pruned");
            entity.Property(e => e.PrunedReason).HasColumnName("pruned_reason");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.DiagnosticSubmission)
                  .WithMany()
                  .HasForeignKey(e => e.DiagnosticSubmissionId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Nodes)
                  .WithOne(n => n.Roadmap)
                  .HasForeignKey(n => n.RoadmapId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng RoadmapNodes (Milestones tích hợp 3 thành phần)
        modelBuilder.Entity<RoadmapNode>(entity =>
        {
            entity.ToTable("RoadmapNodes");
            entity.HasKey(e => e.NodeId);

            entity.Property(e => e.NodeId).HasColumnName("node_id");
            entity.Property(e => e.RoadmapId).HasColumnName("roadmap_id").IsRequired();
            entity.Property(e => e.SkillId).HasColumnName("skill_id").IsRequired();
            entity.Property(e => e.StepOrder).HasColumnName("step_order").IsRequired();
            entity.Property(e => e.MaterialId).HasColumnName("material_id");
            entity.Property(e => e.QuizExamId).HasColumnName("quiz_exam_id");
            entity.Property(e => e.LiveSessionId).HasColumnName("live_session_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsPruned).HasColumnName("is_pruned");
            entity.Property(e => e.UnlockedAt).HasColumnName("unlocked_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.VideoWatchedSeconds).HasColumnName("video_watched_seconds");
            entity.Property(e => e.VideoTotalSeconds).HasColumnName("video_total_seconds");
            entity.Property(e => e.IsVideoCompleted).HasColumnName("is_video_completed");
            entity.Property(e => e.QuizScore).HasColumnName("quiz_score");
            entity.Property(e => e.IsQuizPassed).HasColumnName("is_quiz_passed");

            entity.HasOne(e => e.LiveSession)
                  .WithMany(s => s.RoadmapNodes)
                  .HasForeignKey(e => e.LiveSessionId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Cấu hình bảng LiveSessions (Lịch Live Q&A của lớp cơ sở)
        modelBuilder.Entity<LiveSession>(entity =>
        {
            entity.ToTable("LiveSessions");
            entity.HasKey(e => e.SessionId);

            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ScheduledAt).HasColumnName("scheduled_at").IsRequired();
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(e => e.MeetingUrl).HasColumnName("meeting_url");
            entity.Property(e => e.RecordingUrl).HasColumnName("recording_url");
            entity.Property(e => e.IsRecorded).HasColumnName("is_recorded");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Attendances)
                  .WithOne(a => a.Session)
                  .HasForeignKey(a => a.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng LiveSessionAttendance (Điểm danh & Quiz bù)
        modelBuilder.Entity<LiveSessionAttendance>(entity =>
        {
            entity.ToTable("LiveSessionAttendance");
            entity.HasKey(e => e.AttendanceId);

            entity.Property(e => e.AttendanceId).HasColumnName("attendance_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.AttendanceStatus).HasColumnName("attendance_status").HasMaxLength(20).IsRequired();
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at");
            entity.Property(e => e.LeftAt).HasColumnName("left_at");
            entity.Property(e => e.MakeupQuizId).HasColumnName("makeup_quiz_id");
            entity.Property(e => e.IsMakeupQuizPassed).HasColumnName("is_makeup_quiz_passed");
        });
    }
}
