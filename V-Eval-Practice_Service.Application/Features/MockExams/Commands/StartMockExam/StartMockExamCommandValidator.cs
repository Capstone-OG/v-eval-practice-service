using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.StartMockExam;

public class StartMockExamCommandValidator : AbstractValidator<StartMockExamCommand>
{
    public StartMockExamCommandValidator()
    {
        RuleFor(x => x.ExamId)
            .NotEmpty().WithMessage("Mã đề thi (ExamId) không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã học sinh (StudentId) không được để trống.");
    }
}
