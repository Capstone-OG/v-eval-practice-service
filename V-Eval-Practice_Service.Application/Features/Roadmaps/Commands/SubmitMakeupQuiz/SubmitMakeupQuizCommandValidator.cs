using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.SubmitMakeupQuiz;

public class SubmitMakeupQuizCommandValidator : AbstractValidator<SubmitMakeupQuizCommand>
{
    public SubmitMakeupQuizCommandValidator()
    {
        RuleFor(x => x.NodeId)
            .NotEmpty().WithMessage("NodeId không được để trống.");

        RuleFor(x => x.TotalTimeSpentSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian làm bài không hợp lệ.");

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Danh sách câu trả lời không được để trống.");
    }
}
