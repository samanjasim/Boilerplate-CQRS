using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/eval")]
[ApiController]
public sealed class AiEvalController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpPost("faithfulness")]
    [Authorize(Policy = AiPermissions.RunEval)]
    public async Task<IActionResult> RunFaithfulness(
        [FromForm] string? dataset_name,
        [FromForm] Guid assistant_id,
        [FromForm] string? judge_model,
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
            new RunFaithfulnessEvalCommand(fixtureJson, dataset_name, assistant_id, judge_model), ct);
        return HandleResult(result);
    }
}
