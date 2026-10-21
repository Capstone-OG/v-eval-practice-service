using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.UploadSnapshot;

public class UploadSnapshotCommandValidator : AbstractValidator<UploadSnapshotCommand>
{
    public UploadSnapshotCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài nộp thi thử (SubmissionId) không được để trống.");

        RuleFor(x => x.SnapshotUrl)
            .NotEmpty().WithMessage("Đường dẫn ảnh chụp webcam (SnapshotUrl) không được để trống.");
    }
}
