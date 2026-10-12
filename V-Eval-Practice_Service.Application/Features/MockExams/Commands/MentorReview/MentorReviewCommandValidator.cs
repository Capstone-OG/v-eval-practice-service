using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.MentorReview;

public class MentorReviewCommandValidator : AbstractValidator<MentorReviewCommand>
{
    public MentorReviewCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài nộp thi thử (SubmissionId) không được để trống.");

        RuleFor(x => x.Verdict)
            .NotEmpty().WithMessage("Kết luận thẩm định (Verdict) không được để trống.")
            .Must(v => v == "APPROVED" || v == "DISQUALIFIED")
            .WithMessage("Kết luận thẩm định (Verdict) chỉ chấp nhận giá trị 'APPROVED' (Hợp lệ) hoặc 'DISQUALIFIED' (Hủy kết quả).");

        When(x => x.Verdict == "DISQUALIFIED", () =>
        {
            RuleFor(x => x.Notes)
                .NotEmpty().WithMessage("Khi hủy kết quả bài thi (DISQUALIFIED), Mentor bắt buộc phải nhập lý do/ghi chú thẩm định.");
        });
    }
}
