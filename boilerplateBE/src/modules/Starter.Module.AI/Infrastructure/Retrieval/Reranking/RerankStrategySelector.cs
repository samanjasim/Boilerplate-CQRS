using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class RerankStrategySelector
{
    private readonly AiRagSettings _settings;

    public RerankStrategySelector(AiRagSettings settings)
    {
        _settings = settings;
    }

    public RerankStrategy Resolve(RerankContext ctx)
    {
        if (ctx.StrategyOverride is { } o)
            return o;

        var cfg = _settings.RerankStrategy;
        if (cfg != RerankStrategy.Auto)
            return cfg;

        return ctx.QuestionType switch
        {
            QuestionType.Greeting => RerankStrategy.Off,
            QuestionType.Reasoning => RerankStrategy.Pointwise,
            QuestionType.Definition => RerankStrategy.Listwise,
            QuestionType.Listing => RerankStrategy.Listwise,
            QuestionType.Other => RerankStrategy.Listwise,
            null => RerankStrategy.Listwise,
            _ => RerankStrategy.Listwise
        };
    }
}
