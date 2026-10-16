using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.LogTabDeparture;

public class LogTabDepartureCommandHandler : IRequestHandler<LogTabDepartureCommand, Result<LogTabDepartureResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly ILogger<LogTabDepartureCommandHandler> _logger;

    public LogTabDepartureCommandHandler(
        IMockExamRepository mockExamRepository,
        ILogger<LogTabDepartureCommandHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _logger = logger;
    }

    public async Task<Result<LogTabDepartureResponseDto>> Handle(
        LogTabDepartureCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ghi nhận sự kiện rời tab cho Bài thi {SubmissionId}. Lý do: {Reason}",
            request.SubmissionId, request.Reason ?? "Chuyển cửa sổ / Thoát toàn màn hình");

        // 1. Kiểm tra phiên thi tồn tại
        var submission = await _mockExamRepository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<LogTabDepartureResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy phiên làm bài thi thử với ID {request.SubmissionId}."));
        }

        // 2. Kiểm tra trạng thái bài thi phải đang trong tiến trình (IN_PROGRESS)
        if (submission.Status != "IN_PROGRESS")
        {
            return Result<LogTabDepartureResponseDto>.Failure(
                Error.Validation("Submission.NotActive",
                    $"Phiên thi thử {request.SubmissionId} đã kết thúc với trạng thái '{submission.Status}'. Không thể ghi nhận thêm sự kiện vi phạm."));
        }

        // 3. Tăng số lần rời tab và kiểm tra quy tắc liêm chính học thuật (BR-20)
        submission.TabDepartureCount += 1;
        bool isFlagged = submission.TabDepartureCount > 3;

        if (isFlagged)
        {
            submission.IntegrityStatus = "FLAGGED_FOR_REVIEW";
            _logger.LogWarning("Bài thi {SubmissionId} bị cắm cờ gian lận (FLAGGED_FOR_REVIEW) do rời tab {Count} lần (> 3 lần)",
                submission.SubmissionId, submission.TabDepartureCount);
        }

        await _mockExamRepository.UpdateAsync(submission, cancellationToken);
        await _mockExamRepository.SaveChangesAsync(cancellationToken);

        // 4. Tạo thông điệp cảnh báo sư phạm
        string warningMessage = isFlagged
            ? $"CẢNH BÁO NGHIÊM TRỌNG: Bạn đã rời khỏi màn hình làm bài {submission.TabDepartureCount} lần (vượt quá giới hạn cho phép 3 lần). Bài thi của bạn đã bị gắn cờ nghi vấn gian lận (FLAGGED_FOR_REVIEW) và sẽ được Mentor thẩm định sau khi nộp bài!"
            : $"CẢNH BÁO LIÊM CHÍNH: Bạn đã rời khỏi màn hình làm bài {submission.TabDepartureCount}/3 lần. Nếu rời màn hình quá 3 lần, bài thi sẽ bị đánh cờ gian lận!";

        var response = new LogTabDepartureResponseDto(
            SubmissionId: submission.SubmissionId,
            TabDepartureCount: submission.TabDepartureCount,
            IntegrityStatus: submission.IntegrityStatus,
            IsFlagged: isFlagged,
            WarningMessage: warningMessage,
            LoggedAt: DateTime.UtcNow
        );

        return Result<LogTabDepartureResponseDto>.Success(response);
    }
}
