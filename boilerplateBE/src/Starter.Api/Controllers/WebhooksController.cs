using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Webhooks.Commands.CreateWebhookEndpoint;
using Starter.Application.Features.Webhooks.Commands.DeleteWebhookEndpoint;
using Starter.Application.Features.Webhooks.Commands.RegenerateWebhookSecret;
using Starter.Application.Features.Webhooks.Commands.TestWebhookEndpoint;
using Starter.Application.Features.Webhooks.Commands.UpdateWebhookEndpoint;
using Starter.Application.Features.Webhooks.Queries.GetWebhookDeliveries;
using Starter.Application.Features.Webhooks.Queries.GetWebhookEndpointById;
using Starter.Application.Features.Webhooks.Queries.GetWebhookEndpoints;
using Starter.Application.Features.Webhooks.Queries.GetWebhookEventTypes;
using Starter.Domain.Webhooks.Enums;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Webhook endpoint management and delivery monitoring.
/// </summary>
public sealed class WebhooksController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get all webhook endpoints for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Webhooks.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEndpoints(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEndpointsQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get a webhook endpoint by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Webhooks.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEndpointById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEndpointByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new webhook endpoint.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Webhooks.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEndpoint([FromBody] CreateWebhookEndpointCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Update an existing webhook endpoint.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Webhooks.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEndpoint(Guid id, [FromBody] UpdateWebhookEndpointCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a webhook endpoint.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Webhooks.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEndpoint(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteWebhookEndpointCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get paginated delivery history for a webhook endpoint.
    /// </summary>
    [HttpGet("{id:guid}/deliveries")]
    [Authorize(Policy = Permissions.Webhooks.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeliveries(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] WebhookDeliveryStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookDeliveriesQuery(id, pageNumber, pageSize, status), ct);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Send a test event to a webhook endpoint.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = Permissions.Webhooks.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestEndpoint(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new TestWebhookEndpointCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Regenerate the signing secret for a webhook endpoint.
    /// </summary>
    [HttpPost("{id:guid}/regenerate-secret")]
    [Authorize(Policy = Permissions.Webhooks.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateSecret(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RegenerateWebhookSecretCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get the list of available webhook event types.
    /// </summary>
    [HttpGet("events")]
    [Authorize(Policy = Permissions.Webhooks.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEventTypesQuery(), ct);
        return HandleResult(result);
    }
}
