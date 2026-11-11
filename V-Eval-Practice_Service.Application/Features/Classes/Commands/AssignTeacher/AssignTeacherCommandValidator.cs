using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignTeacher;

public class AssignTeacherCommandValidator : AbstractValidator<AssignTeacherCommand>
{
    public AssignTeacherCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("ClassId không được để trống.");

        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("TeacherId không được để trống.");
    }
}
