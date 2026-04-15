using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.CreateAssistant;
using Starter.Module.AI.Application.Commands.DeleteAssistant;
using Starter.Module.AI.Application.Commands.UpdateAssistant;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetAssistantById;
using Starter.Module.AI.Application.Queries.GetAssistants;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/assistants")]
public sealed class AiAssistantsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(PagedApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetAssistantsQuery(pageNumber, pageSize, searchTerm, isActive), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAssistantByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssistantCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAssistantCommand command,
        CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteAssistantCommand(id), ct);
        return HandleResult(result);
    }
}
