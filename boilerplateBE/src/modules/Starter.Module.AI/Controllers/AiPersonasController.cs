using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Commands.Personas.CreatePersona;
using Starter.Module.AI.Application.Commands.Personas.DeletePersona;
using Starter.Module.AI.Application.Commands.Personas.UpdatePersona;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Personas.GetPersonaById;
using Starter.Module.AI.Application.Queries.Personas.GetPersonas;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/personas")]
public sealed class AiPersonasController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewPersonas)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiPersonaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeSystem = true,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetPersonasQuery(includeSystem, includeInactive), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ViewPersonas)]
    [ProducesResponseType(typeof(ApiResponse<AiPersonaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetPersonaByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManagePersonas)]
    [ProducesResponseType(typeof(ApiResponse<AiPersonaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePersonaCommand command,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManagePersonas)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePersonaBody body,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new UpdatePersonaCommand(
            id, body.DisplayName, body.Description, body.SafetyPreset,
            body.PermittedAgentSlugs, body.IsActive), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManagePersonas)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new DeletePersonaCommand(id), ct));

    public sealed record UpdatePersonaBody(
        string DisplayName,
        string? Description,
        SafetyPreset SafetyPreset,
        IReadOnlyList<string>? PermittedAgentSlugs,
        bool IsActive);
}
