using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;

internal sealed class RunFaithfulnessEvalCommandHandler(
    IRagEvalHarness harness,
    AiDbContext db,
    IOptions<AiRagEvalSettings> settings)
    : IRequestHandler<RunFaithfulnessEvalCommand, Result<EvalReport>>
{
    public async Task<Result<EvalReport>> Handle(
        RunFaithfulnessEvalCommand request, CancellationToken ct)
    {
        var assistant = await db.AiAssistants.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId, ct);
        if (assistant is null) return Result.Failure<EvalReport>(EvalErrors.AssistantNotFound);

        Result<EvalDataset> datasetResult;
        if (!string.IsNullOrWhiteSpace(request.FixtureJson))
        {
            datasetResult = EvalFixtureLoader.LoadFromString(request.FixtureJson);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.DatasetName))
                return Result.Failure<EvalReport>(EvalErrors.FixtureNotFound);
            var path = Path.Combine(
                settings.Value.FixtureDirectory,
                $"rag-eval-dataset-{request.DatasetName}.json");
            datasetResult = EvalFixtureLoader.LoadFromFile(path);
        }
        if (datasetResult.IsFailure) return Result.Failure<EvalReport>(datasetResult.Error);

        var report = await harness.RunAsync(
            datasetResult.Value,
            new EvalRunOptions(
                KValues: settings.Value.KValues,
                IncludeFaithfulness: true,
                JudgeModelOverride: request.JudgeModelOverride ?? settings.Value.JudgeModel,
                WarmupQueries: settings.Value.WarmupQueries,
                AssistantId: request.AssistantId,
                AssistantSystemPrompt: assistant.SystemPrompt,
                AssistantModel: assistant.Model),
            ct);

        return Result.Success(report);
    }
}
