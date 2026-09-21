using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using V_Eval_Practice_Service.API.Middlewares;
using V_Eval_Practice_Service.Application;
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

            CREATE TABLE IF NOT EXISTS practice.submission_answers (
                answer_id UUID PRIMARY KEY,
                submission_id UUID NOT NULL REFERENCES practice.exam_submissions(submission_id) ON DELETE CASCADE,
                question_id UUID NOT NULL,
                selected_option VARCHAR(10),
                is_correct BOOLEAN NOT NULL,
                time_spent_seconds INT NOT NULL
            );
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
