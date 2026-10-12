using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.SubmitMockExam;

public class SubmitMockExamCommandValidator : AbstractValidator<SubmitMockExamCommand>
{
    public SubmitMockExamCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài nộp thi thử (SubmissionId) không được để trống.");

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Danh sách câu trả lời không được để trống.")
            .Must(a => a != null && a.Count > 0).WithMessage("Bài thi phải có ít nhất 1 câu trả lời.");
    }
}
