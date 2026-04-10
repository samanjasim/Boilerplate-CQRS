using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Webhooks.Domain.Enums;
using Starter.Module.Webhooks.Application.Commands.CreateWebhookEndpoint;
using Starter.Module.Webhooks.Application.Commands.DeleteWebhookEndpoint;
using Starter.Module.Webhooks.Application.Commands.RegenerateWebhookSecret;
using Starter.Module.Webhooks.Application.Commands.TestWebhookEndpoint;
using Starter.Module.Webhooks.Application.Commands.UpdateWebhookEndpoint;
using Starter.Module.Webhooks.Application.Queries.GetAllWebhookEndpoints;
using Starter.Module.Webhooks.Application.Queries.GetWebhookAdminStats;
using Starter.Module.Webhooks.Application.Queries.GetWebhookDeliveries;
using Starter.Module.Webhooks.Application.Queries.GetWebhookDeliveriesAdmin;
using Starter.Module.Webhooks.Application.Queries.GetWebhookEndpointById;
using Starter.Module.Webhooks.Application.Queries.GetWebhookEndpoints;
using Starter.Module.Webhooks.Application.Queries.GetWebhookEventTypes;
using Starter.Module.Webhooks.Constants;

namespace Starter.Module.Webhooks.Controllers;

/// <summary>
/// Webhook endpoint management and delivery monitoring.
/// </summary>
public sealed class WebhooksController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get all webhook endpoints across all tenants with delivery stats (Platform Admin).
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Policy = WebhookPermissions.ViewPlatform)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllEndpointsAdmin(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAllWebhookEndpointsQuery(pageNumber, pageSize, searchTerm), ct);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get aggregate webhook statistics across all tenants (Platform Admin).
    /// </summary>
    [HttpGet("admin/stats")]
    [Authorize(Policy = WebhookPermissions.ViewPlatform)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminStats(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookAdminStatsQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get delivery history for any endpoint across tenants (Platform Admin).
    /// </summary>
    [HttpGet("admin/{endpointId:guid}/deliveries")]
    [Authorize(Policy = WebhookPermissions.ViewPlatform)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeliveriesAdmin(
        Guid endpointId,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] WebhookDeliveryStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookDeliveriesAdminQuery(endpointId, pageNumber, pageSize, status), ct);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get all webhook endpoints for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = WebhookPermissions.View)]
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
    [Authorize(Policy = WebhookPermissions.View)]
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
    [Authorize(Policy = WebhookPermissions.Create)]
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
    [Authorize(Policy = WebhookPermissions.Update)]
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
    [Authorize(Policy = WebhookPermissions.Delete)]
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
    [Authorize(Policy = WebhookPermissions.View)]
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
    [Authorize(Policy = WebhookPermissions.View)]
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
    [Authorize(Policy = WebhookPermissions.Update)]
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
    [Authorize(Policy = WebhookPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEventTypesQuery(), ct);
        return HandleResult(result);
    }
}
