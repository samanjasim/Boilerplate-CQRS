using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.ResendDelivery;
using Starter.Module.Communication.Application.Queries.GetDeliveryLogById;
using Starter.Module.Communication.Application.Queries.GetDeliveryLogs;
using Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;
using Starter.Module.Communication.Constants;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// View and manage message delivery history.
/// </summary>
public sealed class DeliveryLogsController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get paginated delivery logs with optional filters.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.ViewDeliveryLog)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DeliveryStatus? status = null,
        [FromQuery] NotificationChannel? channel = null,
        [FromQuery] string? templateName = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetDeliveryLogsQuery(pageNumber, pageSize, status, channel, templateName, from, to), ct);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get a delivery log by ID with attempt details.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ViewDeliveryLog)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDeliveryLogByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Resend a failed or bounced delivery.
    /// </summary>
    [HttpPost("{id:guid}/resend")]
    [Authorize(Policy = CommunicationPermissions.Resend)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resend(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ResendDeliveryCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get delivery status counts grouped over a recent window (default 7 days).
    /// </summary>
    [HttpGet("status-counts")]
    [Authorize(Policy = CommunicationPermissions.ViewDeliveryLog)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusCounts(
        [FromQuery] int windowDays = 7,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDeliveryStatusCountsQuery(windowDays), ct);
        return HandleResult(result);
    }
}
