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

            -- Core Flow 6: ScorePredictions & SystemAlerts
            CREATE TABLE IF NOT EXISTS v_eval_practice.""ScorePredictions"" (
                prediction_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                student_id UUID NOT NULL,
                predicted_score INT,
                predicted_score_min INT,
                predicted_score_max INT,
                confidence_rate DOUBLE PRECISION DEFAULT 0.95,
                target_score INT,
                deficit_score INT,
                sem_score DOUBLE PRECISION,
                prescription_items JSONB,
                calculation_source VARCHAR(50) DEFAULT 'MOCK_EXAM',
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE TABLE IF NOT EXISTS v_eval_practice.""SystemAlerts"" (
                alert_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                student_id UUID NOT NULL,
                alert_type VARCHAR(50) NOT NULL,
                message TEXT NOT NULL,
                is_read BOOLEAN DEFAULT FALSE,
                handled_by UUID,
                resolution_notes TEXT,
                resolved_at TIMESTAMP WITH TIME ZONE,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT (now())
            );

            CREATE INDEX IF NOT EXISTS idx_score_predictions_student 
            ON v_eval_practice.""ScorePredictions"" (student_id);

            CREATE INDEX IF NOT EXISTS idx_system_alerts_student 
            ON v_eval_practice.""SystemAlerts"" (student_id);
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

        // Đảm bảo có dữ liệu mẫu ScorePrediction phục vụ kiểm thử Core Flow 6
        var testStudentId = Guid.Parse("b0a218d2-1111-4000-8000-000000000001");
        var samplePrescriptionsJson = @"[
            {
                ""pattern_id"": ""PAT_TOAN_LOGIC_01"",
                ""pattern_name"": ""Quy nạp logic mệnh đề điều kiện"",
                ""skill_id"": ""a0000001-0000-0000-0000-000000000007"",
                ""delta_score"": 25.5,
                ""study_hours_needed"": 1.8,
                ""roi_score"": 14.16,
                ""priority_rank"": 1,
                ""is_remedied"": false
            },
            {
                ""pattern_id"": ""PAT_HOA_PH_03"",
                ""pattern_name"": ""Tính pH dung dịch đệm axit yếu"",
                ""skill_id"": ""a0000001-0000-0000-0000-000000000010"",
                ""delta_score"": 20.0,
                ""study_hours_needed"": 1.5,
                ""roi_score"": 13.33,
                ""priority_rank"": 2,
                ""is_remedied"": false
            }
        ]";

        var existingPrediction = await dbContext.ScorePredictions.FirstOrDefaultAsync(p => p.StudentId == testStudentId);
        if (existingPrediction == null)
        {
            await dbContext.ScorePredictions.AddAsync(new ScorePrediction
            {
                PredictionId = Guid.NewGuid(),
                StudentId = testStudentId,
                PredictedScore = 805,
                PredictedScoreMin = 780,
                PredictedScoreMax = 830,
                ConfidenceRate = 0.95,
                TargetScore = 850,
                DeficitScore = 45,
                SemScore = 25.5,
                PrescriptionItems = samplePrescriptionsJson,
                CalculationSource = "MOCK_EXAM",
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Đã khởi tạo bản ghi ScorePrediction mẫu cho học sinh {StudentId} phục vụ kiểm thử Core Flow 6.", testStudentId);
        }
        else
        {
            existingPrediction.PrescriptionItems = samplePrescriptionsJson;
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Đã làm mới PrescriptionItems chuẩn cho học sinh {StudentId}.", testStudentId);
        }

        // Khởi tạo các cảnh báo mẫu 3 cấp BR-04 phục vụ kiểm thử Core Flow 6
        var testStudentIds = new[] { testStudentId, Guid.Parse("00000000-0000-0000-0000-000000000001") };
        foreach (var sid in testStudentIds)
        {
            var hasAlerts = await dbContext.SystemAlerts.AnyAsync(a => a.StudentId == sid);
            if (!hasAlerts)
            {
                await dbContext.SystemAlerts.AddRangeAsync(
                    new SystemAlert
                    {
                        AlertId = Guid.NewGuid(),
                        StudentId = sid,
                        AlertType = "RED",
                        Message = "Cảnh báo nguy cơ cao: Điểm dự báo hiện tại (805) đang thâm hụt 45 điểm so với mục tiêu (850). Mentor đề xuất làm ngay bài tập bù điểm trong Toa thuốc.",
                        IsRead = false,
                        HandledBy = null,
                        CreatedAt = DateTime.UtcNow.AddHours(-12)
                    },
                    new SystemAlert
                    {
                        AlertId = Guid.NewGuid(),
                        StudentId = sid,
                        AlertType = "YELLOW",
                        Message = "Nhắc nhở học tập: Bạn đang có bài tập vi mô chuyên đề 'Tính pH dung dịch đệm' chưa hoàn thành sau 3 ngày.",
                        IsRead = false,
                        HandledBy = null,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    },
                    new SystemAlert
                    {
                        AlertId = Guid.NewGuid(),
                        StudentId = sid,
                        AlertType = "UNREALISTIC_GOAL",
                        Message = "Mục tiêu điểm số (950) có độ dốc tăng trưởng quá gấp (+250 điểm trong 3 tuần). Mentor đã lên lịch tư vấn định hướng lại lộ trình.",
                        IsRead = true,
                        HandledBy = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                        ResolutionNotes = "Đã tư vấn trực tiếp tại lớp, học sinh đồng thuận điều chỉnh mục tiêu giai đoạn 1 về 850 điểm.",
                        ResolvedAt = DateTime.UtcNow.AddDays(-1),
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    }
                );
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Đã khởi tạo bộ cảnh báo sớm mẫu cho học sinh {StudentId}.", sid);
            }
        }

        // Khởi tạo lớp mẫu cho Mentor nếu chưa có
        var sampleClassId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        var sampleTeacherId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var classObj = await dbContext.Classes.FirstOrDefaultAsync(c => c.ClassId == sampleClassId);
        if (classObj == null)
        {
            classObj = new Class
            {
                ClassId = sampleClassId,
                CampusId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                TeacherId = sampleTeacherId,
                Name = "Lớp Chuyên Đề ĐGNL Tăng Tốc 01",
                Status = "ACTIVE",
                ClassType = 1,
                DomainCode = "DOM_MATH",
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Classes.AddAsync(classObj);
            await dbContext.SaveChangesAsync();

            foreach (var sid in testStudentIds)
            {
                var enrolled = await dbContext.ClassEnrollments.AnyAsync(ce => ce.ClassId == sampleClassId && ce.StudentId == sid);
                if (!enrolled)
                {
                    await dbContext.ClassEnrollments.AddAsync(new ClassEnrollment
                    {
                        EnrollmentId = Guid.NewGuid(),
                        ClassId = sampleClassId,
                        StudentId = sid,
                        Status = "ENROLLED",
                        EnrolledAt = DateTime.UtcNow
                    });
                }
            }
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Đã khởi tạo lớp mẫu {ClassId} và ghi danh học sinh phục vụ Mentor Dashboard.", sampleClassId);
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
