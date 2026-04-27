using MediatR;
using Starter.Abstractions.Ai;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetModelPricing;

public sealed record GetModelPricingQuery(bool ActiveOnly = true) : IRequest<Result<IReadOnlyList<ModelPricingDto>>>;

public sealed record ModelPricingDto(
    Guid Id,
    AiProviderType Provider,
    string Model,
    decimal InputUsdPer1KTokens,
    decimal OutputUsdPer1KTokens,
    bool IsActive,
    DateTimeOffset EffectiveFrom);
