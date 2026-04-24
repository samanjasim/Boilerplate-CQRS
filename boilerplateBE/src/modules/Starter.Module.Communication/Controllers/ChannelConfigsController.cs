using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.CreateChannelConfig;
using Starter.Module.Communication.Application.Commands.DeleteChannelConfig;
using Starter.Module.Communication.Application.Commands.SetDefaultChannelConfig;
using Starter.Module.Communication.Application.Commands.TestChannelConfig;
using Starter.Module.Communication.Application.Commands.UpdateChannelConfig;
using Starter.Module.Communication.Application.Queries.GetAvailableProviders;
using Starter.Module.Communication.Application.Queries.GetChannelConfigById;
using Starter.Module.Communication.Application.Queries.GetChannelConfigs;
using Starter.Module.Communication.Constants;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Manage notification channel provider configurations.
/// </summary>
public sealed class ChannelConfigsController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get all channel configurations for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetChannelConfigsQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get a channel configuration by ID with masked credentials.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetChannelConfigByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new channel configuration.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateChannelConfigCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Update an existing channel configuration.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelConfigCommand command, CancellationToken ct = default)
    {
        if (ValidateRouteId(id, command.Id) is { } mismatch) return mismatch;
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a channel configuration.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteChannelConfigCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Test a channel configuration's connection.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new TestChannelConfigCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Set a channel configuration as the default for its channel type.
    /// </summary>
    [HttpPost("{id:guid}/set-default")]
    [Authorize(Policy = CommunicationPermissions.ManageChannels)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new SetDefaultChannelConfigCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get available channel providers with their required credential fields.
    /// </summary>
    [HttpGet("providers")]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviders(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAvailableProvidersQuery(), ct);
        return HandleResult(result);
    }
}
