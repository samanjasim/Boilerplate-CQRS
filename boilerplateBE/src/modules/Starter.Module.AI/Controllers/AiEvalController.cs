using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/eval")]
public sealed class AiEvalController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpPost("faithfulness")]
    [Authorize(Policy = AiPermissions.RunEval)]
    [RequestSizeLimit(5_242_880)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    [ProducesResponseType(typeof(ApiResponse<EvalReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> RunFaithfulness(
        [FromForm] string? datasetName,
        [FromForm] Guid assistantId,
        [FromForm] string? judgeModel,
        IFormFile? fixture,
        CancellationToken ct)
    {
        string? fixtureJson = null;
        if (fixture is not null)
        {
            using var reader = new StreamReader(fixture.OpenReadStream());
            fixtureJson = await reader.ReadToEndAsync(ct);
        }

        var result = await Mediator.Send(
            new RunFaithfulnessEvalCommand(fixtureJson, datasetName, assistantId, judgeModel), ct);
        return HandleResult(result);
    }
}
