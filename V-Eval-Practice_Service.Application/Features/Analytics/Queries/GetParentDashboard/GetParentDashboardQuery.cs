using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetParentDashboard;

/// <summary>
/// Query lấy dữ liệu học tập tổng hợp của tất cả con cái liên kết với tài khoản phụ huynh (Core Flow 5 - API 4)
/// </summary>
public record GetParentDashboardQuery(Guid ParentId) : IRequest<Result<ParentDashboardResponseDto>>;
