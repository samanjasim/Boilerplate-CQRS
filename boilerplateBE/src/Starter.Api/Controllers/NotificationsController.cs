using MediatR;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using Starter.Application.Features.Notifications.Commands.MarkNotificationRead;
using Starter.Application.Features.Notifications.Commands.UpdateNotificationPreferences;
using Starter.Application.Features.Notifications.Queries.GetNotifications;
using Starter.Application.Features.Notifications.Queries.GetNotificationPreferences;
using Starter.Application.Features.Notifications.Queries.GetUnreadCount;

namespace Starter.Api.Controllers;

/// <summary>
/// Notification endpoints.
/// </summary>
public sealed class NotificationsController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get paginated notifications for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications([FromQuery] GetNotificationsQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get unread notification count for the current user.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await Mediator.Send(new GetUnreadCountQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var result = await Mediator.Send(new MarkNotificationReadCommand(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Mark all notifications as read.
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead()
    {
        var result = await Mediator.Send(new MarkAllNotificationsReadCommand());
        return HandleResult(result);
    }

    /// <summary>
    /// Get notification preferences for the current user.
    /// </summary>
    [HttpGet("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPreferences()
    {
        var result = await Mediator.Send(new GetNotificationPreferencesQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Update notification preferences for the current user.
    /// </summary>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
}
