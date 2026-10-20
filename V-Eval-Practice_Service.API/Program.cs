using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using V_Eval_Practice_Service.API.Middlewares;
using V_Eval_Practice_Service.Application;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure;
using V_Eval_Practice_Service.Infrastructure.Persistence;

// Enable unencrypted HTTP/2 support for gRPC clients
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký Controllers & API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Cấu hình Swagger UI trực quan
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "V-Eval Practice Service API",
        Version = "v1",
        Description = "Microservice tiếp nhận bài thi, chấm điểm tự động và lưu trữ kết quả kiểm tra năng lực (V-Eval Core Flow 1 - Bước 3)."
    });

    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// 3. Đăng ký các tầng Clean Architecture
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 4. Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 5. Cấu hình Rate Limiting (Giới hạn tốc độ gọi AI Tutor)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("StrictTutorPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// 6. Middleware xử lý lỗi toàn cục & Rate Limiter
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseStaticFiles();

// 6. Kích hoạt Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "V-Eval Practice Service API v1");
    c.RoutePrefix = "swagger";
});

// 7. Tự động kiểm tra & khởi tạo schema practice trên CSDL Supabase
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = services.GetRequiredService<PracticeDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE SCHEMA IF NOT EXISTS practice;

            CREATE TABLE IF NOT EXISTS practice.exam_submissions (
                submission_id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                exam_id UUID NOT NULL,
                exam_type VARCHAR(50) NOT NULL,
                total_score INT NOT NULL,
                total_correct INT NOT NULL,
                total_questions INT NOT NULL,
                total_time_spent_seconds INT NOT NULL,
                started_at TIMESTAMP WITH TIME ZONE NOT NULL,
                completed_at TIMESTAMP WITH TIME ZONE NOT NULL,
                status VARCHAR(50) NOT NULL
            );

            -- Bổ sung các cột mới cho Core Flow 1 nếu chưa tồn tại
            ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS theta_0 DOUBLE PRECISION;
            ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS placement_class VARCHAR(50);
            ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS ai_commentary TEXT;
            ALTER TABLE practice.exam_submissions ADD COLUMN IF NOT EXISTS enrolled_class_id UUID;

            CREATE TABLE IF NOT EXISTS practice.submission_answers (
                answer_id UUID PRIMARY KEY,
                submission_id UUID NOT NULL REFERENCES practice.exam_submissions(submission_id) ON DELETE CASCADE,
                question_id UUID NOT NULL,
                selected_option VARCHAR(10),
                is_correct BOOLEAN NOT NULL,
                time_spent_seconds INT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ""LearningProfiles"" (
                profile_id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                skill_id UUID NOT NULL,
                mastery_score DOUBLE PRECISION DEFAULT 0.0,
                last_updated TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE TABLE IF NOT EXISTS ""Classes"" (
                class_id UUID PRIMARY KEY,
                campus_id UUID NOT NULL,
                teacher_id UUID,
                assigned_by UUID,
                name VARCHAR(255) NOT NULL,
                start_date DATE,
                end_date DATE,
                status VARCHAR(50) DEFAULT 'ACTIVE',
                assigned_at TIMESTAMP WITH TIME ZONE,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE TABLE IF NOT EXISTS ""ClassEnrollments"" (
                enrollment_id UUID PRIMARY KEY,
                class_id UUID NOT NULL,
                student_id UUID NOT NULL,
                diagnostic_submission_id UUID,
                approved_by UUID,
                status VARCHAR(50) DEFAULT 'ENROLLED',
                enrolled_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE TABLE IF NOT EXISTS ""ClassGroups"" (
                group_id UUID PRIMARY KEY,
                class_id UUID NOT NULL REFERENCES ""Classes""(class_id) ON DELETE CASCADE,
                group_name VARCHAR(255) NOT NULL,
                focus_area VARCHAR(255),
                common_weak_skill_ids TEXT,
                recommended_worksheet_title VARCHAR(255),
                assigned_worksheet_id VARCHAR(100),
                assigned_worksheet_title VARCHAR(255),
                worksheet_assigned_at TIMESTAMP WITH TIME ZONE,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE TABLE IF NOT EXISTS ""ClassGroupMembers"" (
                group_member_id UUID PRIMARY KEY,
                group_id UUID NOT NULL REFERENCES ""ClassGroups""(group_id) ON DELETE CASCADE,
                student_id UUID NOT NULL,
                joined_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            -- Bổ sung các cột mới cho Core Flow 2 nếu chưa tồn tại
            ALTER TABLE v_eval_practice.""RoadmapNodes"" ADD COLUMN IF NOT EXISTS video_watched_seconds INT DEFAULT 0;
            ALTER TABLE v_eval_practice.""RoadmapNodes"" ADD COLUMN IF NOT EXISTS video_total_seconds INT DEFAULT 0;
            ALTER TABLE v_eval_practice.""RoadmapNodes"" ADD COLUMN IF NOT EXISTS is_video_completed BOOLEAN DEFAULT FALSE;
            ALTER TABLE v_eval_practice.""RoadmapNodes"" ADD COLUMN IF NOT EXISTS quiz_score DOUBLE PRECISION DEFAULT 0.0;
            ALTER TABLE v_eval_practice.""RoadmapNodes"" ADD COLUMN IF NOT EXISTS is_quiz_passed BOOLEAN DEFAULT FALSE;

            -- Core Flow 3: Khởi tạo bảng StageProgress và AdaptiveQuizAttempts
            CREATE TABLE IF NOT EXISTS v_eval_practice.""StageProgress"" (
                id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                roadmap_node_id UUID NOT NULL,
                current_step VARCHAR(20) NOT NULL DEFAULT 'PREVIEW',
                video_watch_percentage NUMERIC(5,2) DEFAULT 0.00,
                bkt_mastery_plt DOUBLE PRECISION DEFAULT 0.1000,
                consecutive_advanced_correct INT DEFAULT 0,
                consecutive_incorrect INT DEFAULT 0,
                status VARCHAR(20) NOT NULL DEFAULT 'IN_PROGRESS',
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE TABLE IF NOT EXISTS v_eval_practice.""AdaptiveQuizAttempts"" (
                id UUID PRIMARY KEY,
                stage_progress_id UUID NOT NULL,
                student_id UUID NOT NULL,
                question_id UUID NOT NULL,
                pattern_id VARCHAR(100),
                selected_option VARCHAR(10),
                is_correct BOOLEAN NOT NULL,
                time_spent_seconds INT NOT NULL,
                item_difficulty_b DOUBLE PRECISION NOT NULL,
                item_discrimination_a DOUBLE PRECISION NOT NULL,
                is_lucky_guess BOOLEAN DEFAULT FALSE,
                prior_plt DOUBLE PRECISION NOT NULL,
                posterior_plt DOUBLE PRECISION NOT NULL,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            -- Core Flow 3: Khởi tạo bảng MistakeNotebooks phục vụ Sổ tay lỗi sai & Spaced Repetition (Module 2)
            CREATE TABLE IF NOT EXISTS v_eval_practice.""MistakeNotebooks"" (
                id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                question_id UUID NOT NULL,
                skill_id UUID NOT NULL,
                pattern_id VARCHAR(100),
                cognitive_error_tag VARCHAR(50),
                student_notes TEXT,
                next_review_date DATE NOT NULL,
                review_count INT DEFAULT 0,
                consecutive_correct_reviews INT DEFAULT 0,
                interval_days INT DEFAULT 1,
                ease_factor DOUBLE PRECISION DEFAULT 2.50,
                is_mastered BOOLEAN DEFAULT FALSE,
                last_reviewed_at TIMESTAMP WITH TIME ZONE,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE INDEX IF NOT EXISTS idx_mistake_notebook_daily_review 
            ON v_eval_practice.""MistakeNotebooks"" (student_id, next_review_date, is_mastered);

            -- Core Flow 3 (Module 3): Proctoring & Mock Exam fields
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS tab_departure_count INT DEFAULT 0;
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS integrity_status VARCHAR(50) DEFAULT 'NORMAL';
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS mentor_verdict VARCHAR(50) DEFAULT 'PENDING';
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS mentor_notes TEXT;

            CREATE TABLE IF NOT EXISTS v_eval_practice.""SubmissionAnswers"" (
                answer_id UUID PRIMARY KEY,
                submission_id UUID NOT NULL REFERENCES v_eval_practice.""ExamSubmissions""(submission_id) ON DELETE CASCADE,
                question_id UUID NOT NULL,
                selected_option VARCHAR(10),
                is_correct BOOLEAN NOT NULL,
                time_spent_seconds INT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS v_eval_practice.""ProctoringSnapshots"" (
                id UUID PRIMARY KEY,
                submission_id UUID NOT NULL REFERENCES v_eval_practice.""ExamSubmissions""(submission_id) ON DELETE CASCADE,
                snapshot_url TEXT NOT NULL,
                captured_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                face_detected BOOLEAN DEFAULT TRUE
            );

            CREATE INDEX IF NOT EXISTS idx_proctoring_snapshots_sub 
            ON v_eval_practice.""ProctoringSnapshots"" (submission_id, captured_at);

            -- Core Flow 5: Analytics & Class Heatmap
            CREATE TABLE IF NOT EXISTS v_eval_practice.""ClassSkillHeatmaps"" (
                id UUID PRIMARY KEY,
                class_id UUID NOT NULL REFERENCES ""Classes""(class_id) ON DELETE CASCADE,
                student_id UUID NOT NULL,
                skill_id UUID NOT NULL,
                skill_name VARCHAR(255) NOT NULL,
                domain_code VARCHAR(50) NOT NULL,
                plt_value DOUBLE PRECISION NOT NULL DEFAULT 0.1000,
                mastery_level VARCHAR(30) NOT NULL DEFAULT 'WEAK',
                last_assessment_type VARCHAR(50) NOT NULL DEFAULT 'DIAGNOSTIC',
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                CONSTRAINT uq_class_student_skill UNIQUE (class_id, student_id, skill_id)
            );

            CREATE INDEX IF NOT EXISTS idx_heatmaps_class_id ON v_eval_practice.""ClassSkillHeatmaps""(class_id);
            CREATE INDEX IF NOT EXISTS idx_heatmaps_student_id ON v_eval_practice.""ClassSkillHeatmaps""(student_id);

            CREATE TABLE IF NOT EXISTS v_eval_practice.""ClassHeatmapSummaries"" (
                id UUID PRIMARY KEY,
                class_id UUID NOT NULL REFERENCES ""Classes""(class_id) ON DELETE CASCADE,
                skill_id UUID NOT NULL,
                skill_name VARCHAR(255) NOT NULL,
                domain_code VARCHAR(50) NOT NULL,
                average_plt DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                struggling_count INT NOT NULL DEFAULT 0,
                total_students INT NOT NULL DEFAULT 0,
                struggling_rate DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                std_dev_plt DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                alert_status VARCHAR(30) NOT NULL DEFAULT 'SAFE_GREEN',
                is_bimodal BOOLEAN NOT NULL DEFAULT FALSE,
                calculated_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                CONSTRAINT uq_class_skill_summary UNIQUE (class_id, skill_id)
            );

            CREATE INDEX IF NOT EXISTS idx_heatmap_summaries_class_status 
            ON v_eval_practice.""ClassHeatmapSummaries""(class_id, alert_status);

            CREATE TABLE IF NOT EXISTS v_eval_practice.""StudentDailyActivities"" (
                id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                activity_date DATE NOT NULL,
                study_time_minutes INT NOT NULL DEFAULT 0,
                practice_questions_solved INT NOT NULL DEFAULT 0,
                video_minutes_watched INT NOT NULL DEFAULT 0,
                mock_exams_taken INT NOT NULL DEFAULT 0,
                is_streak_counted BOOLEAN NOT NULL DEFAULT FALSE,
                is_frozen BOOLEAN NOT NULL DEFAULT FALSE,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                CONSTRAINT uq_student_activity_date UNIQUE (student_id, activity_date)
            );

            CREATE INDEX IF NOT EXISTS idx_student_activity_date 
            ON v_eval_practice.""StudentDailyActivities""(student_id, activity_date DESC);

            CREATE TABLE IF NOT EXISTS v_eval_practice.""StudentReadinessMetrics"" (
                id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                normalized_theta DOUBLE PRECISION NOT NULL DEFAULT 50.0,
                skill_coverage_plt DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                consistency_score DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                exam_readiness_index DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                readiness_tier VARCHAR(30) NOT NULL DEFAULT 'PREPARING',
                study_hours_this_week DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                current_streak_days INT NOT NULL DEFAULT 0,
                calculated_at TIMESTAMP WITH TIME ZONE DEFAULT (now()),
                CONSTRAINT uq_student_readiness UNIQUE (student_id)
            );

            CREATE INDEX IF NOT EXISTS idx_readiness_student_id 
            ON v_eval_practice.""StudentReadinessMetrics""(student_id);
        ");
        logger.LogInformation("Đã xác thực và khởi tạo thành công CSDL schema practice trên Supabase.");

        // Đảm bảo có dữ liệu mẫu RoadmapNode phục vụ kiểm thử Core Flow 3
        var sampleNodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var sampleRoadmapId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var sampleStudentId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var sampleSkillId = Guid.Parse("6f3765db-943e-4810-bf74-d6a8bdc215da"); // DOM_MATH

        var nodeExists = await dbContext.RoadmapNodes.AnyAsync(n => n.NodeId == sampleNodeId);
        if (!nodeExists)
        {
            var roadmapExists = await dbContext.LearningRoadmaps.AnyAsync(r => r.RoadmapId == sampleRoadmapId);
            if (!roadmapExists)
            {
                await dbContext.LearningRoadmaps.AddAsync(new LearningRoadmap
                {
                    RoadmapId = sampleRoadmapId,
                    StudentId = sampleStudentId,
                    TargetScore = 800,
                    TotalMilestones = 1,
                    CompletedMilestones = 0,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await dbContext.RoadmapNodes.AddAsync(new RoadmapNode
            {
                NodeId = sampleNodeId,
                RoadmapId = sampleRoadmapId,
                SkillId = sampleSkillId,
                StepOrder = 1,
                Status = "IN_PROGRESS",
                UnlockedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Đã khởi tạo chặng học mẫu RoadmapNode {NodeId} phục vụ kiểm thử Core Flow 3.", sampleNodeId);
        }

        // Đảm bảo có dữ liệu mẫu Class & Heatmap phục vụ kiểm thử Core Flow 5
        var sampleClassId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var classExists = await dbContext.Classes.AnyAsync(c => c.ClassId == sampleClassId);
        if (!classExists)
        {
            var sampleCampusId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var sampleClass = new Class
            {
                ClassId = sampleClassId,
                CampusId = sampleCampusId,
                Name = "Lớp ĐGNL Cơ sở 1 - ĐHQG TP.HCM",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Classes.AddAsync(sampleClass);

            // 5 học sinh mẫu
            var demoStudents = new[]
            {
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), // Phạm Minh Hoàng
                Guid.Parse("4fa85f64-5717-4562-b3fc-2c963f66afa6"), // Lê Bảo Trâm
                Guid.Parse("5fa85f64-5717-4562-b3fc-2c963f66afa6"), // Trần Tuấn Kiệt
                Guid.Parse("6fa85f64-5717-4562-b3fc-2c963f66afa6"), // Nguyễn Hà My
                Guid.Parse("7fa85f64-5717-4562-b3fc-2c963f66afa6")  // Vũ Hoàng Long
            };

            foreach (var stId in demoStudents)
            {
                await dbContext.ClassEnrollments.AddAsync(new ClassEnrollment
                {
                    EnrollmentId = Guid.NewGuid(),
                    ClassId = sampleClassId,
                    StudentId = stId,
                    Status = "ENROLLED",
                    EnrolledAt = DateTime.UtcNow
                });
            }

            // 4 Kỹ năng mẫu đại diện 4 phân môn lớn
            var demoSkills = new[]
            {
                (Guid.Parse("88888888-9999-aaaa-bbbb-cccccccccccc"), "Toán logic mệnh đề & suy luận kéo theo", "DOM_MATH", 0.42),
                (Guid.Parse("99999999-0000-bbbb-cccc-dddddddddddd"), "Đọc hiểu phân tích biểu đồ & bảng biểu", "DOM_MATH", 0.74),
                (Guid.Parse("aaaaaaaa-1111-bbbb-cccc-dddddddddddd"), "Sử dụng từ Hán Việt & Phong cách văn bản", "DOM_LANG", 0.78),
                (Guid.Parse("bbbbbbbb-2222-cccc-dddd-eeeeeeeeeeee"), "Đọc hiểu văn bản Khoa học Tự nhiên", "DOM_NAT_SCI", 0.65)
            };

            // Gieo các ô điểm nhiệt P(Lt). Cố tình để kỹ năng 1 có 3/5 em < 0.50 (tỷ lệ yếu 60%) để kích hoạt cảnh báo đỏ CRITICAL_RED!
            foreach (var (skId, skName, domCode, basePlt) in demoSkills)
            {
                for (int idx = 0; idx < demoStudents.Length; idx++)
                {
                    var stId = demoStudents[idx];
                    double plt = basePlt;
                    if (skId == Guid.Parse("88888888-9999-aaaa-bbbb-cccccccccccc"))
                    {
                        plt = idx < 3 ? 0.35 + idx * 0.05 : 0.75 + idx * 0.05;
                    }

                    await dbContext.ClassSkillHeatmaps.AddAsync(new ClassSkillHeatmap
                    {
                        Id = Guid.NewGuid(),
                        ClassId = sampleClassId,
                        StudentId = stId,
                        SkillId = skId,
                        SkillName = skName,
                        DomainCode = domCode,
                        PltValue = Math.Round(plt, 2),
                        MasteryLevel = plt < 0.50 ? "WEAK" : "MASTERED",
                        LastAssessmentType = "DIAGNOSTIC",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Seed dữ liệu chuyên cần và chỉ số sẵn sàng thi cho học sinh Hoàng (3fa85f64-5717-4562-b3fc-2c963f66afa6)
            var primaryStudentId = demoStudents[0];
            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            for (int d = 6; d >= 0; d--)
            {
                var actDate = today.AddDays(-d);
                await dbContext.StudentDailyActivities.AddAsync(new StudentDailyActivity
                {
                    Id = Guid.NewGuid(),
                    StudentId = primaryStudentId,
                    ActivityDate = actDate,
                    StudyTimeMinutes = 120,
                    PracticeQuestionsSolved = 15,
                    VideoMinutesWatched = 25,
                    MockExamsTaken = d == 1 ? 1 : 0,
                    IsStreakCounted = true,
                    IsFrozen = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await dbContext.StudentReadinessMetrics.AddAsync(new StudentReadinessMetric
            {
                Id = Guid.NewGuid(),
                StudentId = primaryStudentId,
                NormalizedTheta = 81.2,
                SkillCoveragePlt = 79.5,
                ConsistencyScore = 95.0,
                ExamReadinessIndex = 82.4,
                ReadinessTier = "READY",
                StudyHoursThisWeek = 14.5,
                CurrentStreakDays = 12,
                CalculatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Đã khởi tạo thành công Lớp học mẫu {ClassId} và ma trận nhiệt phục vụ Core Flow 5.", sampleClassId);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi kiểm tra hoặc khởi tạo schema practice trên CSDL Supabase.");
    }
}

// 8. Đăng ký Controllers
app.MapControllers();

app.Run();
