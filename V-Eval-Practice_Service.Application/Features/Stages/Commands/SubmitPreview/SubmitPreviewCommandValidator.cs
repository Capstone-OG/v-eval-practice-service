using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitPreview;

public class SubmitPreviewCommandValidator : AbstractValidator<SubmitPreviewCommand>
{
    public SubmitPreviewCommandValidator()
    {
        RuleFor(x => x.StageProgressId)
            .NotEmpty().WithMessage("StageProgressId không được để trống.");

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Danh sách câu trả lời không được null.")
            .Must(a => a != null && a.Count > 0).WithMessage("Phải nộp ít nhất 1 câu trả lời khởi động.");
    }
}
