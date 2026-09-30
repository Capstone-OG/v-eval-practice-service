using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetTeacherSchedule;

public class GetTeacherScheduleQueryValidator : AbstractValidator<GetTeacherScheduleQuery>
{
    public GetTeacherScheduleQueryValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("TeacherId không được để trống.");
    }
}
