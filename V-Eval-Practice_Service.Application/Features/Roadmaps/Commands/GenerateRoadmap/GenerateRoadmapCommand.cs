using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;

/// <summary>
/// Command khởi tạo lộ trình học tập cá nhân hóa dựa trên kết quả bài thi chẩn đoán (Core Flow 2 - API 1)
/// </summary>
public record GenerateRoadmapCommand(
    Guid StudentId,
    Guid DiagnosticSubmissionId,
    DateTime ExamDate,
    double StudyHoursPerDay = 2.0
) : IRequest<Result<GenerateRoadmapResponseDto>>;
