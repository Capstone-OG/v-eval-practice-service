using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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

var app = builder.Build();

// 5. Middleware xử lý lỗi toàn cục
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseCors("AllowAll");
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
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi kiểm tra hoặc khởi tạo schema practice trên CSDL Supabase.");
    }
}

// 8. Đăng ký Controllers
app.MapControllers();

app.Run();
