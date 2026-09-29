using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CreateLiveSession;

public class CreateLiveSessionCommandValidator : AbstractValidator<CreateLiveSessionCommand>
{
    public CreateLiveSessionCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("ClassId không được để trống.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề buổi học Live không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(15, 300).WithMessage("Thời lượng buổi học phải từ 15 đến 300 phút.");

        RuleFor(x => x.ScheduledAt)
            .NotEmpty().WithMessage("Thời gian lên lịch không được để trống.");
    }
}
