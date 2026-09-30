using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoClusterThematicClasses;

public class AutoClusterThematicClassesCommandValidator : AbstractValidator<AutoClusterThematicClassesCommand>
{
    public AutoClusterThematicClassesCommandValidator()
    {
        RuleFor(x => x.CampusId)
            .NotEmpty()
            .WithMessage("CampusId không được để trống.");

        RuleFor(x => x.MaxK)
            .InclusiveBetween(2, 10)
            .WithMessage("MaxK phải nằm trong khoảng từ 2 đến 10.");
    }
}
