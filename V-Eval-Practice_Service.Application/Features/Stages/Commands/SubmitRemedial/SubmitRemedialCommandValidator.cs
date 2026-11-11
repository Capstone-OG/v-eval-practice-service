using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitRemedial;

public class SubmitRemedialCommandValidator : AbstractValidator<SubmitRemedialCommand>
{
    public SubmitRemedialCommandValidator()
    {
        RuleFor(x => x.StageProgressId)
            .NotEmpty().WithMessage("Mã tiến trình chặng học (StageProgressId) không được để trống.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Dữ liệu nộp bài cứu trợ không được null.");

        RuleFor(x => x.Request.Answers)
            .NotEmpty().WithMessage("Danh sách câu trả lời cứu trợ không được để trống.");
    }
}
