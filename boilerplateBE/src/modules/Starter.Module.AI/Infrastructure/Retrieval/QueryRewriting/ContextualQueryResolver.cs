using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Observability;
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
        if (!ContextualFollowUpHeuristic.LooksLikeFollowUp(latestUserMessage))
        {
            _logger.LogDebug("contextualize: heuristic-skip original={Original}", latestUserMessage);
            return latestUserMessage;
        }

        var cacheKey = BuildCacheKey(latestUserMessage, history, language);

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

        var resolved = await TryCallLlmAsync(latestUserMessage, history, language, ct);
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
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
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

            var model = _settings.ContextualRewriterModel ?? _factory.GetDefaultChatModelId();
            var opts = new AiChatOptions(
                Model: model,
                Temperature: 0.2,
                MaxTokens: 200,
                SystemPrompt: systemPrompt);

            var messages = new List<AiChatMessage> { new("user", userPrompt) };
            var completion = await provider.ChatAsync(messages, opts, ct);

            return StripSurroundingQuotes(completion.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "ContextualQueryResolver: LLM call failed; falling back to raw message");
            return null;
        }
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
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language)
    {
        var providerType = _factory.GetDefaultProviderType().ToString();
        var model = _settings.ContextualRewriterModel ?? _factory.GetDefaultChatModelId();
        var lang = language ?? RagLanguageDetector.Detect(latestUserMessage);

        var normalizedHistory = history
            .TakeLast(Math.Max(1, _settings.ContextualRewriteHistoryTurns) * 2)
            .Select(t => $"{t.Role}:{Normalize(t.Content)}");
        var normalizedMessage = Normalize(latestUserMessage);
        var payload = string.Join("\n", normalizedHistory) + "\n---\n" + normalizedMessage;

        return RagCacheKeys.Contextualize(providerType, model, lang, payload);
    }

    private string Normalize(string s) =>
        ArabicTextNormalizer.Normalize((s ?? string.Empty).Trim(), _settings.ToArabicOptions());
}
