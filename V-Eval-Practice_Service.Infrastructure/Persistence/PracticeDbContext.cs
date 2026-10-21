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
    public DbSet<StageProgress> StageProgresses => Set<StageProgress>();
    public DbSet<AdaptiveQuizAttempt> AdaptiveQuizAttempts => Set<AdaptiveQuizAttempt>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<ClassGroupMember> ClassGroupMembers => Set<ClassGroupMember>();
    public DbSet<MistakeNotebook> MistakeNotebooks => Set<MistakeNotebook>();
    public DbSet<ProctoringSnapshot> ProctoringSnapshots => Set<ProctoringSnapshot>();
    public DbSet<ScorePrediction> ScorePredictions => Set<ScorePrediction>();
    public DbSet<SystemAlert> SystemAlerts => Set<SystemAlert>();

    // Core Flow 5: Analytics & Class Heatmap
    public DbSet<ClassSkillHeatmap> ClassSkillHeatmaps => Set<ClassSkillHeatmap>();
    public DbSet<ClassHeatmapSummary> ClassHeatmapSummaries => Set<ClassHeatmapSummary>();
    public DbSet<StudentDailyActivity> StudentDailyActivities => Set<StudentDailyActivity>();
    public DbSet<StudentReadinessMetric> StudentReadinessMetrics => Set<StudentReadinessMetric>();

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

            // Module 3: Proctoring & Integrity fields
            entity.Property(e => e.TabDepartureCount).HasColumnName("tab_departure_count");
            entity.Property(e => e.IntegrityStatus).HasColumnName("integrity_status").HasMaxLength(50);
            entity.Property(e => e.MentorVerdict).HasColumnName("mentor_verdict").HasMaxLength(50);
            entity.Property(e => e.MentorNotes).HasColumnName("mentor_notes");

            entity.HasMany(e => e.Answers)
                  .WithOne(a => a.Submission)
                  .HasForeignKey(a => a.SubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ProctoringSnapshots)
                  .WithOne(p => p.Submission)
                  .HasForeignKey(p => p.SubmissionId)
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

        // Cấu hình bảng ClassGroups (Nhóm học tập vi mô 3 - 5 học sinh)
        modelBuilder.Entity<ClassGroup>(entity =>
        {
            entity.ToTable("ClassGroups");
            entity.HasKey(e => e.GroupId);

            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(e => e.GroupName).HasColumnName("group_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FocusArea).HasColumnName("focus_area").HasMaxLength(255);
            entity.Property(e => e.CommonWeakSkillIds).HasColumnName("common_weak_skill_ids");
            entity.Property(e => e.RecommendedWorksheetTitle).HasColumnName("recommended_worksheet_title").HasMaxLength(255);
            entity.Property(e => e.AssignedWorksheetId).HasColumnName("assigned_worksheet_id").HasMaxLength(100);
            entity.Property(e => e.AssignedWorksheetTitle).HasColumnName("assigned_worksheet_title").HasMaxLength(255);
            entity.Property(e => e.WorksheetAssignedAt).HasColumnName("worksheet_assigned_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Members)
                  .WithOne(m => m.Group)
                  .HasForeignKey(m => m.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng ClassGroupMembers
        modelBuilder.Entity<ClassGroupMember>(entity =>
        {
            entity.ToTable("ClassGroupMembers");
            entity.HasKey(e => e.GroupMemberId);

            entity.Property(e => e.GroupMemberId).HasColumnName("group_member_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at");
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

        // Cấu hình bảng StageProgress (Core Flow 3 - Tiến trình chặng học P-L-A-R)
        modelBuilder.Entity<StageProgress>(entity =>
        {
            entity.ToTable("StageProgress");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.RoadmapNodeId).HasColumnName("roadmap_node_id").IsRequired();
            entity.Property(e => e.CurrentStep).HasColumnName("current_step").HasMaxLength(20).IsRequired();
            entity.Property(e => e.VideoWatchPercentage).HasColumnName("video_watch_percentage").HasPrecision(5, 2);
            entity.Property(e => e.BktMasteryPlt).HasColumnName("bkt_mastery_plt");
            entity.Property(e => e.ConsecutiveAdvancedCorrect).HasColumnName("consecutive_advanced_correct");
            entity.Property(e => e.ConsecutiveIncorrect).HasColumnName("consecutive_incorrect");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.RoadmapNode)
                  .WithMany()
                  .HasForeignKey(e => e.RoadmapNodeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.AdaptiveAttempts)
                  .WithOne(a => a.StageProgress)
                  .HasForeignKey(a => a.StageProgressId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng AdaptiveQuizAttempt (Core Flow 3 - Nhật ký trả lời câu hỏi thích ứng)
        modelBuilder.Entity<AdaptiveQuizAttempt>(entity =>
        {
            entity.ToTable("AdaptiveQuizAttempts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StageProgressId).HasColumnName("stage_progress_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.PatternId).HasColumnName("pattern_id").HasMaxLength(100);
            entity.Property(e => e.SelectedOption).HasColumnName("selected_option").HasMaxLength(10);
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.TimeSpentSeconds).HasColumnName("time_spent_seconds");
            entity.Property(e => e.ItemDifficultyB).HasColumnName("item_difficulty_b");
            entity.Property(e => e.ItemDiscriminationA).HasColumnName("item_discrimination_a");
            entity.Property(e => e.IsLuckyGuess).HasColumnName("is_lucky_guess");
            entity.Property(e => e.PriorPlt).HasColumnName("prior_plt");
            entity.Property(e => e.PosteriorPlt).HasColumnName("posterior_plt");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // Cấu hình bảng MistakeNotebooks (Core Flow 3 - Sổ tay lỗi sai & Lặp lại ngắt quãng SM-2)
        modelBuilder.Entity<MistakeNotebook>(entity =>
        {
            entity.ToTable("MistakeNotebooks");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.SkillId).HasColumnName("skill_id").IsRequired();
            entity.Property(e => e.PatternId).HasColumnName("pattern_id").HasMaxLength(100);
            entity.Property(e => e.CognitiveErrorTag).HasColumnName("cognitive_error_tag").HasMaxLength(50);
            entity.Property(e => e.StudentNotes).HasColumnName("student_notes");
            entity.Property(e => e.NextReviewDate).HasColumnName("next_review_date").HasColumnType("date");
            entity.Property(e => e.ReviewCount).HasColumnName("review_count");
            entity.Property(e => e.ConsecutiveCorrectReviews).HasColumnName("consecutive_correct_reviews");
            entity.Property(e => e.IntervalDays).HasColumnName("interval_days");
            entity.Property(e => e.EaseFactor).HasColumnName("ease_factor");
            entity.Property(e => e.IsMastered).HasColumnName("is_mastered");
            entity.Property(e => e.LastReviewedAt).HasColumnName("last_reviewed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        // Cấu hình bảng ProctoringSnapshots (Core Flow 3 - Module 3 Ảnh chụp webcam giám sát thi thử)
        modelBuilder.Entity<ProctoringSnapshot>(entity =>
        {
            entity.ToTable("ProctoringSnapshots");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(e => e.SnapshotUrl).HasColumnName("snapshot_url").IsRequired();
            entity.Property(e => e.CapturedAt).HasColumnName("captured_at");
            entity.Property(e => e.FaceDetected).HasColumnName("face_detected");
        });

        // Cấu hình bảng ClassSkillHeatmaps (Core Flow 5 - Từng ô ma trận nhiệt lớp học)
        modelBuilder.Entity<ClassSkillHeatmap>(entity =>
        {
            entity.ToTable("ClassSkillHeatmaps", "v_eval_practice");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.SkillId).HasColumnName("skill_id").IsRequired();
            entity.Property(e => e.SkillName).HasColumnName("skill_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.DomainCode).HasColumnName("domain_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PltValue).HasColumnName("plt_value");
            entity.Property(e => e.MasteryLevel).HasColumnName("mastery_level").HasMaxLength(30).IsRequired();
            entity.Property(e => e.LastAssessmentType).HasColumnName("last_assessment_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.ClassId, e.StudentId, e.SkillId })
                  .IsUnique()
                  .HasDatabaseName("uq_class_student_skill");

            entity.HasIndex(e => e.ClassId)
                  .HasDatabaseName("idx_heatmaps_class_id");

            entity.HasIndex(e => e.StudentId)
                  .HasDatabaseName("idx_heatmaps_student_id");

            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng ClassHeatmapSummaries (Core Flow 5 - Tổng hợp cột kỹ năng lớp học)
        modelBuilder.Entity<ClassHeatmapSummary>(entity =>
        {
            entity.ToTable("ClassHeatmapSummaries", "v_eval_practice");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(e => e.SkillId).HasColumnName("skill_id").IsRequired();
            entity.Property(e => e.SkillName).HasColumnName("skill_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.DomainCode).HasColumnName("domain_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.AveragePlt).HasColumnName("average_plt");
            entity.Property(e => e.StrugglingCount).HasColumnName("struggling_count");
            entity.Property(e => e.TotalStudents).HasColumnName("total_students");
            entity.Property(e => e.StrugglingRate).HasColumnName("struggling_rate");
            entity.Property(e => e.StdDevPlt).HasColumnName("std_dev_plt");
            entity.Property(e => e.AlertStatus).HasColumnName("alert_status").HasMaxLength(30).IsRequired();
            entity.Property(e => e.IsBimodal).HasColumnName("is_bimodal");
            entity.Property(e => e.CalculatedAt).HasColumnName("calculated_at");

            entity.HasIndex(e => new { e.ClassId, e.SkillId })
                  .IsUnique()
                  .HasDatabaseName("uq_class_skill_summary");

            entity.HasIndex(e => new { e.ClassId, e.AlertStatus })
                  .HasDatabaseName("idx_heatmap_summaries_class_status");

            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng StudentDailyActivities (Core Flow 5 - Chuyên cần & Streak)
        modelBuilder.Entity<StudentDailyActivity>(entity =>
        {
            entity.ToTable("StudentDailyActivities", "v_eval_practice");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.ActivityDate).HasColumnName("activity_date").HasColumnType("date").IsRequired();
            entity.Property(e => e.StudyTimeMinutes).HasColumnName("study_time_minutes");
            entity.Property(e => e.PracticeQuestionsSolved).HasColumnName("practice_questions_solved");
            entity.Property(e => e.VideoMinutesWatched).HasColumnName("video_minutes_watched");
            entity.Property(e => e.MockExamsTaken).HasColumnName("mock_exams_taken");
            entity.Property(e => e.IsStreakCounted).HasColumnName("is_streak_counted");
            entity.Property(e => e.IsFrozen).HasColumnName("is_frozen");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.StudentId, e.ActivityDate })
                  .IsUnique()
                  .HasDatabaseName("uq_student_activity_date");

            entity.HasIndex(e => new { e.StudentId, e.ActivityDate })
                  .HasDatabaseName("idx_student_activity_date");
        });

        // Cấu hình bảng StudentReadinessMetrics (Core Flow 5 - Chỉ số sẵn sàng thi)
        modelBuilder.Entity<StudentReadinessMetric>(entity =>
        {
            entity.ToTable("StudentReadinessMetrics", "v_eval_practice");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.NormalizedTheta).HasColumnName("normalized_theta");
            entity.Property(e => e.SkillCoveragePlt).HasColumnName("skill_coverage_plt");
            entity.Property(e => e.ConsistencyScore).HasColumnName("consistency_score");
            entity.Property(e => e.ExamReadinessIndex).HasColumnName("exam_readiness_index");
            entity.Property(e => e.ReadinessTier).HasColumnName("readiness_tier").HasMaxLength(30).IsRequired();
            entity.Property(e => e.StudyHoursThisWeek).HasColumnName("study_hours_this_week");
            entity.Property(e => e.CurrentStreakDays).HasColumnName("current_streak_days");
            entity.Property(e => e.CalculatedAt).HasColumnName("calculated_at");

            entity.HasIndex(e => e.StudentId)
                  .IsUnique()
                  .HasDatabaseName("idx_readiness_student_id");
        });

        // Cấu hình bảng ScorePredictions (Core Flow 6: Dự báo khoảng điểm & Toa thuốc Knapsack)
        modelBuilder.Entity<ScorePrediction>(entity =>
        {
            entity.ToTable("ScorePredictions");
            entity.HasKey(e => e.PredictionId);

            entity.Property(e => e.PredictionId).HasColumnName("prediction_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.PredictedScore).HasColumnName("predicted_score");
            entity.Property(e => e.PredictedScoreMin).HasColumnName("predicted_score_min");
            entity.Property(e => e.PredictedScoreMax).HasColumnName("predicted_score_max");
            entity.Property(e => e.ConfidenceRate).HasColumnName("confidence_rate");
            entity.Property(e => e.TargetScore).HasColumnName("target_score");
            entity.Property(e => e.DeficitScore).HasColumnName("deficit_score");
            entity.Property(e => e.SemScore).HasColumnName("sem_score");
            entity.Property(e => e.PrescriptionItems).HasColumnName("prescription_items").HasColumnType("jsonb");
            entity.Property(e => e.CalculationSource).HasColumnName("calculation_source").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // Cấu hình bảng SystemAlerts (Core Flow 6: Hệ thống cảnh báo sớm 3 cấp BR-04)
        modelBuilder.Entity<SystemAlert>(entity =>
        {
            entity.ToTable("SystemAlerts");
            entity.HasKey(e => e.AlertId);

            entity.Property(e => e.AlertId).HasColumnName("alert_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.AlertType).HasColumnName("alert_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").IsRequired();
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.HandledBy).HasColumnName("handled_by");
            entity.Property(e => e.ResolutionNotes).HasColumnName("resolution_notes");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}

