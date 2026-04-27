using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Runtime;

internal sealed class AiAgentRuntimeFactory(IServiceProvider services) : IAiAgentRuntimeFactory
{
    public IAiAgentRuntime Create(AiProviderType providerType)
    {
        IAiAgentRuntime inner = providerType switch
        {
            AiProviderType.OpenAI => services.GetRequiredService<OpenAiAgentRuntime>(),
            AiProviderType.Anthropic => services.GetRequiredService<AnthropicAgentRuntime>(),
            AiProviderType.Ollama => services.GetRequiredService<OllamaAgentRuntime>(),
            _ => throw new NotSupportedException($"No agent runtime registered for provider {providerType}.")
        };

        // Plan 5d-1: wrap the provider-specific runtime in the cost-cap-enforcing decorator
        // so every run goes through atomic claim + rate-limit + reconcile. The decorator is
        // a no-op when ctx.AssistantId/TenantId are missing (legacy/test paths).
        return new CostCapEnforcingAgentRuntime(
            inner,
            services.GetRequiredService<ICostCapResolver>(),
            services.GetRequiredService<ICostCapAccountant>(),
            services.GetRequiredService<IAgentRateLimiter>(),
            services.GetRequiredService<IModelPricingService>(),
            services.GetRequiredService<ILogger<CostCapEnforcingAgentRuntime>>());
    }
}
