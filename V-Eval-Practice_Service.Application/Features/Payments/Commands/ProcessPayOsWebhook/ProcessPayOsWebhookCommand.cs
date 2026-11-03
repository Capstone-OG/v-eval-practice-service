using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;

namespace V_Eval_Practice_Service.Application.Features.Payments.Commands.ProcessPayOsWebhook;

public record ProcessPayOsWebhookCommand(
    string RawPayload,
    string? IpAddress = null
) : IRequest<Result<bool>>;

