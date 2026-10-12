using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Commands.SubmitDailyReview;

public class SubmitDailyReviewCommandValidator : AbstractValidator<SubmitDailyReviewCommand>
{
    public SubmitDailyReviewCommandValidator()
    {
        RuleFor(x => x.MistakeNotebookId)
            .NotEmpty().WithMessage("Mã bản ghi Sổ tay lỗi sai không được để trống.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Dữ liệu nộp bài ôn tập không được null.");

        RuleFor(x => x.Request.SelectedOption)
            .NotEmpty().WithMessage("Phương án đã chọn không được để trống.");
    }
}
