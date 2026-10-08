using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitPreview;

public class SubmitPreviewCommandHandler : IRequestHandler<SubmitPreviewCommand, Result<SubmitPreviewResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly ILogger<SubmitPreviewCommandHandler> _logger;

    public SubmitPreviewCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILearningRoadmapRepository roadmapRepository,
        ILogger<SubmitPreviewCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _roadmapRepository = roadmapRepository;
        _logger = logger;
    }

    public async Task<Result<SubmitPreviewResponseDto>> Handle(SubmitPreviewCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Xử lý nộp bài khởi động Preview cho tiến trình {StageProgressId}", request.StageProgressId);

        // 1. Kiểm tra tồn tại của StageProgress
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<SubmitPreviewResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        // 2. Chấm điểm khởi động khách quan (không tính vào BKT, chỉ để kích hoạt nhận thức)
        int totalQuestions = request.Answers.Count;
        int totalCorrect = request.Answers.Count(a => !string.IsNullOrWhiteSpace(a.SelectedOption));

        // 3. Chuyển bước State Machine từ PREVIEW sang LEARN
        progress.CurrentStep = "LEARN";
        progress.UpdatedAt = DateTime.UtcNow;

        await _stageProgressRepository.UpdateAsync(progress, ct);
        _logger.LogInformation("Tiến trình {StageProgressId} đã hoàn thành bước PREVIEW, chuyển sang bước LEARN.", progress.Id);

        // 4. Lấy thông tin video bài giảng phương pháp từ RoadmapNode
        Guid? materialId = progress.RoadmapNode?.MaterialId;
        string videoUrl = "https://storage.googleapis.com/veval-materials/videos/phuong_phap_giai_chuyen_de.mp4";

        var response = new SubmitPreviewResponseDto(
            progress.Id,
            progress.RoadmapNodeId,
            progress.CurrentStep,
            progress.Status,
            totalQuestions,
            totalCorrect,
            "Hoàn thành phần khởi động! Hãy xem video bài giảng phương pháp giải (tối thiểu 80% thời lượng) để mở khóa bước luyện tập thích ứng.",
            "WATCH_VIDEO",
            materialId,
            videoUrl
        );

        return Result<SubmitPreviewResponseDto>.Success(response);
    }
}
