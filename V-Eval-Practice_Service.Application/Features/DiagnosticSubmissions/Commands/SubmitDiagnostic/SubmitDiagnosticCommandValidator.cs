using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Commands.SubmitDiagnostic;

public class SubmitDiagnosticCommandValidator : AbstractValidator<SubmitDiagnosticCommand>
{
    public SubmitDiagnosticCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.ExamId)
            .NotEmpty().WithMessage("ExamId không được để trống.");

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Danh sách câu trả lời không được null.")
            .Must(a => a.Count >= 1 && a.Count <= 30)
            .WithMessage("Danh sách câu trả lời phải có từ 1 đến 30 câu hỏi.");

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuestionId)
                .NotEmpty().WithMessage("QuestionId của câu trả lời không được để trống.");

            answer.RuleFor(a => a.TimeSpentSeconds)
                .GreaterThanOrEqualTo(0).WithMessage("Thời gian làm bài (TimeSpentSeconds) không được âm.");
        });
    }
}
