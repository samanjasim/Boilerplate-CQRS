using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class ContextualQueryResolver : IContextualQueryResolver
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ContextualQueryResolver> _logger;

    public ContextualQueryResolver(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<ContextualQueryResolver> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(latestUserMessage)) return latestUserMessage;
        if (!_settings.EnableContextualRewrite) return latestUserMessage;
        if (history.Count == 0) return latestUserMessage;

        // Heuristic + cache + LLM path: filled in by Task 5+.
        await Task.CompletedTask;
        return latestUserMessage;
    }
}
