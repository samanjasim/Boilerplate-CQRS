using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.UpdateNotificationPreferences;
using Starter.Module.Communication.Application.Queries.GetNotificationPreferences;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Manage per-user notification preferences.
/// </summary>
public sealed class NotificationPreferencesController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get the current user's notification preferences.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetNotificationPreferencesQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Update the current user's notification preferences.
    /// </summary>
    [HttpPut]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateNotificationPreferencesCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
}
