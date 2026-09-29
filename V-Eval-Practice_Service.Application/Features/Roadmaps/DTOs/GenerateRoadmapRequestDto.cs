using System;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO yêu cầu khởi tạo lộ trình học tập thích ứng (Core Flow 2 - API 1)
/// </summary>
public record GenerateRoadmapRequestDto(
    Guid DiagnosticSubmissionId,
    DateTime ExamDate,
    double StudyHoursPerDay = 2.0,
    Guid? StudentId = null
);
