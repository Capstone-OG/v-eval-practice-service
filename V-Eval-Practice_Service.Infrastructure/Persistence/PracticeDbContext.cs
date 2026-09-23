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
    }
}
