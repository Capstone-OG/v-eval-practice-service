using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// 1.5. Cấu hình xác thực JWT Bearer đồng bộ với Identity Service & API Gateway
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"]
                ?? builder.Configuration["Jwt:SecretKey"]
                ?? "V-Eval_Secure_JWT_Key_2026_Identity_Service_Capstone_Secret!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
                ?? builder.Configuration["Jwt:Issuer"]
                ?? "V-Eval-IdentityService";
var jwtAudience = builder.Configuration["JwtSettings:Audience"]
                  ?? builder.Configuration["Jwt:Audience"]
                  ?? "V-Eval-Clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer),
        ValidIssuer = jwtIssuer,
        ValidateAudience = !string.IsNullOrEmpty(jwtAudience),
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

// 2. Cấu hình Swagger UI trực quan (Hỗ trợ cả JWT Bearer qua API Gateway & Header X-User-Id trực tiếp)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "V-Eval Practice Service API",
        Version = "v1",
        Description = "Microservice tiếp nhận bài thi, chấm điểm tự động, theo dõi năng lực, dự báo điểm SEM/Knapsack ROI, cảnh báo sớm BR-04 và Sổ tay lỗi sai SM-2 (Core Flows 1 - 6)."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Dán JWT Access Token (không cần gõ chữ 'Bearer ') để kiểm thử qua API Gateway (:5212) hoặc trực tiếp.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityDefinition("X-User-Id", new OpenApiSecurityScheme
    {
        Description = "Khi bấm thử trực tiếp tại Swagger Practice Service (:5261), có thể điền nhanh StudentId/MentorId mẫu (VD: b0a218d2-1111-4000-8000-000000000001).",
        Name = "X-User-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-User-Id" }
            },
            Array.Empty<string>()
        }
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
app.UseAuthentication();
app.UseAuthorization();

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

            -- Bổ sung các cột mới cho Core Flow 2 & Enrollment nếu chưa tồn tại
            ALTER TABLE v_eval_practice.""Classes"" ADD COLUMN IF NOT EXISTS course_id UUID;
            ALTER TABLE v_eval_practice.""ClassEnrollments"" ADD COLUMN IF NOT EXISTS registration_id UUID;
            ALTER TABLE v_eval_practice.""LearningRoadmaps"" ADD COLUMN IF NOT EXISTS is_teacher_approved BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE v_eval_practice.""LearningRoadmaps"" ADD COLUMN IF NOT EXISTS approved_by_teacher_id UUID;
            ALTER TABLE v_eval_practice.""LearningRoadmaps"" ADD COLUMN IF NOT EXISTS approved_at TIMESTAMP WITH TIME ZONE;
            ALTER TABLE v_eval_practice.""LearningRoadmaps"" ADD COLUMN IF NOT EXISTS teacher_kickoff_note TEXT;
            ALTER TABLE v_eval_practice.""RoadmapNodes"" ADD COLUMN IF NOT EXISTS daily_exam_id UUID;
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

            ALTER TABLE v_eval_practice.""StageProgress"" ADD COLUMN IF NOT EXISTS preview_submission_json TEXT;

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

            -- Core Flow 3 & 6: Khởi tạo bảng MistakeNotebooks phục vụ Sổ tay lỗi sai & Spaced Repetition (SM-2)
            CREATE TABLE IF NOT EXISTS v_eval_practice.""MistakeNotebooks"" (
                id UUID PRIMARY KEY,
                student_id UUID NOT NULL,
                question_id UUID NOT NULL,
                skill_id UUID NOT NULL,
                pattern_id VARCHAR(100),
                status VARCHAR(50) NOT NULL DEFAULT 'NEW_MISTAKE',
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

            ALTER TABLE v_eval_practice.""MistakeNotebooks"" ADD COLUMN IF NOT EXISTS status VARCHAR(50) NOT NULL DEFAULT 'NEW_MISTAKE';

            CREATE INDEX IF NOT EXISTS idx_mistake_notebook_daily_review 
            ON v_eval_practice.""MistakeNotebooks"" (student_id, next_review_date, is_mastered);

            -- Core Flow 3 (Module 3): Proctoring & Mock Exam fields
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS tab_departure_count INT DEFAULT 0;
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS integrity_status VARCHAR(50) DEFAULT 'NORMAL';
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS mentor_verdict VARCHAR(50) DEFAULT 'PENDING';
            ALTER TABLE v_eval_practice.""ExamSubmissions"" ADD COLUMN IF NOT EXISTS mentor_notes TEXT;
            ALTER TABLE v_eval_practice.""StageProgress"" ADD COLUMN IF NOT EXISTS confidence_rating INT;
            ALTER TABLE v_eval_practice.""StageProgress"" ADD COLUMN IF NOT EXISTS learned_summary TEXT;
            ALTER TABLE v_eval_practice.""StageProgress"" ADD COLUMN IF NOT EXISTS mistake_notes TEXT;

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
                class_id UUID NOT NULL REFERENCES v_eval_practice.""Classes""(class_id) ON DELETE CASCADE,
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
                class_id UUID NOT NULL REFERENCES v_eval_practice.""Classes""(class_id) ON DELETE CASCADE,
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

            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'ClassSkillHeatmaps_class_id_fkey' AND table_schema = 'v_eval_practice') THEN
                    ALTER TABLE v_eval_practice.""ClassSkillHeatmaps"" DROP CONSTRAINT ""ClassSkillHeatmaps_class_id_fkey"";
                    ALTER TABLE v_eval_practice.""ClassSkillHeatmaps"" ADD CONSTRAINT ""ClassSkillHeatmaps_class_id_fkey"" FOREIGN KEY (class_id) REFERENCES v_eval_practice.""Classes""(class_id) ON DELETE CASCADE;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'ClassHeatmapSummaries_class_id_fkey' AND table_schema = 'v_eval_practice') THEN
                    ALTER TABLE v_eval_practice.""ClassHeatmapSummaries"" DROP CONSTRAINT ""ClassHeatmapSummaries_class_id_fkey"";
                    ALTER TABLE v_eval_practice.""ClassHeatmapSummaries"" ADD CONSTRAINT ""ClassHeatmapSummaries_class_id_fkey"" FOREIGN KEY (class_id) REFERENCES v_eval_practice.""Classes""(class_id) ON DELETE CASCADE;
                END IF;
            END $$;

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

            -- =================================================================
            -- MODULE HỌC KỲ (ACADEMIC TERMS), KHUNG LỊCH TUẦN (CLASS SCHEDULES),
            -- BUỔI HỌC THỰC TẾ (CLASS SESSIONS) & ĐIỂM DANH (SESSION ATTENDANCES)
            -- =================================================================
            CREATE SCHEMA IF NOT EXISTS v_eval_enrollment;
            CREATE SCHEMA IF NOT EXISTS v_eval_practice;

            CREATE TABLE IF NOT EXISTS v_eval_enrollment.""AcademicTerms"" (
                term_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                campus_id UUID NOT NULL,
                code VARCHAR(30) UNIQUE NOT NULL,
                name VARCHAR(150) NOT NULL,
                academic_year VARCHAR(20) NOT NULL,
                term_order INT NOT NULL DEFAULT 1,
                start_date DATE NOT NULL,
                end_date DATE NOT NULL,
                status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            ALTER TABLE v_eval_enrollment.""AcademicTerms""
                ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;

            CREATE TABLE IF NOT EXISTS v_eval_enrollment.""Courses"" (
                course_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                campus_id UUID NOT NULL,
                code VARCHAR(50) UNIQUE NOT NULL,
                name VARCHAR(255) NOT NULL,
                plan_type VARCHAR(20) NOT NULL DEFAULT 'STANDARD',
                duration_weeks INT NOT NULL DEFAULT 12,
                tuition_fee NUMERIC(12,2) NOT NULL DEFAULT 0,
                retake_policy VARCHAR(20) NOT NULL DEFAULT 'FREE_1X',
                retake_fee NUMERIC(12,2) NOT NULL DEFAULT 0,
                max_class_size INT NOT NULL DEFAULT 20,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS v_eval_enrollment.""CourseRegistrations"" (
                registration_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                student_id UUID NOT NULL,
                course_id UUID NOT NULL REFERENCES v_eval_enrollment.""Courses""(course_id) ON DELETE RESTRICT,
                term_id UUID REFERENCES v_eval_enrollment.""AcademicTerms""(term_id) ON DELETE SET NULL,
                previous_registration_id UUID REFERENCES v_eval_enrollment.""CourseRegistrations""(registration_id) ON DELETE SET NULL,
                attempt_no INT NOT NULL DEFAULT 1,
                status VARCHAR(30) NOT NULL DEFAULT 'PENDING_PAYMENT',
                registered_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                start_date DATE,
                end_date DATE
            );

            ALTER TABLE v_eval_enrollment.""CourseRegistrations""
                ADD COLUMN IF NOT EXISTS term_id UUID REFERENCES v_eval_enrollment.""AcademicTerms""(term_id) ON DELETE SET NULL;

            ALTER TABLE v_eval_practice.""Classes""
                ADD COLUMN IF NOT EXISTS term_id UUID REFERENCES v_eval_enrollment.""AcademicTerms""(term_id) ON DELETE SET NULL;
            ALTER TABLE v_eval_practice.""Classes""
                ADD COLUMN IF NOT EXISTS academic_year VARCHAR(20) DEFAULT '2026-2027';
            ALTER TABLE v_eval_practice.""Classes""
                ADD COLUMN IF NOT EXISTS max_capacity INT NOT NULL DEFAULT 20;
            ALTER TABLE v_eval_practice.""Classes""
                ADD COLUMN IF NOT EXISTS start_date DATE;
            ALTER TABLE v_eval_practice.""Classes""
                ADD COLUMN IF NOT EXISTS end_date DATE;

            CREATE TABLE IF NOT EXISTS v_eval_practice.""ClassSchedules"" (
                schedule_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                class_id UUID NOT NULL REFERENCES v_eval_practice.""Classes""(class_id) ON DELETE CASCADE,
                day_of_week INT NOT NULL,
                slot_number INT NOT NULL DEFAULT 1,
                start_time TIME NOT NULL,
                end_time TIME NOT NULL,
                room VARCHAR(100) NOT NULL,
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            ALTER TABLE v_eval_practice.""ClassSchedules""
                ADD COLUMN IF NOT EXISTS slot_number INT NOT NULL DEFAULT 1;
            ALTER TABLE v_eval_practice.""ClassSchedules""
                ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();

            CREATE TABLE IF NOT EXISTS v_eval_practice.""ClassSessions"" (
                session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                class_id UUID NOT NULL REFERENCES v_eval_practice.""Classes""(class_id) ON DELETE CASCADE,
                schedule_id UUID REFERENCES v_eval_practice.""ClassSchedules""(schedule_id) ON DELETE SET NULL,
                session_no INT NOT NULL DEFAULT 1,
                session_date DATE NOT NULL,
                start_time TIME NOT NULL,
                end_time TIME NOT NULL,
                room VARCHAR(100) NOT NULL,
                teacher_id UUID,
                skill_id UUID,
                topic_title VARCHAR(255),
                rescheduled_from_id UUID REFERENCES v_eval_practice.""ClassSessions""(session_id) ON DELETE SET NULL,
                status VARCHAR(30) NOT NULL DEFAULT 'SCHEDULED',
                notes TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS schedule_id UUID REFERENCES v_eval_practice.""ClassSchedules""(schedule_id) ON DELETE SET NULL;
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS session_no INT NOT NULL DEFAULT 1;
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS teacher_id UUID;
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS topic_title VARCHAR(255);
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS rescheduled_from_id UUID REFERENCES v_eval_practice.""ClassSessions""(session_id) ON DELETE SET NULL;
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS notes TEXT;
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
            ALTER TABLE v_eval_practice.""ClassSessions""
                ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();

            CREATE INDEX IF NOT EXISTS idx_class_sessions_date_room
                ON v_eval_practice.""ClassSessions"" (session_date, room, start_time, end_time);
            CREATE INDEX IF NOT EXISTS idx_class_sessions_teacher_date
                ON v_eval_practice.""ClassSessions"" (teacher_id, session_date);

            CREATE TABLE IF NOT EXISTS v_eval_practice.""SessionAttendances"" (
                attendance_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                session_id UUID NOT NULL REFERENCES v_eval_practice.""ClassSessions""(session_id) ON DELETE CASCADE,
                student_id UUID NOT NULL,
                enrollment_id UUID REFERENCES v_eval_practice.""ClassEnrollments""(enrollment_id) ON DELETE SET NULL,
                status VARCHAR(30) NOT NULL DEFAULT 'PRESENT',
                check_in_at TIMESTAMPTZ,
                recorded_by UUID,
                note TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT uq_session_attendance_student UNIQUE (session_id, student_id)
            );

            ALTER TABLE v_eval_practice.""SessionAttendances""
                ADD COLUMN IF NOT EXISTS enrollment_id UUID REFERENCES v_eval_practice.""ClassEnrollments""(enrollment_id) ON DELETE SET NULL;
            ALTER TABLE v_eval_practice.""SessionAttendances""
                ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
            ALTER TABLE v_eval_practice.""SessionAttendances""
                ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();

            CREATE INDEX IF NOT EXISTS idx_session_attendances_student
                ON v_eval_practice.""SessionAttendances"" (student_id, status);
        ");
        logger.LogInformation("Đã xác thực và khởi tạo thành công CSDL schema practice trên Supabase.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi kiểm tra hoặc khởi tạo schema practice trên CSDL Supabase.");
    }
}

// 8. Đăng ký Controllers
app.MapControllers();

app.Run();
