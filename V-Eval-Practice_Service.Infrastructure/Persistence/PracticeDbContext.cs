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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình bảng exam_submissions thuộc schema practice
        modelBuilder.Entity<ExamSubmission>(entity =>
        {
            entity.ToTable("exam_submissions", "practice");
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

            entity.HasMany(e => e.Answers)
                  .WithOne(a => a.Submission)
                  .HasForeignKey(a => a.SubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng submission_answers thuộc schema practice
        modelBuilder.Entity<SubmissionAnswer>(entity =>
        {
            entity.ToTable("submission_answers", "practice");
            entity.HasKey(e => e.AnswerId);

            entity.Property(e => e.AnswerId).HasColumnName("answer_id");
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.SelectedOption).HasColumnName("selected_option").HasMaxLength(10);
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.TimeSpentSeconds).HasColumnName("time_spent_seconds");
        });
    }
}
