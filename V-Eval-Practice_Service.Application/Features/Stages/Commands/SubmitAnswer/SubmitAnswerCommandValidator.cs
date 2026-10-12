using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitAnswer;

public class SubmitAnswerCommandValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerCommandValidator()
    {
        RuleFor(x => x.StageProgressId)
            .NotEmpty().WithMessage("StageProgressId không được để trống.");

        RuleFor(x => x.Request.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.Request.QuestionId)
            .NotEmpty().WithMessage("QuestionId không được để trống.");

        RuleFor(x => x.Request.SelectedOption)
            .NotEmpty().WithMessage("SelectedOption không được để trống.");

        RuleFor(x => x.Request.TimeSpentSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("TimeSpentSeconds phải >= 0.");
    }
}
