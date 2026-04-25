using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Runtime;

internal interface IAiAgentRuntimeFactory
{
    IAiAgentRuntime Create(AiProviderType providerType);
}
