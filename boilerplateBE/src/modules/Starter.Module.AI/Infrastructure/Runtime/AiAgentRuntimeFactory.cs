using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

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

        // Plan 5d-1: cost-cap layer — atomic claim + rate-limit + reconcile. No-op when
        // ctx.AssistantId/TenantId are missing (legacy/test paths).
        var costEnforced = new CostCapEnforcingAgentRuntime(
            inner,
            services.GetRequiredService<ICostCapResolver>(),
            services.GetRequiredService<ICostCapAccountant>(),
            services.GetRequiredService<IAgentRateLimiter>(),
            services.GetRequiredService<IModelPricingService>(),
            services.GetRequiredService<ILogger<CostCapEnforcingAgentRuntime>>());

        // Plan 5d-2: moderation layer — outermost so input scan happens before any cost
        // claim or rate-limit increment, and output scan/buffering wraps everything the
        // inner layers emit. No-op when ctx.AssistantId/TenantId are missing.
        return new ContentModerationEnforcingAgentRuntime(
            costEnforced,
            services.GetRequiredService<IContentModerator>(),
            services.GetRequiredService<IPiiRedactor>(),
            services.GetRequiredService<ISafetyProfileResolver>(),
            services.GetRequiredService<IModerationRefusalProvider>(),
            services.GetRequiredService<AiDbContext>(),
            services.GetRequiredService<IWebhookPublisher>(),
            services.GetRequiredService<ILogger<ContentModerationEnforcingAgentRuntime>>());
    }
}
