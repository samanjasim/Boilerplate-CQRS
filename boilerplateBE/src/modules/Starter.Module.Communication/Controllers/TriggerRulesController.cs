using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.CreateTriggerRule;
using Starter.Module.Communication.Application.Commands.DeleteTriggerRule;
using Starter.Module.Communication.Application.Commands.ToggleTriggerRule;
using Starter.Module.Communication.Application.Commands.UpdateTriggerRule;
using Starter.Module.Communication.Application.Queries.GetTriggerRuleById;
using Starter.Module.Communication.Application.Queries.GetTriggerRules;
using Starter.Module.Communication.Constants;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Manage trigger rules that connect domain events to the message dispatch pipeline.
/// </summary>
public sealed class TriggerRulesController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get all trigger rules for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetTriggerRulesQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get a trigger rule by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetTriggerRuleByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new trigger rule.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = CommunicationPermissions.ManageTriggerRules)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTriggerRuleCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Update an existing trigger rule.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageTriggerRules)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTriggerRuleCommand command, CancellationToken ct = default)
    {
        if (ValidateRouteId(id, command.Id) is { } mismatch) return mismatch;
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a trigger rule.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.ManageTriggerRules)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteTriggerRuleCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Toggle a trigger rule between Active and Inactive.
    /// </summary>
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = CommunicationPermissions.ManageTriggerRules)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ToggleTriggerRuleCommand(id), ct);
        return HandleResult(result);
    }
}
