using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.RemoveRequiredNotification;
using Starter.Module.Communication.Application.Commands.SetRequiredNotification;
using Starter.Module.Communication.Application.Queries.GetRequiredNotifications;
using Starter.Module.Communication.Constants;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Manage tenant-level required notification rules.
/// </summary>
public sealed class RequiredNotificationsController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// List all required notifications for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetRequiredNotificationsQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Add a required notification rule.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] SetRequiredNotificationCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Remove a required notification rule.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RemoveRequiredNotificationCommand(id), ct);
        return HandleResult(result);
    }
}
