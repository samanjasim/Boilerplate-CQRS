using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.ApiKeys.Commands.CreateApiKey;
using Starter.Application.Features.ApiKeys.Commands.EmergencyRevokeApiKey;
using Starter.Application.Features.ApiKeys.Commands.RevokeApiKey;
using Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;
using Starter.Application.Features.ApiKeys.Queries.GetApiKeyById;
using Starter.Application.Features.ApiKeys.Queries.GetApiKeys;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// API key management endpoints.
/// </summary>
public sealed class ApiKeysController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Create a new API key (tenant-scoped or platform).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"{Permissions.ApiKeys.Create}|{Permissions.ApiKeys.CreatePlatform}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return HandleCreatedResult(result, nameof(GetById), null);
    }

    /// <summary>
    /// Get paginated list of API keys. Platform admins use ?keyType=platform|tenant|all.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = $"{Permissions.ApiKeys.View}|{Permissions.ApiKeys.ViewPlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] GetApiKeysQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get API key details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"{Permissions.ApiKeys.View}|{Permissions.ApiKeys.ViewPlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetApiKeyByIdQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Update API key name or scopes.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = $"{Permissions.ApiKeys.Update}|{Permissions.ApiKeys.UpdatePlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApiKeyRequest request, CancellationToken ct)
    {
        var command = new UpdateApiKeyCommand(id, request.Name, request.Scopes);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Revoke an API key. Tenant users can revoke their own keys. Platform admins can revoke platform keys.
    /// For tenant keys, platform admins must use the emergency-revoke endpoint.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"{Permissions.ApiKeys.Delete}|{Permissions.ApiKeys.DeletePlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new RevokeApiKeyCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Emergency revoke any API key. Platform admins only.
    /// </summary>
    [HttpDelete("{id:guid}/emergency-revoke")]
    [Authorize(Policy = Permissions.ApiKeys.EmergencyRevoke)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EmergencyRevoke(Guid id, [FromBody] EmergencyRevokeRequest? request, CancellationToken ct)
    {
        var result = await Mediator.Send(new EmergencyRevokeApiKeyCommand(id, request?.Reason), ct);
        return HandleResult(result);
    }
}

/// <summary>
/// Request body for updating an API key.
/// </summary>
public sealed record UpdateApiKeyRequest(string? Name, List<string>? Scopes);

/// <summary>
/// Request body for emergency revoking an API key.
/// </summary>
public sealed record EmergencyRevokeRequest(string? Reason);
