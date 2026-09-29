using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.TrackVideo;

/// <summary>
/// Validator kiểm định tham số đầu vào cho TrackVideoCommand
/// </summary>
public class TrackVideoCommandValidator : AbstractValidator<TrackVideoCommand>
{
    public TrackVideoCommandValidator()
    {
        RuleFor(x => x.NodeId)
            .NotEmpty().WithMessage("NodeId không được để trống.");

        RuleFor(x => x.WatchedDurationSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian xem (WatchedDurationSeconds) không được âm.");

        RuleFor(x => x.TotalDurationSeconds)
            .GreaterThan(0).WithMessage("Tổng thời lượng bài giảng (TotalDurationSeconds) phải lớn hơn 0.");

        RuleFor(x => x)
            .Must(x => x.WatchedDurationSeconds <= x.TotalDurationSeconds * 3)
            .WithMessage("Thời gian xem vượt quá giới hạn hợp lý của tổng thời lượng bài giảng.");
    }
}
