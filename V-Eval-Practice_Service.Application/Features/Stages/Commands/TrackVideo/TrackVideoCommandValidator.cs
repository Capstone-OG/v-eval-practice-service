using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.TrackVideo;

public class TrackVideoCommandValidator : AbstractValidator<TrackVideoCommand>
{
    public TrackVideoCommandValidator()
    {
        RuleFor(x => x.StageProgressId)
            .NotEmpty().WithMessage("StageProgressId không được để trống.");

        RuleFor(x => x.Request.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.Request.WatchedSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Thời lượng đã xem phải >= 0 giây.");

        RuleFor(x => x.Request.TotalSeconds)
            .GreaterThan(0).WithMessage("Tổng thời lượng video phải > 0 giây.");
    }
}
