using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.Commands.LogTabDeparture;
using V_Eval_Practice_Service.Application.Features.MockExams.Commands.StartMockExam;
using V_Eval_Practice_Service.Application.Features.MockExams.Commands.SubmitMockExam;
using V_Eval_Practice_Service.Application.Features.MockExams.Commands.UploadSnapshot;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý Phòng thi thử mô phỏng có giám sát &amp; Thẩm định Mentor (Core Flow 3 - Module 3)
/// </summary>
[Route("api/practice/mock-exams")]
public class MockExamsController : ApiControllerBase
{
    /// <summary>
    /// API 1: Khởi tạo phiên thi thử chuẩn hóa 120 câu / 150 phút (Mock Exam)
    /// </summary>
    /// <remarks>
    /// Bắt đầu đếm ngược thời gian 150 phút, lấy 120 câu hỏi bảo mật từ Content Service (ẩn đáp án đúng),
    /// và thiết lập trạng thái giám sát thi cử (Proctoring).
    /// </remarks>
    /// <param name="examId">Mã đề thi thử</param>
    /// <param name="request">Thông tin học sinh đăng ký làm bài</param>
    /// <returns>Phiên thi thử khởi tạo cùng danh sách câu hỏi và đồng hồ đếm ngược</returns>
    [HttpPost("{examId:guid}/start")]
    [ProducesResponseType(typeof(Result<StartMockExamResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<StartMockExamResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<StartMockExamResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartMockExam(
        [FromRoute] Guid examId,
        [FromBody] StartMockExamRequestDto request)
    {
        var command = new StartMockExamCommand(examId, request.StudentId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// API 2: Ghi nhận sự kiện rời tab hoặc thoát toàn màn hình (Log Tab Departure - BR-20)
    /// </summary>
    /// <remarks>
    /// Bắt sự kiện chuyển tab, thu nhỏ trình duyệt hoặc thoát fullscreen từ Client.
    /// Nếu rời tab quá 3 lần, hệ thống tự động cắm cờ bài thi (FLAGGED_FOR_REVIEW) để Mentor thẩm định.
    /// </remarks>
    /// <param name="submissionId">Mã phiên nộp bài thi thử</param>
    /// <param name="request">Thông tin lý do rời tab (tùy chọn)</param>
    /// <returns>Số lần vi phạm hiện tại, trạng thái liêm chính và thông điệp cảnh báo</returns>
    [HttpPost("{submissionId:guid}/log-tab-departure")]
    [ProducesResponseType(typeof(Result<LogTabDepartureResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<LogTabDepartureResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<LogTabDepartureResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LogTabDeparture(
        [FromRoute] Guid submissionId,
        [FromBody] LogTabDepartureRequestDto? request)
    {
        var command = new LogTabDepartureCommand(submissionId, request?.Reason);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// API 3: Tải lên ảnh chụp webcam ngẫu nhiên định kỳ (Proctoring Snapshot)
    /// </summary>
    /// <remarks>
    /// Lưu URL hoặc dữ liệu ảnh chụp ngẫu nhiên từ camera của thí sinh kèm cờ nhận diện khuôn mặt (FaceDetected).
    /// Phục vụ thiết lập chuỗi bằng chứng kiểm toán thị giác (Visual Audit Trail) cho Mentor thẩm định.
    /// </remarks>
    /// <param name="submissionId">Mã phiên nộp bài thi thử</param>
    /// <param name="request">Thông tin ảnh chụp và trạng thái nhận diện khuôn mặt</param>
    /// <returns>Bản ghi ảnh chụp được lưu trữ và tổng số ảnh hiện có</returns>
    [HttpPost("{submissionId:guid}/upload-snapshot")]
    [ProducesResponseType(typeof(Result<UploadSnapshotResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<UploadSnapshotResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<UploadSnapshotResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadSnapshot(
        [FromRoute] Guid submissionId,
        [FromBody] UploadSnapshotRequestDto request)
    {
        var command = new UploadSnapshotCommand(
            submissionId, request.SnapshotUrl, request.FaceDetected);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// API 4: Nộp bài thi thử, chấm điểm &amp; Phân luồng giám sát liêm chính (Submit Mock Exam)
    /// </summary>
    /// <remarks>
    /// Chấm điểm toàn bài thi, đối chiếu đáp án với Content Service, ghi nhận chi tiết từng câu.
    /// Nếu bài thi có dấu hiệu gian lận (FLAGGED_FOR_REVIEW) do rời tab &gt; 3 lần, trạng thái chuyển thành UNDER_REVIEW (chờ Mentor duyệt).
    /// Các câu trả lời sai được tự động đẩy vào Sổ tay lỗi sai (Mistake Notebook) phục vụ ôn tập ngắt quãng SM-2.
    /// Điểm thành phần các kỹ năng được cập nhật vào LearningProfiles phục vụ phân nhóm bàn học vi mô (Micro-groups 3-5 bạn) tuần kế tiếp.
    /// </remarks>
    /// <param name="submissionId">Mã phiên nộp bài thi thử</param>
    /// <param name="request">Danh sách các câu trả lời của thí sinh</param>
    /// <returns>Bảng điểm tổng, trạng thái liêm chính, số câu sai nạp vào sổ tay và chi tiết từng câu</returns>
    [HttpPost("{submissionId:guid}/submit")]
    [ProducesResponseType(typeof(Result<SubmitMockExamResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<SubmitMockExamResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<SubmitMockExamResponseDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<SubmitMockExamResponseDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitMockExam(
        [FromRoute] Guid submissionId,
        [FromBody] SubmitMockExamRequestDto request)
    {
        var command = new SubmitMockExamCommand(submissionId, request.Answers);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
