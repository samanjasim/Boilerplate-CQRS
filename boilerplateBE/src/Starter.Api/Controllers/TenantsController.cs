using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Tenants.Commands.ActivateTenant;
using Starter.Application.Features.Tenants.Commands.DeactivateTenant;
using Starter.Application.Features.Tenants.Commands.SuspendTenant;
using Starter.Application.Features.Tenants.Commands.UpdateTenant;
using Starter.Application.Features.Tenants.Commands.UpdateTenantBranding;
using Starter.Application.Features.Tenants.Commands.UpdateTenantBusinessInfo;
using Starter.Application.Features.Tenants.Commands.UpdateTenantCustomText;
using Starter.Application.Features.Tenants.Queries.GetTenants;
using Starter.Application.Features.Tenants.Queries.GetTenantById;
using Starter.Application.Features.Tenants.Queries.GetTenantBranding;
using Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;
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

    /// <summary>
    /// Update tenant branding.
    /// </summary>
    [HttpPut("{id:guid}/branding")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenantBranding(
        Guid id,
        [FromBody] UpdateTenantBrandingRequest request)
    {
        var command = new UpdateTenantBrandingCommand(
            id,
            request.LogoFileId,
            request.FaviconFileId,
            request.RemoveLogo,
            request.RemoveFavicon,
            request.PrimaryColor,
            request.SecondaryColor,
            request.Description);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Update tenant business info.
    /// </summary>
    [HttpPut("{id:guid}/business-info")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenantBusinessInfo(Guid id, [FromBody] UpdateTenantBusinessInfoRequest request)
    {
        var command = new UpdateTenantBusinessInfoCommand(
            id,
            request.Address,
            request.Phone,
            request.Website,
            request.TaxId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Update tenant custom text.
    /// </summary>
    [HttpPut("{id:guid}/custom-text")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenantCustomText(Guid id, [FromBody] UpdateTenantCustomTextRequest request)
    {
        var command = new UpdateTenantCustomTextCommand(
            id,
            request.LoginPageTitle,
            request.LoginPageSubtitle,
            request.EmailFooterText);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Set the default registration role for a tenant.
    /// </summary>
    [HttpPut("{id:guid}/default-role")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultRole(Guid id, [FromBody] SetDefaultRoleRequest request)
    {
        var command = new SetTenantDefaultRoleCommand(id, request.RoleId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Get tenant branding (public — needed for login page customization).
    /// </summary>
    [HttpGet("branding")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantBranding([FromQuery] string? slug, [FromQuery] Guid? tenantId)
    {
        var result = await Mediator.Send(new GetTenantBrandingQuery(slug, tenantId));
        return HandleResult(result);
    }
}

#region Request DTOs

/// <summary>
/// Request to update a tenant.
/// </summary>
public sealed record UpdateTenantRequest(
    string Name,
    string? Slug);

/// <summary>
/// Request to update tenant branding.
/// </summary>
public sealed record UpdateTenantBrandingRequest(
    Guid? LogoFileId,
    Guid? FaviconFileId,
    bool RemoveLogo,
    bool RemoveFavicon,
    string? PrimaryColor,
    string? SecondaryColor,
    string? Description);

/// <summary>
/// Request to update tenant business info.
/// </summary>
public sealed record UpdateTenantBusinessInfoRequest(
    string? Address,
    string? Phone,
    string? Website,
    string? TaxId);

/// <summary>
/// Request to update tenant custom text.
/// </summary>
public sealed record UpdateTenantCustomTextRequest(
    string? LoginPageTitle,
    string? LoginPageSubtitle,
    string? EmailFooterText);

/// <summary>
/// Request to set a tenant's default registration role.
/// </summary>
public sealed record SetDefaultRoleRequest(Guid? RoleId);

#endregion
