using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.JoinLiveSession;

public class JoinLiveSessionCommandValidator : AbstractValidator<JoinLiveSessionCommand>
{
    public JoinLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");
    }
}
