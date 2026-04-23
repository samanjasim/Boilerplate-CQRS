using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Commands.CreateTemplateOverride;
using Starter.Module.Communication.Application.Commands.DeleteTemplateOverride;
using Starter.Module.Communication.Application.Commands.PreviewTemplate;
using Starter.Module.Communication.Application.Commands.UpdateTemplateOverride;
using Starter.Module.Communication.Application.Queries.GetMessageTemplateById;
using Starter.Module.Communication.Application.Queries.GetMessageTemplates;
using Starter.Module.Communication.Application.Queries.GetTemplateCategories;
using Starter.Module.Communication.Constants;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Message template management — view system templates and manage tenant overrides.
/// </summary>
public sealed class MessageTemplatesController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get all message templates, optionally filtered by category.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? category = null, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetMessageTemplatesQuery(category), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get a message template by ID with tenant override if exists.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetMessageTemplateByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get distinct template categories.
    /// </summary>
    [HttpGet("categories")]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetTemplateCategoriesQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a tenant-specific override for a system template.
    /// </summary>
    [HttpPost("{id:guid}/override")]
    [Authorize(Policy = CommunicationPermissions.ManageTemplates)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOverride(Guid id, [FromBody] CreateTemplateOverrideRequest body, CancellationToken ct = default)
    {
        var command = new CreateTemplateOverrideCommand(id, body.SubjectTemplate, body.BodyTemplate);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Update a tenant-specific template override.
    /// </summary>
    [HttpPut("{id:guid}/override")]
    [Authorize(Policy = CommunicationPermissions.ManageTemplates)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOverride(Guid id, [FromBody] UpdateTemplateOverrideRequest body, CancellationToken ct = default)
    {
        var command = new UpdateTemplateOverrideCommand(id, body.SubjectTemplate, body.BodyTemplate);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a tenant-specific template override (reverts to system default).
    /// </summary>
    [HttpDelete("{id:guid}/override")]
    [Authorize(Policy = CommunicationPermissions.ManageTemplates)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteTemplateOverrideCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Preview a rendered template with sample or provided variables.
    /// </summary>
    [HttpPost("{id:guid}/preview")]
    [Authorize(Policy = CommunicationPermissions.ManageTemplates)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(Guid id, [FromBody] PreviewTemplateRequest? body = null, CancellationToken ct = default)
    {
        var command = new PreviewTemplateCommand(id, body?.Variables);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
}

// Request DTOs for endpoints that use route params + body
public sealed record CreateTemplateOverrideRequest(string? SubjectTemplate, string BodyTemplate);
public sealed record UpdateTemplateOverrideRequest(string? SubjectTemplate, string BodyTemplate);
public sealed record PreviewTemplateRequest(Dictionary<string, object>? Variables);
