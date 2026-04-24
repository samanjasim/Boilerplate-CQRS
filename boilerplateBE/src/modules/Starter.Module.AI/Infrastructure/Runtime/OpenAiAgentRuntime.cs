using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Runtime;

internal sealed class OpenAiAgentRuntime(
    IAiProviderFactory providerFactory,
    IAgentToolDispatcher toolDispatcher,
    ILogger<AgentRuntimeBase> logger)
    : AgentRuntimeBase(providerFactory, toolDispatcher, logger);
