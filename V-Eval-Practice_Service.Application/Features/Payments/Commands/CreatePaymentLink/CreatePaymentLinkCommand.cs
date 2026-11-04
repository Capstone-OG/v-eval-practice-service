using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Payments.Commands.CreatePaymentLink;

public record CreatePaymentLinkCommand(
    Guid StudentId,
    Guid ParentId,
    Guid CourseId,
    string ReturnUrl,
    string CancelUrl
) : IRequest<Result<CreatePaymentLinkResponseDto>>;

