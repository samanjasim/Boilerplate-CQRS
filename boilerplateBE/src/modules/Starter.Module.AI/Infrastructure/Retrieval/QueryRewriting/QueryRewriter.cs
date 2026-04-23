using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class QueryRewriter : IQueryRewriter
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<QueryRewriter> _logger;

    public QueryRewriter(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<QueryRewriter> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> RewriteAsync(
        string originalQuery, string? language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(originalQuery))
            return Array.Empty<string>();

        var ruleVariants = RuleBasedQueryRewriter.Rewrite(originalQuery);

        if (!_settings.EnableQueryExpansion)
            return ruleVariants;

        var cacheKey = BuildCacheKey(originalQuery, language);
        var cached = await _cache.GetAsync<List<string>>(cacheKey, ct);
        AiRagMetrics.CacheRequests.Add(
            1,
            new KeyValuePair<string, object?>("rag.cache", "rewrite"),
            new KeyValuePair<string, object?>("rag.hit", cached is not null));
        IReadOnlyList<string> llmVariants;
        if (cached is not null)
        {
            llmVariants = cached;
        }
        else
        {
            llmVariants = await TryCallLlmAsync(originalQuery, language, ct);
            if (llmVariants.Count > 0 && _settings.QueryRewriteCacheTtlSeconds > 0)
            {
                await _cache.SetAsync(
                    cacheKey, llmVariants.ToList(),
                    TimeSpan.FromSeconds(_settings.QueryRewriteCacheTtlSeconds), ct);
            }
        }

        return Merge(ruleVariants, llmVariants, _settings.QueryRewriteMaxVariants);
    }

    private async Task<IReadOnlyList<string>> TryCallLlmAsync(
        string query, string? language, CancellationToken ct)
    {
        try
        {
            var provider = _factory.CreateDefault();
            var langHint = language switch
            {
                "ar" => "Arabic",
                "en" => "English",
                _ => "the same language as the input"
            };

            var systemPrompt =
                "You rewrite a user's question into 2 alternative phrasings that preserve the information need. " +
                "You may receive questions in Arabic or English. Reply in the same language as the input. Do NOT translate. " +
                "Respond with a JSON array of exactly 2 alternative phrasings as strings. No commentary.";

            var userPrompt = $"Language hint: {langHint}\nOriginal question: {query}";

            var model = _settings.RewriterModel ?? _factory.GetDefaultChatModelId();
            var opts = new AiChatOptions(
                Model: model,
                Temperature: 0.2,
                MaxTokens: 256,
                SystemPrompt: systemPrompt);

            var messages = new List<AiChatMessage> { new("user", userPrompt) };
            var completion = await provider.ChatAsync(messages, opts, ct);

            if (completion.Content is null || !JsonArrayExtractor.TryExtractStrings(completion.Content, out var variants))
            {
                _logger.LogWarning("QueryRewriter: LLM output did not contain a JSON array");
                return Array.Empty<string>();
            }
            return variants;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "QueryRewriter: LLM call failed; falling back to rule variants only");
            return Array.Empty<string>();
        }
    }

    private IReadOnlyList<string> Merge(
        IReadOnlyList<string> ruleVariants,
        IReadOnlyList<string> llmVariants,
        int cap)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(cap);

        foreach (var v in ruleVariants)
        {
            var norm = Normalize(v);
            if (seen.Add(norm)) result.Add(v);
            if (result.Count >= cap) return result;
        }
        foreach (var v in llmVariants)
        {
            var norm = Normalize(v);
            if (seen.Add(norm)) result.Add(v);
            if (result.Count >= cap) return result;
        }
        return result;
    }

    private string Normalize(string s) =>
        ArabicTextNormalizer.Normalize(s.Trim(), _settings.ToArabicOptions());

    private string BuildCacheKey(string query, string? language)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.RewriterModel ?? _factory.GetDefaultChatModelId();
        return RagCacheKeys.QueryRewrite(provider, model, language ?? string.Empty, Normalize(query));
    }
}
