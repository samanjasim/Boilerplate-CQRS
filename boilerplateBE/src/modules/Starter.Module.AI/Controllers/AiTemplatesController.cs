using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetTemplates;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/templates")]
public sealed class AiTemplatesController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiAgentTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetTemplatesQuery(), ct);
        return HandleResult(result);
    }

    [HttpPost("{slug}/install")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Install(
        string slug,
        [FromBody] InstallTemplateBody? body,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new InstallTemplateCommand(slug, body?.TargetTenantId), ct);
        return HandleResult(result);
    }

    public sealed record InstallTemplateBody(Guid? TargetTenantId);
}
