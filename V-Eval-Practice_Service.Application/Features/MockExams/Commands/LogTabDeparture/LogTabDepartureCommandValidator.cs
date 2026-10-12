using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.LogTabDeparture;

public class LogTabDepartureCommandValidator : AbstractValidator<LogTabDepartureCommand>
{
    public LogTabDepartureCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài nộp thi thử (SubmissionId) không được để trống.");
    }
}
