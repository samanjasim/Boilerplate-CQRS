using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.ToggleTool;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetTools;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/tools")]
public sealed class AiToolsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageTools)]
    [ProducesResponseType(typeof(PagedApiResponse<AiToolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetToolsQuery(pageNumber, pageSize, category, isEnabled, searchTerm), ct);
        return HandlePagedResult(result);
    }

    [HttpPut("{name}/toggle")]
    [Authorize(Policy = AiPermissions.ManageTools)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(
        string name,
        [FromBody] ToggleToolBody body,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ToggleToolCommand(name, body.IsEnabled), ct);
        return HandleResult(result);
    }

    public sealed record ToggleToolBody(bool IsEnabled);
}
