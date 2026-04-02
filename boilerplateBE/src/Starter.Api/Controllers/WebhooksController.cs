using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Webhooks.Commands.CreateWebhookEndpoint;
using Starter.Application.Features.Webhooks.Commands.DeleteWebhookEndpoint;
using Starter.Application.Features.Webhooks.Commands.TestWebhookEndpoint;
using Starter.Application.Features.Webhooks.Commands.UpdateWebhookEndpoint;
using Starter.Application.Features.Webhooks.Queries.GetWebhookDeliveries;
using Starter.Application.Features.Webhooks.Queries.GetWebhookEndpointById;
using Starter.Application.Features.Webhooks.Queries.GetWebhookEndpoints;
using Starter.Application.Features.Webhooks.Queries.GetWebhookEventTypes;
using Starter.Domain.Webhooks.Enums;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class WebhooksController(ISender mediator) : BaseApiController(mediator)
{
    // GET /api/v1/webhooks
    [HttpGet]
    [Authorize(Policy = Permissions.Webhooks.View)]
    public async Task<IActionResult> GetEndpoints(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEndpointsQuery(), ct);
        return HandleResult(result);
    }

    // GET /api/v1/webhooks/{id}
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Webhooks.View)]
    public async Task<IActionResult> GetEndpointById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEndpointByIdQuery(id), ct);
        return HandleResult(result);
    }

    // POST /api/v1/webhooks
    [HttpPost]
    [Authorize(Policy = Permissions.Webhooks.Create)]
    public async Task<IActionResult> CreateEndpoint([FromBody] CreateWebhookEndpointCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    // PUT /api/v1/webhooks/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Webhooks.Update)]
    public async Task<IActionResult> UpdateEndpoint(Guid id, [FromBody] UpdateWebhookEndpointCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    // DELETE /api/v1/webhooks/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Webhooks.Delete)]
    public async Task<IActionResult> DeleteEndpoint(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteWebhookEndpointCommand(id), ct);
        return HandleResult(result);
    }

    // GET /api/v1/webhooks/{id}/deliveries
    [HttpGet("{id:guid}/deliveries")]
    [Authorize(Policy = Permissions.Webhooks.View)]
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

    // POST /api/v1/webhooks/{id}/test
    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = Permissions.Webhooks.Create)]
    public async Task<IActionResult> TestEndpoint(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new TestWebhookEndpointCommand(id), ct);
        return HandleResult(result);
    }

    // GET /api/v1/webhooks/events
    [HttpGet("events")]
    [Authorize(Policy = Permissions.Webhooks.View)]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWebhookEventTypesQuery(), ct);
        return HandleResult(result);
    }
}
