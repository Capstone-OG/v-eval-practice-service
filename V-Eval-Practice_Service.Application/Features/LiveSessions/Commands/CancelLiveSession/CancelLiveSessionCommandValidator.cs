using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CancelLiveSession;

public class CancelLiveSessionCommandValidator : AbstractValidator<CancelLiveSessionCommand>
{
    public CancelLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy buổi học không được để trống.")
            .MaximumLength(500).WithMessage("Lý do hủy buổi học không được vượt quá 500 ký tự.");
    }
}
