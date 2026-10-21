using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.UploadSnapshot;

public class UploadSnapshotCommandHandler : IRequestHandler<UploadSnapshotCommand, Result<UploadSnapshotResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly ILogger<UploadSnapshotCommandHandler> _logger;

    public UploadSnapshotCommandHandler(
        IMockExamRepository mockExamRepository,
        ILogger<UploadSnapshotCommandHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _logger = logger;
    }

    public async Task<Result<UploadSnapshotResponseDto>> Handle(
        UploadSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nhận ảnh webcam giám sát cho Bài thi {SubmissionId}. FaceDetected={FaceDetected}",
            request.SubmissionId, request.FaceDetected);

        // 1. Kiểm tra phiên thi tồn tại
        var submission = await _mockExamRepository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<UploadSnapshotResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy phiên làm bài thi thử với ID {request.SubmissionId}."));
        }

        // 2. Kiểm tra trạng thái bài thi phải đang diễn ra (IN_PROGRESS)
        if (submission.Status != "IN_PROGRESS")
        {
            return Result<UploadSnapshotResponseDto>.Failure(
                Error.Validation("Submission.NotActive",
                    $"Phiên thi thử {request.SubmissionId} đã ở trạng thái '{submission.Status}'. Không thể lưu thêm ảnh chụp webcam."));
        }

        // 3. Khởi tạo bản ghi ảnh chụp webcam
        var snapshot = new ProctoringSnapshot
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            SnapshotUrl = request.SnapshotUrl,
            CapturedAt = DateTime.UtcNow,
            FaceDetected = request.FaceDetected
        };

        await _mockExamRepository.AddSnapshotAsync(snapshot, cancellationToken);
        await _mockExamRepository.SaveChangesAsync(cancellationToken);

        // 4. Lấy tổng số lượng ảnh chụp trong phiên thi này
        var allSnapshots = await _mockExamRepository.GetSnapshotsBySubmissionIdAsync(submission.SubmissionId, cancellationToken);

        if (!request.FaceDetected)
        {
            _logger.LogWarning("Ảnh chụp {SnapshotId} của Bài thi {SubmissionId} KHÔNG PHÁT HIỆN ĐƯỢC KHUÔN MẶT!",
                snapshot.Id, submission.SubmissionId);
        }

        var response = new UploadSnapshotResponseDto(
            SnapshotId: snapshot.Id,
            SubmissionId: submission.SubmissionId,
            SnapshotUrl: snapshot.SnapshotUrl,
            CapturedAt: snapshot.CapturedAt,
            FaceDetected: snapshot.FaceDetected,
            TotalSnapshotsCount: allSnapshots.Count,
            Message: request.FaceDetected
                ? "Lưu ảnh chụp webcam giám sát thành công. Nhận diện khuôn mặt hợp lệ."
                : "CẢNH BÁO: Ảnh chụp webcam không phát hiện thấy khuôn mặt học sinh!"
        );

        return Result<UploadSnapshotResponseDto>.Success(response);
    }
}
