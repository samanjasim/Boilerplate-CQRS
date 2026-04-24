using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.CreateIntegrationConfig;
using Starter.Module.Communication.Application.Commands.DeleteIntegrationConfig;
using Starter.Module.Communication.Application.Commands.TestIntegrationConfig;
using Starter.Module.Communication.Application.Commands.UpdateIntegrationConfig;
using Starter.Module.Communication.Application.Queries.GetIntegrationConfigById;
using Starter.Module.Communication.Application.Queries.GetIntegrationConfigs;
using Starter.Module.Communication.Constants;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Manage team integration configurations (Slack, Telegram, Discord, Microsoft Teams).
/// </summary>
public sealed class IntegrationConfigsController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get all integration configurations for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetIntegrationConfigsQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get an integration configuration by ID with masked credentials.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetIntegrationConfigByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new integration configuration.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = CommunicationPermissions.ManageIntegrations)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationConfigCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Update an existing integration configuration.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageIntegrations)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntegrationConfigCommand command, CancellationToken ct = default)
    {
        if (ValidateRouteId(id, command.Id) is { } mismatch) return mismatch;
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete an integration configuration.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageIntegrations)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteIntegrationConfigCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Test an integration configuration's connection.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = CommunicationPermissions.ManageIntegrations)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new TestIntegrationConfigCommand(id), ct);
        return HandleResult(result);
    }
}
