using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Tenants.Commands.ActivateTenant;
using Starter.Application.Features.Tenants.Commands.DeactivateTenant;
using Starter.Application.Features.Tenants.Commands.SuspendTenant;
using Starter.Application.Features.Tenants.Commands.UpdateTenant;
using Starter.Application.Features.Tenants.Queries.GetTenants;
using Starter.Application.Features.Tenants.Queries.GetTenantById;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Tenant management endpoints.
/// </summary>
public sealed class TenantsController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get paginated list of tenants.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Tenants.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenants([FromQuery] GetTenantsQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get tenant by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Tenants.Show)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantById(Guid id)
    {
        var result = await Mediator.Send(new GetTenantByIdQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Update a tenant.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        var command = new UpdateTenantCommand(id, request.Name, request.Slug);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Activate a tenant.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateTenant(Guid id)
        => HandleResult(await Mediator.Send(new ActivateTenantCommand(id)));

    /// <summary>
    /// Suspend a tenant.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendTenant(Guid id)
        => HandleResult(await Mediator.Send(new SuspendTenantCommand(id)));

    /// <summary>
    /// Deactivate a tenant.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateTenant(Guid id)
        => HandleResult(await Mediator.Send(new DeactivateTenantCommand(id)));
}

#region Request DTOs

/// <summary>
/// Request to update a tenant.
/// </summary>
public sealed record UpdateTenantRequest(
    string Name,
    string? Slug);

#endregion
