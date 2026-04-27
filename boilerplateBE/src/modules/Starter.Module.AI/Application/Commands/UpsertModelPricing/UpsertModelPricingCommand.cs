using MediatR;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Queries.GetModelPricing;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpsertModelPricing;

public sealed record UpsertModelPricingCommand(
    AiProviderType Provider,
    string Model,
    decimal InputUsdPer1KTokens,
    decimal OutputUsdPer1KTokens,
    DateTimeOffset? EffectiveFrom) : IRequest<Result<ModelPricingDto>>;
