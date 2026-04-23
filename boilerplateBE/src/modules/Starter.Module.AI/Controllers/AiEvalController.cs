using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/eval")]
public sealed class AiEvalController(
    ISender mediator,
    IRagEvalHarness harness,
    AiDbContext db,
    IOptions<AiRagEvalSettings> evalSettings)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

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

    /// <summary>
    /// Streams a faithfulness eval run as Server-Sent Events so operators can watch
    /// per-question progress on large datasets without waiting for the whole report.
    /// Emits a <c>run_started</c> event, one <c>question_completed</c> event per
    /// question, then a terminal <c>run_completed</c> event with the full report.
    /// </summary>
    [HttpPost("faithfulness/stream")]
    [Authorize(Policy = AiPermissions.RunEval)]
    [RequestSizeLimit(5_242_880)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task StreamFaithfulness(
        [FromForm] string? datasetName,
        [FromForm] Guid assistantId,
        [FromForm] string? judgeModel,
        IFormFile? fixture,
        CancellationToken ct)
    {
        // Resolve assistant + dataset up front so we can still return a proper
        // 4xx status before SSE headers are flushed.
        var assistant = await db.AiAssistants.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assistantId, ct);
        if (assistant is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(ApiResponse.Fail(EvalErrors.AssistantNotFound.Description), ct);
            return;
        }

        string? fixtureJson = null;
        if (fixture is not null)
        {
            using var reader = new StreamReader(fixture.OpenReadStream());
            fixtureJson = await reader.ReadToEndAsync(ct);
        }

        Starter.Shared.Results.Result<EvalDataset> datasetResult;
        if (!string.IsNullOrWhiteSpace(fixtureJson))
        {
            datasetResult = EvalFixtureLoader.LoadFromString(fixtureJson);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(datasetName))
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                await Response.WriteAsJsonAsync(ApiResponse.Fail(EvalErrors.FixtureNotFound.Description), ct);
                return;
            }
            var path = Path.Combine(
                evalSettings.Value.FixtureDirectory,
                $"rag-eval-dataset-{datasetName}.json");
            datasetResult = EvalFixtureLoader.LoadFromFile(path);
        }
        if (datasetResult.IsFailure)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(ApiResponse.Fail(datasetResult.Error.Description), ct);
            return;
        }

        var options = new EvalRunOptions(
            KValues: evalSettings.Value.KValues,
            IncludeFaithfulness: true,
            JudgeModelOverride: judgeModel ?? evalSettings.Value.JudgeModel,
            WarmupQueries: evalSettings.Value.WarmupQueries,
            AssistantId: assistantId,
            AssistantSystemPrompt: assistant.SystemPrompt,
            AssistantModel: assistant.Model);

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable Nginx buffering

        await foreach (var evt in harness.RunStreamingAsync(datasetResult.Value, options, ct))
        {
            var json = JsonSerializer.Serialize((object)evt, StreamJsonOptions);
            await Response.WriteAsync($"event: {evt.Type}\n", ct);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
            if (evt.Type is "run_completed" or "run_error") break;
        }
    }
}
