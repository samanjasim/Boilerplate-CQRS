using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Roles.Commands.CreateRole;
using Starter.Application.Features.Roles.Commands.UpdateRole;
using Starter.Application.Features.Roles.Commands.DeleteRole;
using Starter.Application.Features.Roles.Commands.UpdateRolePermissions;
using Starter.Application.Features.Roles.Commands.AssignUserRole;
using Starter.Application.Features.Roles.Commands.RemoveUserRole;
using Starter.Application.Features.Roles.Queries.GetRoles;
using Starter.Application.Features.Roles.Queries.GetRoleById;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Role management endpoints.
/// </summary>
public sealed class RolesController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get paginated list of roles.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoles([FromQuery] GetRolesQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get a role by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole(Guid id)
    {
        var query = new GetRoleByIdQuery(id);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new role.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Roles.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var command = new CreateRoleCommand(request.Name, request.Description);
        var result = await Mediator.Send(command);
        return HandleCreatedResult(result, nameof(GetRole), new { id = result.IsSuccess ? result.Value : (Guid?)null });
    }

    /// <summary>
    /// Update an existing role.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var command = new UpdateRoleCommand(id, request.Name, request.Description);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a role.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Roles.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var command = new DeleteRoleCommand(id);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Update role permissions (replaces all permissions).
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    [Authorize(Policy = Permissions.Roles.ManagePermissions)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRolePermissions(Guid id, [FromBody] UpdateRolePermissionsRequest request)
    {
        var command = new UpdateRolePermissionsCommand(id, request.PermissionIds);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Assign a role to a user.
    /// </summary>
    [HttpPost("{id:guid}/users/{userId:guid}")]
    [Authorize(Policy = Permissions.Users.ManageRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignUserToRole(Guid id, Guid userId)
    {
        var command = new AssignUserRoleCommand(userId, id);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Remove a role from a user.
    /// </summary>
    [HttpDelete("{id:guid}/users/{userId:guid}")]
    [Authorize(Policy = Permissions.Users.ManageRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserFromRole(Guid id, Guid userId)
    {
        var command = new RemoveUserRoleCommand(userId, id);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}

#region Request DTOs

/// <summary>
/// Request to create a new role.
/// </summary>
public sealed record CreateRoleRequest(
    string Name,
    string? Description);

/// <summary>
/// Request to update a role.
/// </summary>
public sealed record UpdateRoleRequest(
    string Name,
    string? Description);

/// <summary>
/// Request to update role permissions.
/// </summary>
public sealed record UpdateRolePermissionsRequest(List<Guid> PermissionIds);

#endregion
