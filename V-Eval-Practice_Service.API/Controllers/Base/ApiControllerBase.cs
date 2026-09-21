using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using V_Eval_Practice_Service.Application.Common.Models;

namespace V_Eval_Practice_Service.API.Controllers.Base;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

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
