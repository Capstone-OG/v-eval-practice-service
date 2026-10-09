using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.Commands.SubmitDailyReview;
using V_Eval_Practice_Service.Application.Features.Mistakes.Commands.TagCognitiveError;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;
using V_Eval_Practice_Service.Application.Features.Mistakes.Queries.GetDailyReview;
using V_Eval_Practice_Service.Application.Features.Mistakes.Queries.GetMistakeNotebook;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý Sổ tay lỗi sai &amp; Lặp lại ngắt quãng SM-2 (Core Flow 3 - Module 2)
/// </summary>
[Route("api/practice/mistakes")]
public class MistakesController : ApiControllerBase
{
    /// <summary>
    /// Xem Sổ tay lỗi sai cá nhân
    /// </summary>
    /// <remarks>
    /// Truy xuất danh sách các câu làm sai của học sinh kèm bộ lọc theo Chuyên đề, Nhãn nhận thức và Trạng thái đã thành thạo hay chưa.
    /// </remarks>
    /// <param name="studentId">Mã định danh học sinh</param>
    /// <param name="skillId">Mã chuyên đề/kỹ năng cần lọc (tùy chọn)</param>
    /// <param name="cognitiveErrorTag">Nhãn nhận thức: CARELESS, MISREAD_QUESTION, MISSING_CONCEPT (tùy chọn)</param>
    /// <param name="isMastered">Lọc theo trạng thái đã khắc phục triệt để hay chưa (tùy chọn)</param>
    /// <param name="pageIndex">Trang hiện tại (mặc định 1)</param>
    /// <param name="pageSize">Số lượng bản ghi mỗi trang (mặc định 10)</param>
    /// <returns>Danh sách câu sai kèm thống kê phân trang</returns>
    [HttpGet]
    [ProducesResponseType(typeof(Result<MistakeNotebookListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<MistakeNotebookListResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMistakeNotebook(
        [FromQuery] Guid studentId,
        [FromQuery] Guid? skillId = null,
        [FromQuery] string? cognitiveErrorTag = null,
        [FromQuery] bool? isMastered = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetMistakeNotebookQuery(studentId, skillId, cognitiveErrorTag, isMastered, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy nhiệm vụ ôn tập hôm nay (Spaced Repetition)
    /// </summary>
    /// <remarks>
    /// Quét các câu hỏi đến hạn ôn tập theo lịch SM-2 và tự động bốc câu hỏi biến thể (Isomorphic Variant) từ Ngân hàng đề của Content Service để học sinh củng cố trí nhớ dài hạn.
    /// </remarks>
    /// <param name="studentId">Mã định danh học sinh</param>
    /// <param name="targetDate">Ngày kiểm tra nhiệm vụ ôn tập (mặc định hôm nay UTC)</param>
    /// <returns>Danh sách các câu hỏi biến thể cần ôn tập hôm nay</returns>
    [HttpGet("daily-review")]
    [ProducesResponseType(typeof(Result<DailyReviewListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<DailyReviewListResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDailyReview(
        [FromQuery] Guid studentId,
        [FromQuery] DateTime? targetDate = null)
    {
        var query = new GetDailyReviewQuery(studentId, targetDate);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Gắn nhãn nhận thức nguyên nhân sai (Metacognition)
    /// </summary>
    /// <remarks>
    /// Học sinh tự phản tư lý do làm sai: CARELESS (Tính ẩu), MISREAD_QUESTION (Đọc sót đề), MISSING_CONCEPT (Hổng lý thuyết) kèm ghi chú bài học kinh nghiệm cá nhân.
    /// </remarks>
    /// <param name="id">Mã định danh bản ghi trong Sổ tay lỗi sai</param>
    /// <param name="request">Nhãn nhận thức và ghi chú bài học</param>
    /// <returns>Kết quả cập nhật nhãn nhận thức</returns>
    [HttpPost("{id:guid}/tag-error")]
    [ProducesResponseType(typeof(Result<TagCognitiveErrorResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<TagCognitiveErrorResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<TagCognitiveErrorResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TagCognitiveError(
        [FromRoute] Guid id,
        [FromBody] TagCognitiveErrorRequestDto request)
    {
        var command = new TagCognitiveErrorCommand(id, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Nộp bài ôn tập &amp; Cập nhật lịch SM-2
    /// </summary>
    /// <remarks>
    /// Chấm điểm câu hỏi ôn tập, tính toán khoảng cách ngày tiếp theo theo thuật toán SuperMemo-2 (SM-2) và tự động gắn cờ IsMastered khi học sinh làm đúng liên tiếp 3 lần.
    /// </remarks>
    /// <param name="id">Mã định danh bản ghi trong Sổ tay lỗi sai</param>
    /// <param name="request">Phương án lựa chọn và thời gian làm bài</param>
    /// <returns>Kết quả làm bài và ngày hẹn ôn tập tiếp theo</returns>
    [HttpPost("{id:guid}/review-submit")]
    [ProducesResponseType(typeof(Result<SubmitDailyReviewResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<SubmitDailyReviewResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<SubmitDailyReviewResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitDailyReview(
        [FromRoute] Guid id,
        [FromBody] SubmitDailyReviewRequestDto request)
    {
        var command = new SubmitDailyReviewCommand(id, request);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
