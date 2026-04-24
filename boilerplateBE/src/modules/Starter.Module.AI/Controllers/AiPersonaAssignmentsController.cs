using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Commands.Personas.AssignPersona;
using Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;
using Starter.Module.AI.Application.Commands.Personas.UnassignPersona;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/personas/{personaId:guid}/assignments")]
public sealed class AiPersonaAssignmentsController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewPersonas)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserPersonaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid personaId, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetPersonaAssignmentsQuery(personaId), ct));

    [HttpPost]
    [Authorize(Policy = AiPermissions.AssignPersona)]
    public async Task<IActionResult> Assign(
        Guid personaId,
        [FromBody] AssignBody body,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(
            new AssignPersonaCommand(personaId, body.UserId, body.MakeDefault), ct));

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = AiPermissions.AssignPersona)]
    public async Task<IActionResult> Unassign(
        Guid personaId,
        Guid userId,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new UnassignPersonaCommand(personaId, userId), ct));

    [HttpPut("{userId:guid}/default")]
    [Authorize(Policy = AiPermissions.AssignPersona)]
    public async Task<IActionResult> SetDefault(
        Guid personaId,
        Guid userId,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new SetUserDefaultPersonaCommand(personaId, userId), ct));

    public sealed record AssignBody(Guid UserId, bool MakeDefault);
}
