using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Ollama llama3.1 has no native tool-calling. If a caller passes tools we log a
/// warning and strip them from the context; the base loop will never see a tool_calls
/// response and will terminate in one step. Downstream sub-plans may override
/// RunAsync when specific Ollama models gain tool support.
/// </summary>
internal sealed class OllamaAgentRuntime(
    IAiProviderFactory providerFactory,
    IAgentToolDispatcher toolDispatcher,
    ILogger<AgentRuntimeBase> logger)
    : AgentRuntimeBase(providerFactory, toolDispatcher, logger)
{
    public override async Task<AgentRunResult> RunAsync(
        AgentRunContext context,
        IAgentRunSink sink,
        CancellationToken ct = default)
    {
        if (context.Tools.ProviderTools.Count > 0)
        {
            logger.LogInformation(
                "Ollama runtime invoked with {ToolCount} tools; stripping because the provider has no native tool calling.",
                context.Tools.ProviderTools.Count);

            context = context with
            {
                Tools = new ToolResolutionResult(
                    ProviderTools: Array.Empty<AiToolDefinitionDto>(),
                    DefinitionsByName: context.Tools.DefinitionsByName)
            };
        }

        return await base.RunAsync(context, sink, ct);
    }
}
