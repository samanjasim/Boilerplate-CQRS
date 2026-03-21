using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Permissions.Queries.GetAllPermissions;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Permissions read-only endpoints.
/// </summary>
public sealed class PermissionsController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get all available permissions grouped by module.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllPermissions()
    {
        var query = new GetAllPermissionsQuery();
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }
}
