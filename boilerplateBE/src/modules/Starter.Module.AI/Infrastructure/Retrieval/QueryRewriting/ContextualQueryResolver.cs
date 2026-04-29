using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class ContextualQueryResolver : IContextualQueryResolver
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly IAiModelDefaultResolver _modelDefaults;
    private readonly IAiProviderCredentialResolver _providerCredentials;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ContextualQueryResolver> _logger;

    public ContextualQueryResolver(
        IAiProviderFactory factory,
        ICacheService cache,
        IAiModelDefaultResolver modelDefaults,
        IAiProviderCredentialResolver providerCredentials,
        IOptions<AiRagSettings> settings,
        ILogger<ContextualQueryResolver> logger)
    {
        _factory = factory;
        _cache = cache;
        _modelDefaults = modelDefaults;
        _providerCredentials = providerCredentials;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(
        Guid tenantId,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(latestUserMessage)) return latestUserMessage;
        if (!_settings.EnableContextualRewrite) return latestUserMessage;
        if (history.Count == 0) return latestUserMessage;
        if (!ContextualFollowUpHeuristic.LooksLikeFollowUp(latestUserMessage))
        {
            _logger.LogDebug("contextualize: heuristic-skip original={Original}", latestUserMessage);
            return latestUserMessage;
        }

        var providerState = await ResolveRagHelperProviderAsync(tenantId, _settings.ContextualRewriterModel, 0.2, 200, null, ct);
        if (providerState is null)
            return latestUserMessage;

        var cacheKey = BuildCacheKey(
            tenantId,
            providerState.Value.ProviderType.ToString(),
            providerState.Value.Options.Model,
            latestUserMessage,
            history,
            language);

        string? cached = null;
        try
        {
            cached = await _cache.GetAsync<string>(cacheKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ContextualQueryResolver: cache Get failed; proceeding without cache");
        }

        AiRagMetrics.CacheRequests.Add(
            1,
            new KeyValuePair<string, object?>("rag.cache", "contextualize"),
            new KeyValuePair<string, object?>("rag.hit", cached is not null));

        if (cached is { Length: > 0 })
        {
            _logger.LogDebug("contextualize: cache-hit resolved={Resolved}", cached);
            return cached;
        }

        var resolved = await TryCallLlmAsync(providerState.Value.Provider, providerState.Value.Options, latestUserMessage, history, language, ct);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            _logger.LogDebug("contextualize: llm-empty falling-back original={Original}", latestUserMessage);
            return latestUserMessage;
        }

        var originalLang = RagLanguageDetector.Detect(latestUserMessage);
        var resolvedLang = RagLanguageDetector.Detect(resolved);
        if (originalLang != RagLanguageDetector.Unknown
            && resolvedLang != RagLanguageDetector.Unknown
            && originalLang != RagLanguageDetector.Mixed
            && resolvedLang != RagLanguageDetector.Mixed
            && originalLang != resolvedLang)
        {
            _logger.LogWarning("contextualize: detected translation {From}->{To}; falling back", originalLang, resolvedLang);
            return latestUserMessage;
        }

        if (_settings.ContextualRewriteCacheTtlSeconds > 0)
        {
            try
            {
                await _cache.SetAsync(
                    cacheKey, resolved,
                    TimeSpan.FromSeconds(_settings.ContextualRewriteCacheTtlSeconds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ContextualQueryResolver: cache Set failed; continuing");
            }
        }

        _logger.LogDebug(
            "contextualize: original={Original} resolved={Resolved} lang={Lang} skipped={Skipped}",
            latestUserMessage, resolved, language ?? originalLang, false);

        return resolved;
    }

    private async Task<string?> TryCallLlmAsync(
        IAiProvider provider,
        AiChatOptions options,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
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
                "Given the recent conversation and the user's latest message, rewrite the latest message into a single " +
                "self-contained question that preserves the user's intent. Reply in the same language as the user. " +
                "Do NOT translate. If the message is already self-contained, return it unchanged.";

            var turns = history
                .TakeLast(Math.Max(1, _settings.ContextualRewriteHistoryTurns) * 2)
                .Select(t => $"{t.Role}: {t.Content.Trim()}");
            var historyText = string.Join("\n", turns);

            var userPrompt =
                $"Language hint: {langHint}\n" +
                $"Conversation (oldest first):\n{historyText}\n" +
                $"Latest message: {latestUserMessage}\n" +
                $"Self-contained rewrite:";

            var messages = new List<AiChatMessage> { new("user", userPrompt) };
            var completion = await provider.ChatAsync(messages, options with { SystemPrompt = systemPrompt }, ct);

            return StripSurroundingQuotes(completion.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ContextualQueryResolver: LLM call failed; falling back to raw message");
            return null;
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

    private static string? StripSurroundingQuotes(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var trimmed = s.Trim();
        if (trimmed.Length >= 2
            && ((trimmed[0] == '"' && trimmed[^1] == '"')
             || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1].Trim();
        }
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private string BuildCacheKey(
        Guid tenantId,
        string providerType,
        string model,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language)
    {
        var lang = language ?? RagLanguageDetector.Detect(latestUserMessage);

        var normalizedHistory = history
            .TakeLast(Math.Max(1, _settings.ContextualRewriteHistoryTurns) * 2)
            .Select(t => $"{t.Role}:{Normalize(t.Content)}");
        var normalizedMessage = Normalize(latestUserMessage);
        var payload = string.Join("\n", normalizedHistory) + "\n---\n" + normalizedMessage;

        return RagCacheKeys.Contextualize(tenantId, providerType, model, lang, payload);
    }

    private string Normalize(string s) =>
        ArabicTextNormalizer.Normalize((s ?? string.Empty).Trim(), _settings.ToArabicOptions());
}
