using System;
using FluentValidation;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.UpdateRecording;

public class UpdateLiveSessionRecordingCommandValidator : AbstractValidator<UpdateLiveSessionRecordingCommand>
{
    public UpdateLiveSessionRecordingCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(x => x.RecordingUrl)
            .NotEmpty().WithMessage("RecordingUrl không được để trống.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                         (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            .WithMessage("RecordingUrl phải là một đường link URL hợp lệ (http hoặc https).");
    }
}
