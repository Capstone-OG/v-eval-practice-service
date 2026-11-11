using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.ReflectComplete;

public class ReflectCompleteCommandValidator : AbstractValidator<ReflectCompleteCommand>
{
    public ReflectCompleteCommandValidator()
    {
        RuleFor(x => x.StageProgressId)
            .NotEmpty().WithMessage("StageProgressId không được để trống.");

        RuleFor(x => x.Request.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.Request.ConfidenceRating)
            .InclusiveBetween(1, 5).WithMessage("Đánh giá độ tự tin phải nằm trong khoảng từ 1 đến 5 sao.");
    }
}
