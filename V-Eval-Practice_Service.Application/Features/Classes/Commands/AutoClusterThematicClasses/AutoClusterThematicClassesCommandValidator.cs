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
            .InclusiveBetween(1, 24)
            .WithMessage("MaxK phải nằm trong khoảng từ 1 đến 24.");

        RuleFor(x => x.MinCapacity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Sĩ số tối thiểu của một lớp không được nhỏ hơn 1 học sinh.");

        RuleFor(x => x.MaxCapacity)
            .LessThanOrEqualTo(20)
            .WithMessage("Trần sĩ số tối đa của một lớp không được vượt quá 20 học sinh.")
            .GreaterThanOrEqualTo(x => x.MinCapacity)
            .WithMessage("Trần sĩ số tối đa phải lớn hơn hoặc bằng sĩ số tối thiểu.");

        RuleFor(x => x.FoundationThreshold)
            .InclusiveBetween(0.10, 0.80)
            .WithMessage("Ngưỡng Cơ bản phải nằm trong khoảng 10% - 80%.");

        RuleFor(x => x.AdvancedThreshold)
            .GreaterThan(x => x.FoundationThreshold)
            .WithMessage("Ngưỡng Nâng cao phải lớn hơn ngưỡng Cơ bản.")
            .LessThanOrEqualTo(0.95)
            .WithMessage("Ngưỡng Nâng cao không được vượt quá 95%.");
    }
}
