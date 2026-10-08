using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.Commands.ReflectComplete;
using V_Eval_Practice_Service.Application.Features.Stages.Commands.StartStage;
using V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitAnswer;
using V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitPreview;
using V_Eval_Practice_Service.Application.Features.Stages.Commands.TrackVideo;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;
using V_Eval_Practice_Service.Application.Features.Stages.Queries.GetNextQuestion;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý chu trình tự học thích ứng P-L-A-R theo từng chặng (Core Flow 3)
/// </summary>
[Route("api/practice/stages")]
public class StagesController : ApiControllerBase
{
    /// <summary>
    /// Bước 1: Khởi tạo chặng học thích ứng (P - Preview)
    /// </summary>
    /// <remarks>
    /// Khởi tạo tiến trình StageProgress ở bước PREVIEW và nạp 3 câu hỏi Quick Check khởi động kích hoạt phản xạ nền tảng.
    /// </remarks>
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
    /// Bước 2: Nộp bài khởi động &amp; Mở khóa lý thuyết (P sang L)
    /// </summary>
    /// <remarks>
    /// Chấm điểm 3 câu khởi động (không tính vào BKT), tự động chuyển State Machine sang LEARN và cấp đường dẫn video bài giảng.
    /// </remarks>
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
        var command = new SubmitPreviewCommand(stageProgressId, request.Answers);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Bước 3: Ghi nhận thời lượng xem video bài giảng (L - Learn)
    /// </summary>
    /// <remarks>
    /// Tính tỷ lệ phần trăm xem bài giảng lũy tiến. Khi học sinh xem đạt tối thiểu 80% thời lượng, tự động mở khóa bước luyện tập thích ứng APPLY.
    /// </remarks>
    /// <param name="stageProgressId">Mã định danh tiến trình chặng học</param>
    /// <param name="request">Thời lượng đã xem và tổng thời lượng video</param>
    /// <returns>Tiến độ xem cập nhật và trạng thái chuyển bước APPLY</returns>
    [HttpPost("{stageProgressId:guid}/track-video")]
    [ProducesResponseType(typeof(Result<TrackVideoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<TrackVideoResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<TrackVideoResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrackVideo(
        [FromRoute] Guid stageProgressId,
        [FromBody] TrackVideoRequestDto request)
    {
        var command = new TrackVideoCommand(stageProgressId, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Bước 4: Lấy câu hỏi thích ứng tiếp theo (A - Apply)
    /// </summary>
    /// <remarks>
    /// Áp dụng mô hình IRT 2PL lọc câu hỏi tối ưu trong vùng phát triển gần nhất (ZPD: xác suất trả lời đúng trong dải 0.60 - 0.75), ẩn đáp án đúng để bảo mật.
    /// </remarks>
    /// <param name="stageProgressId">Mã định danh tiến trình chặng học</param>
    /// <returns>Câu hỏi thích ứng tiếp theo kèm thông số độ khó và độ thành thạo hiện tại</returns>
    [HttpGet("{stageProgressId:guid}/next-question")]
    [ProducesResponseType(typeof(Result<NextQuestionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<NextQuestionResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<NextQuestionResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNextQuestion([FromRoute] Guid stageProgressId)
    {
        var query = new GetNextQuestionQuery(stageProgressId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Bước 5: Nộp đáp án thích ứng &amp; Động cơ BKT (A - Apply)
    /// </summary>
    /// <remarks>
    /// Đánh giá đúng/sai, phạt đoán mò thời gian nhanh (t &lt; 5s và b &gt;= 0.50), cập nhật xác suất thành thạo BKT P(Lt). Kiểm tra quy tắc đạt chuẩn thành thạo BR-01 và rẽ nhánh phụ đạo BR-03.
    /// </remarks>
    /// <param name="stageProgressId">Mã định danh tiến trình chặng học</param>
    /// <param name="request">Thông tin câu trả lời và thời gian làm bài</param>
    /// <returns>Kết quả câu trả lời, xác suất P(Lt) mới và chỉ dẫn hành động tiếp theo</returns>
    [HttpPost("{stageProgressId:guid}/submit-answer")]
    [ProducesResponseType(typeof(Result<SubmitAnswerResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<SubmitAnswerResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<SubmitAnswerResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] Guid stageProgressId,
        [FromBody] SubmitAnswerRequestDto request)
    {
        var command = new SubmitAnswerCommand(stageProgressId, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Bước 6: Phản tư cá nhân &amp; Hoàn thành chặng học (R - Reflect)
    /// </summary>
    /// <remarks>
    /// Ghi nhận đánh giá độ tự tin (1-5 sao) và ghi chú rút kinh nghiệm, đánh dấu chặng COMPLETED và tự động kích hoạt mở khóa chặng tiếp theo trên lộ trình.
    /// </remarks>
    /// <param name="stageProgressId">Mã định danh tiến trình chặng học</param>
    /// <param name="request">Đánh giá độ tự tin và bài học rút ra</param>
    /// <returns>Thông tin chặng học đã hoàn thành và mã chặng tiếp theo được mở khóa</returns>
    [HttpPost("{stageProgressId:guid}/reflect-complete")]
    [ProducesResponseType(typeof(Result<ReflectCompleteResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ReflectCompleteResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<ReflectCompleteResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReflectComplete(
        [FromRoute] Guid stageProgressId,
        [FromBody] ReflectCompleteRequestDto request)
    {
        var command = new ReflectCompleteCommand(stageProgressId, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
