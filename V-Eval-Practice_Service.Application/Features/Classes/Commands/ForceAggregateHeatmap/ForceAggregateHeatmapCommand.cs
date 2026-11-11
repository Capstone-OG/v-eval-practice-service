using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.ForceAggregateHeatmap;

/// <summary>
/// Command kích hoạt tính toán lại ma trận nhiệt thủ công cho một lớp học (Core Flow 5 - API 6)
/// </summary>
public record ForceAggregateHeatmapCommand(Guid ClassId) : IRequest<Result<ForceAggregateResponseDto>>;
