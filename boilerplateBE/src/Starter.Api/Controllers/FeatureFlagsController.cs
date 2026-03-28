using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;
using Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;
using Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;
using Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;
using Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class FeatureFlagsController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = Permissions.FeatureFlags.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetFeatureFlagsQuery(pageNumber, pageSize, category, search), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{key}")]
    [Authorize(Policy = Permissions.FeatureFlags.View)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetFeatureFlagByKeyQuery(key), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.FeatureFlags.Create)]
    public async Task<IActionResult> Create(CreateFeatureFlagCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.Update)]
    public async Task<IActionResult> Update(Guid id, UpdateFeatureFlagCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteFeatureFlagCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.ManageTenantOverrides)]
    public async Task<IActionResult> SetTenantOverride(
        Guid id, Guid tenantId, [FromBody] SetTenantOverrideRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new SetTenantOverrideCommand(id, tenantId, request.Value), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.ManageTenantOverrides)]
    public async Task<IActionResult> RemoveTenantOverride(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RemoveTenantOverrideCommand(id, tenantId), ct);
        return HandleResult(result);
    }
}

public sealed record SetTenantOverrideRequest(string Value);
