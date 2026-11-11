using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassHeatmap;

/// <summary>
/// Query lấy ma trận nhiệt 2 chiều của lớp học, danh sách cột kỹ năng, hàng học sinh và cảnh báo sư phạm (Core Flow 5 - API 2)
/// </summary>
public record GetClassHeatmapQuery(
    Guid ClassId,
    string? DomainCode = null,
    double? MinMastery = null
) : IRequest<Result<ClassHeatmapResponseDto>>;
