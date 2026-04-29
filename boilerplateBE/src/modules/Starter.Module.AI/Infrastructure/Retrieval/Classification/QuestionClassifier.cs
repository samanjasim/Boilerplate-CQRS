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

namespace Starter.Module.AI.Infrastructure.Retrieval.Classification;

internal sealed class QuestionClassifier : IQuestionClassifier
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly IAiModelDefaultResolver _modelDefaults;
    private readonly IAiProviderCredentialResolver _providerCredentials;
    private readonly AiRagSettings _settings;
    private readonly ILogger<QuestionClassifier> _logger;

    public QuestionClassifier(
        IAiProviderFactory factory,
        ICacheService cache,
        IAiModelDefaultResolver modelDefaults,
        IAiProviderCredentialResolver providerCredentials,
        IOptions<AiRagSettings> settings,
        ILogger<QuestionClassifier> logger)
    {
        _factory = factory;
        _cache = cache;
        _modelDefaults = modelDefaults;
        _providerCredentials = providerCredentials;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<QuestionType?> ClassifyAsync(Guid tenantId, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return QuestionType.Other;

        var regexHit = RegexQuestionClassifier.TryClassify(query);
        if (regexHit is not null)
            return regexHit;

        var normalized = _settings.ApplyArabicNormalization
            ? ArabicTextNormalizer.Normalize(query, _settings.ToArabicOptions())
            : query;

        var providerState = await ResolveRagHelperProviderAsync(tenantId, _settings.ClassifierModel, 0.0, 8, null, ct);
        if (providerState is null)
            return null;

        var key = BuildCacheKey(tenantId, providerState.Value.ProviderType.ToString(), providerState.Value.Options.Model, normalized);
        var cached = await _cache.GetAsync<string>(key, ct);
        AiRagMetrics.CacheRequests.Add(
            1,
            new KeyValuePair<string, object?>("rag.cache", "classify"),
            new KeyValuePair<string, object?>("rag.hit", !string.IsNullOrEmpty(cached)));
        if (!string.IsNullOrEmpty(cached))
            return ParseLabel(cached);

        try
        {
            var label = await CallLlmAsync(providerState.Value.Provider, providerState.Value.Options, query, ct);
            await _cache.SetAsync(key, label, TimeSpan.FromSeconds(_settings.QuestionCacheTtlSeconds), ct);
            return ParseLabel(label);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Question classification LLM failed; returning null");
            return null;
        }
    }

    private async Task<string> CallLlmAsync(IAiProvider provider, AiChatOptions options, string query, CancellationToken ct)
    {
        const string system =
            "Classify the user question into EXACTLY one label: Greeting, Definition, Listing, Reasoning, Other. " +
            "Output ONLY the label word, no punctuation, no explanation.";

        var msgs = new List<AiChatMessage> { new("user", query) };
        var resp = await provider.ChatAsync(msgs, options with { SystemPrompt = system }, ct);
        return (resp.Content ?? string.Empty).Trim();
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

    private static QuestionType ParseLabel(string label) =>
        label.Trim().ToLowerInvariant() switch
        {
            "greeting" => QuestionType.Greeting,
            "definition" => QuestionType.Definition,
            "listing" => QuestionType.Listing,
            "reasoning" => QuestionType.Reasoning,
            _ => QuestionType.Other
        };

    private string BuildCacheKey(Guid tenantId, string provider, string model, string normalized)
    {
        return RagCacheKeys.Classify(tenantId, provider, model, normalized);
    }
}
