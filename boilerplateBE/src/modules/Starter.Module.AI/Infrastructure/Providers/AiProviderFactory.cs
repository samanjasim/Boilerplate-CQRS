using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Providers;

internal interface IAiProviderFactory
{
    IAiProvider Create(AiProviderType providerType);
    AiProviderType GetDefaultProviderType();
    AiProviderType GetEmbeddingProviderType();
    IAiProvider CreateDefault();
    IAiProvider CreateForEmbeddings();

    /// <summary>
    /// Stable identifier for the embedding model currently in use
    /// (e.g. "OpenAI:text-embedding-3-small"). Used to namespace caches
    /// so a model change invalidates previously cached vectors.
    /// </summary>
    string GetEmbeddingModelId();

    /// <summary>
    /// Stable identifier for the default chat model currently in use
    /// (e.g. "OpenAI:gpt-4o-mini"). Used by reranker, rewriter, and classifier
    /// handlers that need a chat model separate from the embedding model.
    /// </summary>
    string GetDefaultChatModelId();
}

internal sealed class AiProviderFactory(
    IServiceProvider serviceProvider,
    IConfiguration configuration) : IAiProviderFactory
{
    public IAiProvider Create(AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.Anthropic => serviceProvider.GetRequiredService<AnthropicAiProvider>(),
            AiProviderType.OpenAI => serviceProvider.GetRequiredService<OpenAiProvider>(),
            AiProviderType.Ollama => serviceProvider.GetRequiredService<OllamaAiProvider>(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, "Unknown AI provider type")
        };
    }

    public AiProviderType GetDefaultProviderType()
    {
        var providerStr = configuration["AI:DefaultProvider"] ?? "Anthropic";
        return Enum.Parse<AiProviderType>(providerStr, ignoreCase: true);
    }

    public AiProviderType GetEmbeddingProviderType()
    {
        var providerStr = configuration["AI:EmbeddingProvider"];
        return string.IsNullOrWhiteSpace(providerStr)
            ? GetDefaultProviderType()
            : Enum.Parse<AiProviderType>(providerStr, ignoreCase: true);
    }

    public IAiProvider CreateDefault() => Create(GetDefaultProviderType());

    public IAiProvider CreateForEmbeddings() => Create(GetEmbeddingProviderType());

    public string GetEmbeddingModelId()
    {
        var providerType = GetEmbeddingProviderType();
        var modelName = providerType switch
        {
            AiProviderType.OpenAI => configuration["AI:Providers:OpenAI:EmbeddingModel"] ?? "text-embedding-3-small",
            AiProviderType.Ollama => configuration["AI:Providers:Ollama:EmbeddingModel"] ?? "nomic-embed-text",
            AiProviderType.Anthropic => configuration["AI:Providers:Anthropic:EmbeddingModel"] ?? "default",
            _ => "default"
        };
        return $"{providerType}:{modelName}";
    }

    public string GetDefaultChatModelId()
    {
        var providerType = GetDefaultProviderType();
        var modelName = providerType switch
        {
            AiProviderType.OpenAI => configuration["AI:Providers:OpenAI:ChatModel"] ?? "gpt-4o-mini",
            AiProviderType.Ollama => configuration["AI:Providers:Ollama:ChatModel"] ?? "llama3",
            AiProviderType.Anthropic => configuration["AI:Providers:Anthropic:ChatModel"] ?? "claude-3-5-haiku-20241022",
            _ => "default"
        };
        return $"{providerType}:{modelName}";
    }
}
