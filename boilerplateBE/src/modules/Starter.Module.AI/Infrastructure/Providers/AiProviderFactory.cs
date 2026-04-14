using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class AiProviderFactory(
    IServiceProvider serviceProvider,
    IConfiguration configuration)
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

    public IAiProvider CreateDefault() => Create(GetDefaultProviderType());
}
