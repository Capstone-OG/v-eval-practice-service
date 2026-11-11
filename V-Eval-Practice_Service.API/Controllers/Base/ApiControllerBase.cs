using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using V_Eval_Practice_Service.Application.Common.Models;

namespace V_Eval_Practice_Service.API.Controllers.Base;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>
    /// Trích xuất định danh người dùng theo thứ tự ưu tiên:
    /// 1. Trusted Header 'X-User-Id' từ API Gateway YARP
    /// 2. JWT Bearer Claim ('sub' hoặc NameIdentifier) khi gọi trực tiếp có xác thực
    /// 3. Tham số tường minh từ Query/Body (nếu có)
    /// </summary>
    protected Guid ResolveUserId(Guid? explicitUserId = null)
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) &&
            Guid.TryParse(userIdHeader, out var gatewayUserId) &&
            gatewayUserId != Guid.Empty)
        {
            return gatewayUserId;
        }

        if (User?.Identity?.IsAuthenticated == true)
        {
            var claimVal = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(claimVal) &&
                Guid.TryParse(claimVal, out var jwtUserId) &&
                jwtUserId != Guid.Empty)
            {
                return jwtUserId;
            }
        }

        if (explicitUserId.HasValue && explicitUserId.Value != Guid.Empty)
        {
            return explicitUserId.Value;
        }

        return Guid.Empty;
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return MapError(result.Error, result.Errors);
    }

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return MapError(result.Error, result.Errors);
    }

    private IActionResult MapError(Error mainError, IReadOnlyList<Error> errors)
    {
        var response = errors.Count > 1
            ? (object)new { errors }
            : new { error = mainError };

        return mainError.Type switch
        {
            ErrorType.Validation => BadRequest(response),
            ErrorType.NotFound => NotFound(response),
            ErrorType.Conflict => Conflict(response),
            ErrorType.Unauthorized => Unauthorized(response),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }
}
