using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class QueryRewriter : IQueryRewriter
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly IAiModelDefaultResolver _modelDefaults;
    private readonly IAiProviderCredentialResolver _providerCredentials;
    private readonly AiRagSettings _settings;
    private readonly ILogger<QueryRewriter> _logger;

    public QueryRewriter(
        IAiProviderFactory factory,
        ICacheService cache,
        IAiModelDefaultResolver modelDefaults,
        IAiProviderCredentialResolver providerCredentials,
        IOptions<AiRagSettings> settings,
        ILogger<QueryRewriter> logger)
    {
        _factory = factory;
        _cache = cache;
        _modelDefaults = modelDefaults;
        _providerCredentials = providerCredentials;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> RewriteAsync(
        Guid tenantId, string originalQuery, string? language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(originalQuery))
            return Array.Empty<string>();

        var ruleVariants = RuleBasedQueryRewriter.Rewrite(originalQuery);

        if (!_settings.EnableQueryExpansion)
            return ruleVariants;

        var providerState = await ResolveRagHelperProviderAsync(tenantId, _settings.RewriterModel, 0.2, 256, null, ct);
        if (providerState is null)
            return ruleVariants;

        var cacheKey = BuildCacheKey(tenantId, providerState.Value.ProviderType.ToString(), providerState.Value.Options.Model, originalQuery, language);
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
            llmVariants = await TryCallLlmAsync(providerState.Value.Provider, providerState.Value.Options, originalQuery, language, ct);
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
        IAiProvider provider, AiChatOptions options, string query, string? language, CancellationToken ct)
    {
        try
        {
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

            var messages = new List<AiChatMessage> { new("user", userPrompt) };
            var completion = await provider.ChatAsync(messages, options with { SystemPrompt = systemPrompt }, ct);

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

    private async Task<(AiProviderType ProviderType, IAiProvider Provider, AiChatOptions Options)?> ResolveRagHelperProviderAsync(
        Guid tenantId,
        string? overrideModel,
        double temperature,
        int maxTokens,
        string? systemPrompt,
        CancellationToken ct)
    {
        var modelResult = await _modelDefaults.ResolveAsync(
            tenantId,
            AiAgentClass.RagHelper,
            explicitProvider: null,
            explicitModel: overrideModel,
            explicitTemperature: temperature,
            explicitMaxTokens: maxTokens,
            ct);
        if (modelResult.IsFailure)
        {
            _logger.LogWarning("RAG helper model resolution failed: {Error}", modelResult.Error.Description);
            return null;
        }

        var credentialResult = await _providerCredentials.ResolveAsync(tenantId, modelResult.Value.Provider, ct);
        if (credentialResult.IsFailure)
        {
            _logger.LogWarning("RAG helper provider credential resolution failed: {Error}", credentialResult.Error.Description);
            return null;
        }

        var credential = credentialResult.Value;
        var provider = _factory.Create(modelResult.Value.Provider);
        var options = new AiChatOptions(
            Model: modelResult.Value.Model,
            Temperature: modelResult.Value.Temperature,
            MaxTokens: modelResult.Value.MaxTokens,
            SystemPrompt: systemPrompt,
            ApiKey: credential.Secret,
            ProviderCredentialSource: credential.Source,
            ProviderCredentialId: credential.ProviderCredentialId);

        return (modelResult.Value.Provider, provider, options);
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

    private string BuildCacheKey(Guid tenantId, string provider, string model, string query, string? language)
    {
        return RagCacheKeys.QueryRewrite(tenantId, provider, model, language ?? string.Empty, Normalize(query));
    }
}
