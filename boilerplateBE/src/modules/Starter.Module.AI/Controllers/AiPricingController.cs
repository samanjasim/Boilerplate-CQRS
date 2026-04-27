using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.DeactivateModelPricing;
using Starter.Module.AI.Application.Commands.UpsertModelPricing;
using Starter.Module.AI.Application.Queries.GetModelPricing;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/pricing")]
public sealed class AiPricingController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManagePricing)]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetModelPricingQuery(activeOnly), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManagePricing)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertModelPricingCommand command,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManagePricing)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeactivateModelPricingCommand(id), ct);
        return HandleResult(result);
    }
}
