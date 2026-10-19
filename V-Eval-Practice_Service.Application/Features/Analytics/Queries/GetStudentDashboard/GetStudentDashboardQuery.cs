using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetStudentDashboard;

/// <summary>
/// Query lấy dữ liệu tổng hợp phục vụ màn hình Dashboard năng lực và chuyên cần của học sinh (Core Flow 5 - API 1)
/// </summary>
public record GetStudentDashboardQuery(Guid StudentId) : IRequest<Result<StudentDashboardResponseDto>>;
