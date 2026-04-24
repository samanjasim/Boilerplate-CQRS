using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.FeatureFlags;
using Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.OptOutFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.RemoveOptOut;
using Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;
using Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;
using Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;
using Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;
using Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;
using Starter.Application.Features.FeatureFlags.Queries.ResolveFeatureFlag;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Shared.Constants;
using Starter.Shared.Models;

namespace Starter.Api.Controllers;

public sealed class FeatureFlagsController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = Permissions.FeatureFlags.View)]
    [ProducesResponseType(typeof(PagedApiResponse<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50,
        [FromQuery] FlagCategory? category = null, [FromQuery] string? search = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetFeatureFlagsQuery(pageNumber, pageSize, category, search, tenantId), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{key}")]
    [Authorize(Policy = Permissions.FeatureFlags.View)]
    [ProducesResponseType(typeof(ApiResponse<FeatureFlagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetFeatureFlagByKeyQuery(key), ct);
        return HandleResult(result);
    }

    // Lightweight flag resolution for the current user — returns only key + resolved value
    // (tenant override else default). Any authenticated user can call this; the full GetByKey
    // endpoint above leaks metadata (IsSystem, description, timestamps) and stays admin-only.
    [HttpGet("resolve/{key}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ResolvedFeatureFlagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(string key, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ResolveFeatureFlagQuery(key), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.FeatureFlags.Create)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateFeatureFlagCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, UpdateFeatureFlagCommand command, CancellationToken ct = default)
    {
        if (ValidateRouteId(id, command.Id) is { } mismatch) return mismatch;
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteFeatureFlagCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.ManageTenantOverrides)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTenantOverride(
        Guid id, Guid tenantId, [FromBody] SetTenantOverrideRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new SetTenantOverrideCommand(id, tenantId, request.Value), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.ManageTenantOverrides)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTenantOverride(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RemoveTenantOverrideCommand(id, tenantId), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/opt-out")]
    [Authorize(Policy = Permissions.FeatureFlags.OptOut)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OptOut(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new OptOutFeatureFlagCommand(id), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}/opt-out")]
    [Authorize(Policy = Permissions.FeatureFlags.OptOut)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveOptOut(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RemoveOptOutCommand(id), ct);
        return HandleResult(result);
    }
}

public sealed record SetTenantOverrideRequest(string Value);
