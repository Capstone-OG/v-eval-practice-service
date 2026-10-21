using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.ForceAggregateHeatmap;

public class ForceAggregateHeatmapCommandHandler : IRequestHandler<ForceAggregateHeatmapCommand, Result<ForceAggregateResponseDto>>
{
    private readonly IClassHeatmapRepository _heatmapRepo;
    private readonly IClassHeatmapAggregatorService _aggregatorService;
    private readonly ILogger<ForceAggregateHeatmapCommandHandler> _logger;

    public ForceAggregateHeatmapCommandHandler(
        IClassHeatmapRepository heatmapRepo,
        IClassHeatmapAggregatorService aggregatorService,
        ILogger<ForceAggregateHeatmapCommandHandler> logger)
    {
        _heatmapRepo = heatmapRepo;
        _aggregatorService = aggregatorService;
        _logger = logger;
    }

    public async Task<Result<ForceAggregateResponseDto>> Handle(ForceAggregateHeatmapCommand request, CancellationToken ct)
    {
        var classEntity = await _heatmapRepo.GetClassByIdAsync(request.ClassId, ct);
        if (classEntity == null)
        {
            return Result<ForceAggregateResponseDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học với ID {request.ClassId}."));
        }

        _logger.LogInformation("[Force Aggregate] Giáo viên/Quản trị viên kích hoạt tính toán lại ma trận nhiệt cho Lớp {ClassId}.", request.ClassId);

        await _aggregatorService.AggregateClassHeatmapAsync(request.ClassId, ct);

        var summaries = await _heatmapRepo.GetSummariesByClassIdAsync(request.ClassId, ct);

        var response = new ForceAggregateResponseDto(
            ClassId: request.ClassId,
            AggregatedAt: DateTime.UtcNow,
            TotalSkillsCalculated: summaries.Count,
            Message: $"Đã tính toán lại ma trận nhiệt thành công cho {summaries.Count} kỹ năng của lớp và làm mới bộ đệm Cache."
        );

        return Result<ForceAggregateResponseDto>.Success(response);
    }
}
