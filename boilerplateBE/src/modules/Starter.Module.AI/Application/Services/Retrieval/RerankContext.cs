namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RerankContext(
    QuestionType? QuestionType,
    RerankStrategy? StrategyOverride,
    Guid TenantId);
