using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class AiProviderFactory(
    IConfiguration configuration,
    ILoggerFactory loggerFactory)
{
    public IAiProvider Create(AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.Anthropic => new AnthropicAiProvider(configuration, loggerFactory.CreateLogger<AnthropicAiProvider>()),
            AiProviderType.OpenAI => new OpenAiProvider(configuration, loggerFactory.CreateLogger<OpenAiProvider>()),
            AiProviderType.Ollama => new OllamaAiProvider(configuration, loggerFactory.CreateLogger<OllamaAiProvider>()),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, "Unknown AI provider type")
        };
    }

    public AiProviderType GetDefaultProviderType()
    {
        var providerStr = configuration["AI:DefaultProvider"] ?? "Anthropic";
        return Enum.Parse<AiProviderType>(providerStr, ignoreCase: true);
    }

    public IAiProvider CreateDefault() => Create(GetDefaultProviderType());
}
