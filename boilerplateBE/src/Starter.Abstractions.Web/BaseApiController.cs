using Asp.Versioning;
using Starter.Abstractions.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Abstractions.Web;

/// <summary>
/// Base API controller with common functionality.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public abstract class BaseApiController(ISender mediator) : ControllerBase
{
    protected ISender Mediator { get; } = mediator;

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse.Ok());

        return HandleFailure(result);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.Ok(result.Value));

        return HandleFailure(result);
    }

    protected IActionResult HandleResult<T>(Result<T> result, Func<T, object> mapper)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<object>.Ok(mapper(result.Value)));

        return HandleFailure(result);
    }

    protected IActionResult HandleCreatedResult<T>(Result<T> result, string actionName, object? routeValues = null)
    {
        if (result.IsSuccess)
            return CreatedAtAction(actionName, routeValues, ApiResponse<T>.Ok(result.Value));

        return HandleFailure(result);
    }

    protected IActionResult HandlePagedResult<T>(PaginatedList<T> paged)
    {
        return Ok(PagedApiResponse<T>.Ok(paged));
    }

    protected IActionResult HandlePagedResult<T>(Result<PaginatedList<T>> result)
    {
        if (result.IsFailure)
            return HandleFailure(result);

        return Ok(PagedApiResponse<T>.Ok(result.Value));
    }

    protected IActionResult? ValidateRouteId(Guid routeId, Guid bodyId)
    {
        if (routeId == bodyId) return null;
        return BadRequest(ApiResponse.Fail("Route id does not match body id."));
    }

    private IActionResult HandleFailure(Result result)
    {
        if (result.ValidationErrors is not null)
        {
            return BadRequest(ApiResponse.Fail(result.ValidationErrors));
        }

        return result.Error.Type switch
        {
            ErrorType.NotFound => NotFound(ApiResponse.Fail(result.Error.Description)),
            ErrorType.Unauthorized => Unauthorized(ApiResponse.Fail(result.Error.Description)),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(result.Error.Description)),
            ErrorType.Conflict => Conflict(ApiResponse.Fail(result.Error.Description)),
            ErrorType.Validation => BadRequest(ApiResponse.Fail(result.Error.Description)),
            _ => BadRequest(ApiResponse.Fail(result.Error.Description))
        };
    }
}
