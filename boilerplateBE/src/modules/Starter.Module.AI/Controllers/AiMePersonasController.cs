using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Personas.GetMePersonas;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/me/personas")]
public sealed class AiMePersonasController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.Chat)]
    [ProducesResponseType(typeof(ApiResponse<MePersonasDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetMePersonasQuery(), ct));
}
