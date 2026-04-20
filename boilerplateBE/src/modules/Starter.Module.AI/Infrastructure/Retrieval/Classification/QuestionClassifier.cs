using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Classification;

internal sealed class QuestionClassifier : IQuestionClassifier
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<QuestionClassifier> _logger;

    public QuestionClassifier(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<QuestionClassifier> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<QuestionType?> ClassifyAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return QuestionType.Other;

        var regexHit = RegexQuestionClassifier.TryClassify(query);
        if (regexHit is not null)
            return regexHit;

        var normalized = _settings.ApplyArabicNormalization
            ? ArabicTextNormalizer.Normalize(query, _settings.ToArabicOptions())
            : query;

        var key = BuildCacheKey(normalized);
        var cached = await _cache.GetAsync<string>(key, ct);
        if (!string.IsNullOrEmpty(cached))
            return ParseLabel(cached);

        try
        {
            var provider = _factory.CreateDefault();
            var label = await CallLlmAsync(provider, query, ct);
            await _cache.SetAsync(key, label, TimeSpan.FromSeconds(_settings.QuestionCacheTtlSeconds), ct);
            return ParseLabel(label);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Question classification LLM failed; returning null");
            return null;
        }
    }

    private async Task<string> CallLlmAsync(IAiProvider provider, string query, CancellationToken ct)
    {
        const string system =
            "Classify the user question into EXACTLY one label: Greeting, Definition, Listing, Reasoning, Other. " +
            "Output ONLY the label word, no punctuation, no explanation.";

        var opts = new AiChatOptions(
            Model: _settings.ClassifierModel ?? _factory.GetDefaultChatModelId(),
            Temperature: 0.0,
            MaxTokens: 8,
            SystemPrompt: system);

        var msgs = new List<AiChatMessage> { new("user", query) };
        var resp = await provider.ChatAsync(msgs, opts, ct);
        return (resp.Content ?? string.Empty).Trim();
    }

    private static QuestionType ParseLabel(string label) =>
        label.Trim().ToLowerInvariant() switch
        {
            "greeting" => QuestionType.Greeting,
            "definition" => QuestionType.Definition,
            "listing" => QuestionType.Listing,
            "reasoning" => QuestionType.Reasoning,
            _ => QuestionType.Other
        };

    private string BuildCacheKey(string normalized)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.ClassifierModel ?? _factory.GetDefaultChatModelId();
        return RagCacheKeys.Classify(provider, model, normalized);
    }
}
