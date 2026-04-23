using MediatR;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;

public sealed record RunFaithfulnessEvalCommand(
    string? FixtureJson,
    string? DatasetName,
    Guid AssistantId,
    string? JudgeModelOverride) : IRequest<Result<EvalReport>>;
