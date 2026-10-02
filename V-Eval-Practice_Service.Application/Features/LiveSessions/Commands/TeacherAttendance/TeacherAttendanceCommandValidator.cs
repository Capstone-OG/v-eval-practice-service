using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.TeacherAttendance;

public class TeacherAttendanceCommandValidator : AbstractValidator<TeacherAttendanceCommand>
{
    public TeacherAttendanceCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách học sinh điểm danh không được để trống.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.StudentId)
                .NotEmpty().WithMessage("StudentId không được để trống.");

            item.RuleFor(i => i.AttendanceStatus)
                .NotEmpty().WithMessage("AttendanceStatus không được để trống.")
                .Must(s => string.Equals(s, "ATTENDED", System.StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(s, "ABSENT", System.StringComparison.OrdinalIgnoreCase))
                .WithMessage("AttendanceStatus phải là 'ATTENDED' hoặc 'ABSENT'.");
        });
    }
}
