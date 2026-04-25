using Microsoft.Extensions.DependencyInjection;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Runtime;

internal sealed class AiAgentRuntimeFactory(IServiceProvider services) : IAiAgentRuntimeFactory
{
    public IAiAgentRuntime Create(AiProviderType providerType) => providerType switch
    {
        AiProviderType.OpenAI => services.GetRequiredService<OpenAiAgentRuntime>(),
        AiProviderType.Anthropic => services.GetRequiredService<AnthropicAgentRuntime>(),
        AiProviderType.Ollama => services.GetRequiredService<OllamaAgentRuntime>(),
        _ => throw new NotSupportedException($"No agent runtime registered for provider {providerType}.")
    };
}
