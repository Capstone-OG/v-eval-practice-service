using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Payments.Commands.CreatePaymentLink;

public class CreatePaymentLinkCommandValidator : AbstractValidator<CreatePaymentLinkCommand>
{
    public CreatePaymentLinkCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.ParentId)
            .NotEmpty().WithMessage("ParentId không được để trống.");

        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("CourseId không được để trống.");

        RuleFor(x => x.ReturnUrl)
            .NotEmpty().WithMessage("ReturnUrl không được để trống.");

        RuleFor(x => x.CancelUrl)
            .NotEmpty().WithMessage("CancelUrl không được để trống.");
    }
}

