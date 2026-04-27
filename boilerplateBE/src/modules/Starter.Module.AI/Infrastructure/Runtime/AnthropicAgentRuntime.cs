using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Runtime;

internal sealed class AnthropicAgentRuntime(
    IAiProviderFactory providerFactory,
    IAgentToolDispatcher toolDispatcher,
    AiDbContext aiDb,
    IAgentPermissionResolver agentPermissions,
    ILogger<AgentRuntimeBase> logger)
    : AgentRuntimeBase(providerFactory, toolDispatcher, aiDb, agentPermissions, logger);
