using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Analytics.Queries.GetCampusComparativeHeatmap;

/// <summary>
/// Query lấy bản đồ nhiệt so sánh mức độ làm chủ 4 phân môn giữa các lớp tại một cơ sở (Core Flow 5 - API 5)
/// </summary>
public record GetCampusComparativeHeatmapQuery(Guid CampusId) : IRequest<Result<CampusComparativeHeatmapResponseDto>>;
