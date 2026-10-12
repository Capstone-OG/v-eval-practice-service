using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Queries.GetMockExamDetail;

public class GetMockExamDetailQueryValidator : AbstractValidator<GetMockExamDetailQuery>
{
    public GetMockExamDetailQueryValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài nộp thi thử (SubmissionId) không được để trống.");
    }
}
