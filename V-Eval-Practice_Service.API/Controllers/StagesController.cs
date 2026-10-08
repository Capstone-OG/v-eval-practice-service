using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.Commands.StartStage;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý chu trình học tập thích ứng P-L-A-R theo chặng (Core Flow 3 - Module 1)
/// </summary>
[Route("api/practice/stages")]
public class StagesController : ApiControllerBase
{
    /// <summary>
    /// Core Flow 3 - API 1: Khởi tạo chặng học thích ứng P-L-A-R
    /// Bắt đầu chu trình bằng bước PREVIEW (3 câu khởi động kiến thức nền tảng)
    /// </summary>
    /// <param name="roadmapNodeId">Mã định danh chặng học (RoadmapNodeId)</param>
    /// <param name="request">Thông tin học sinh</param>
    /// <returns>Tiến trình StageProgress hiện tại và 3 câu Quick Check</returns>
    [HttpPost("{roadmapNodeId:guid}/start")]
    [ProducesResponseType(typeof(Result<StartStageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<StartStageResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<StartStageResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartStage(
        [FromRoute] Guid roadmapNodeId,
        [FromBody] StartStageRequestDto request)
    {
        var command = new StartStageCommand(roadmapNodeId, request.StudentId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Core Flow 3 - API 2: Nộp bài khởi động Preview
    /// Ghi nhận kết quả 3 câu khởi động (không tính BKT) và chuyển bước sang LEARN
    /// </summary>
    /// <param name="stageProgressId">Mã định danh tiến trình chặng học</param>
    /// <param name="request">Danh sách đáp án 3 câu khởi động</param>
    /// <returns>Thông tin tiến trình cập nhật sang bước LEARN và thông tin video bài giảng</returns>
    [HttpPost("{stageProgressId:guid}/preview-submit")]
    [ProducesResponseType(typeof(Result<SubmitPreviewResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<SubmitPreviewResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<SubmitPreviewResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitPreview(
        [FromRoute] Guid stageProgressId,
        [FromBody] SubmitPreviewRequestDto request)
    {
        var command = new V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitPreview.SubmitPreviewCommand(
            stageProgressId,
            request.Answers);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
