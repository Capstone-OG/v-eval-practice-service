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

        RuleFor(x => x.MinCapacity)
            .GreaterThanOrEqualTo(5)
            .WithMessage("Sĩ số tối thiểu của một lớp chuyên đề không được nhỏ hơn 5 học sinh.");

        RuleFor(x => x.MaxCapacity)
            .LessThanOrEqualTo(20)
            .WithMessage("Trần sĩ số tối đa của một lớp chuyên đề không được vượt quá 20 học sinh.")
            .GreaterThanOrEqualTo(x => x.MinCapacity)
            .WithMessage("Trần sĩ số tối đa phải lớn hơn hoặc bằng sĩ số tối thiểu.");
    }
}
