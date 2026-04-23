using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Queries.SearchKnowledgeBase;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/search")]
public sealed class AiSearchController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpPost]
    [Authorize(Policy = AiPermissions.SearchKnowledgeBase)]
    [ProducesResponseType(typeof(ApiResponse<SearchKnowledgeBaseResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromBody] SearchKnowledgeBaseQuery query,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}
