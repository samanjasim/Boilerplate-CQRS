using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Users.Commands.ActivateUser;
using Starter.Application.Features.Users.Commands.DeactivateUser;
using Starter.Application.Features.Users.Commands.SuspendUser;
using Starter.Application.Features.Users.Commands.UnlockUser;
using Starter.Application.Features.Users.Commands.UpdateUser;
using Starter.Application.Features.Users.Queries.GetUsers;
using Starter.Application.Features.Users.Queries.GetUserById;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// User management endpoints.
/// </summary>
public sealed class UsersController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get paginated list of users.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get user by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Users.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await Mediator.Send(new GetUserByIdQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Update a user's profile (name, email, phone number).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Users.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(id, request.FirstName, request.LastName, request.Email, request.PhoneNumber);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Activate a user account.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Permissions.Users.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(Guid id)
        => HandleResult(await Mediator.Send(new ActivateUserCommand(id)));

    /// <summary>
    /// Suspend a user account.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = Permissions.Users.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendUser(Guid id)
        => HandleResult(await Mediator.Send(new SuspendUserCommand(id)));

    /// <summary>
    /// Deactivate a user account.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.Users.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(Guid id)
        => HandleResult(await Mediator.Send(new DeactivateUserCommand(id)));

    /// <summary>
    /// Unlock a locked user account.
    /// </summary>
    [HttpPost("{id:guid}/unlock")]
    [Authorize(Policy = Permissions.Users.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockUser(Guid id)
        => HandleResult(await Mediator.Send(new UnlockUserCommand(id)));
}

#region Request DTOs

public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber);

#endregion
