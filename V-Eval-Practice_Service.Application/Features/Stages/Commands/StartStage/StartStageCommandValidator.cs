using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.StartStage;

public class StartStageCommandValidator : AbstractValidator<StartStageCommand>
{
    public StartStageCommandValidator()
    {
        RuleFor(x => x.RoadmapNodeId)
            .NotEmpty().WithMessage("RoadmapNodeId không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");
    }
}
