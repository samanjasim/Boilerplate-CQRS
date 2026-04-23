using System.Text;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Shared multi-step agent loop. Per-provider runtimes derive from this and gain the
/// full loop for free. When we later need provider-native behavior (e.g. OpenAI
/// Responses, Anthropic native tool-use), a specific subclass can override RunAsync.
/// </summary>
internal abstract class AgentRuntimeBase(
    IAiProviderFactory providerFactory,
    IAgentToolDispatcher toolDispatcher,
    ILogger<AgentRuntimeBase> logger) : IAiAgentRuntime
{
    protected ILogger<AgentRuntimeBase> Logger { get; } = logger;

    public virtual Task<AgentRunResult> RunAsync(
        AgentRunContext ctx,
        IAgentRunSink sink,
        CancellationToken ct = default)
    {
        return ctx.Streaming
            ? RunStreamingAsync(ctx, sink, ct)
            : RunNonStreamingAsync(ctx, sink, ct);
    }

    private async Task<AgentRunResult> RunNonStreamingAsync(
        AgentRunContext ctx,
        IAgentRunSink sink,
        CancellationToken ct)
    {
        var provider = providerFactory.Create(ctx.ModelConfig.Provider);
        var chatOptions = new AiChatOptions(
            Model: ctx.ModelConfig.Model,
            Temperature: ctx.ModelConfig.Temperature,
            MaxTokens: ctx.ModelConfig.MaxTokens,
            SystemPrompt: ctx.SystemPrompt,
            Tools: ctx.Tools.ProviderTools.Count == 0 ? null : ctx.Tools.ProviderTools);

        var messages = new List<AiChatMessage>(ctx.Messages);
        var steps = new List<AgentStepEvent>();
        var detector = new LoopBreakDetector(ctx.LoopBreak);
        var totalInput = 0;
        var totalOutput = 0;

        for (var stepIndex = 0; stepIndex < ctx.MaxSteps; stepIndex++)
        {
            if (ct.IsCancellationRequested)
                return await FinalizeAsync(sink, AgentRunStatus.Cancelled, null,
                    "cancelled", steps, totalInput, totalOutput, ct);

            await sink.OnStepStartedAsync(stepIndex, ct);
            var startedAt = DateTimeOffset.UtcNow;

            AiChatCompletion completion;
            try
            {
                completion = await provider.ChatAsync(messages, chatOptions, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return await FinalizeAsync(sink, AgentRunStatus.Cancelled, null,
                    "cancelled", steps, totalInput, totalOutput, ct);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Agent runtime provider call failed at step {Step}", stepIndex);
                return await FinalizeAsync(sink, AgentRunStatus.ProviderError, null,
                    ex.Message, steps, totalInput, totalOutput, ct);
            }

            totalInput += completion.InputTokens;
            totalOutput += completion.OutputTokens;

            var toolCalls = completion.ToolCalls ?? Array.Empty<AiToolCall>();

            // No tool calls → final step.
            if (toolCalls.Count == 0)
            {
                var finalStep = new AgentStepEvent(
                    stepIndex, AgentStepKind.Final,
                    completion.Content, Array.Empty<AgentToolInvocation>(),
                    completion.InputTokens, completion.OutputTokens,
                    completion.FinishReason, startedAt, DateTimeOffset.UtcNow);

                steps.Add(finalStep);
                await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
                    stepIndex, completion.Content, Array.Empty<AiToolCall>(),
                    completion.InputTokens, completion.OutputTokens), ct);
                await sink.OnStepCompletedAsync(finalStep, ct);

                return await FinalizeAsync(sink, AgentRunStatus.Completed,
                    completion.Content, null, steps, totalInput, totalOutput, ct);
            }

            // Tool-call step.
            await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
                stepIndex, completion.Content, toolCalls,
                completion.InputTokens, completion.OutputTokens), ct);

            messages.Add(new AiChatMessage("assistant", completion.Content, ToolCalls: toolCalls));

            var invocations = new List<AgentToolInvocation>(toolCalls.Count);
            string? loopBreakTool = null;

            foreach (var call in toolCalls)
            {
                if (detector.ShouldBreak(call))
                {
                    loopBreakTool = call.Name;
                    break;
                }

                await sink.OnToolCallAsync(new AgentToolCallEvent(stepIndex, call), ct);
                var invStart = DateTimeOffset.UtcNow;
                var dispatch = await toolDispatcher.DispatchAsync(call, ctx.Tools, ct);
                var invEnd = DateTimeOffset.UtcNow;

                invocations.Add(new AgentToolInvocation(
                    call.Id, call.Name, call.ArgumentsJson,
                    dispatch.Json, dispatch.IsError,
                    invStart, invEnd));

                await sink.OnToolResultAsync(
                    new AgentToolResultEvent(stepIndex, call.Id, dispatch.Json, dispatch.IsError), ct);

                messages.Add(new AiChatMessage("tool", dispatch.Json, ToolCallId: call.Id));
            }

            var toolStep = new AgentStepEvent(
                stepIndex, AgentStepKind.ToolCall,
                completion.Content, invocations,
                completion.InputTokens, completion.OutputTokens,
                completion.FinishReason, startedAt, DateTimeOffset.UtcNow);
            steps.Add(toolStep);
            await sink.OnStepCompletedAsync(toolStep, ct);

            if (loopBreakTool is not null)
                return await FinalizeAsync(sink, AgentRunStatus.LoopBreak, null,
                    $"Repeated identical tool call: {loopBreakTool}",
                    steps, totalInput, totalOutput, ct);
        }

        return await FinalizeAsync(sink, AgentRunStatus.MaxStepsExceeded, null,
            $"MaxSteps={ctx.MaxSteps} reached",
            steps, totalInput, totalOutput, ct);
    }

    private async Task<AgentRunResult> RunStreamingAsync(
        AgentRunContext ctx,
        IAgentRunSink sink,
        CancellationToken ct)
    {
        var provider = providerFactory.Create(ctx.ModelConfig.Provider);
        var chatOptions = new AiChatOptions(
            Model: ctx.ModelConfig.Model,
            Temperature: ctx.ModelConfig.Temperature,
            MaxTokens: ctx.ModelConfig.MaxTokens,
            SystemPrompt: ctx.SystemPrompt,
            Tools: ctx.Tools.ProviderTools.Count == 0 ? null : ctx.Tools.ProviderTools);

        var messages = new List<AiChatMessage>(ctx.Messages);
        var steps = new List<AgentStepEvent>();
        var detector = new LoopBreakDetector(ctx.LoopBreak);
        var totalInput = 0;
        var totalOutput = 0;
        var priorPromptChars = 0;

        for (var stepIndex = 0; stepIndex < ctx.MaxSteps; stepIndex++)
        {
            if (ct.IsCancellationRequested)
                return await FinalizeAsync(sink, AgentRunStatus.Cancelled, null,
                    "cancelled", steps, totalInput, totalOutput, ct);

            await sink.OnStepStartedAsync(stepIndex, ct);
            var startedAt = DateTimeOffset.UtcNow;

            var currentPromptChars = messages.Sum(m => m.Content?.Length ?? 0);
            var newPromptChars = currentPromptChars - priorPromptChars;
            priorPromptChars = currentPromptChars;

            var content = new StringBuilder();
            var toolBuilders = new Dictionary<string, ToolCallAccumulator>(StringComparer.Ordinal);
            int? roundIn = null, roundOut = null;
            string finishReason = "stop";

            try
            {
                await foreach (var chunk in provider.StreamChatAsync(messages, chatOptions, ct))
                {
                    if (chunk.ContentDelta is { Length: > 0 } d)
                    {
                        content.Append(d);
                        await sink.OnDeltaAsync(d, ct);
                    }
                    if (chunk.ToolCallDelta is { } tc)
                    {
                        if (!toolBuilders.TryGetValue(tc.Id, out var acc))
                        {
                            acc = new ToolCallAccumulator(tc.Id, tc.Name);
                            toolBuilders[tc.Id] = acc;
                        }
                        acc.AppendArguments(tc.ArgumentsJson);
                    }
                    if (chunk.FinishReason is { Length: > 0 } fr) finishReason = fr;
                    if (chunk.InputTokens is int ci && ci > 0) roundIn = ci;
                    if (chunk.OutputTokens is int co && co > 0) roundOut = co;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return await FinalizeAsync(sink, AgentRunStatus.Cancelled, null,
                    "cancelled", steps, totalInput, totalOutput, ct);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Agent runtime streaming provider call failed at step {Step}", stepIndex);
                return await FinalizeAsync(sink, AgentRunStatus.ProviderError, null,
                    ex.Message, steps, totalInput, totalOutput, ct);
            }

            var stepIn = roundIn ?? EstimateTokens(newPromptChars);
            var stepOut = roundOut ?? EstimateTokens(content.Length);
            totalInput += stepIn;
            totalOutput += stepOut;

            var assembledCalls = toolBuilders.Values.Select(a => a.Build()).ToList();
            var roundContent = content.Length == 0 ? null : content.ToString();

            if (assembledCalls.Count == 0)
            {
                var finalStep = new AgentStepEvent(
                    stepIndex, AgentStepKind.Final,
                    roundContent, Array.Empty<AgentToolInvocation>(),
                    stepIn, stepOut, finishReason,
                    startedAt, DateTimeOffset.UtcNow);
                steps.Add(finalStep);

                await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
                    stepIndex, roundContent, Array.Empty<AiToolCall>(), stepIn, stepOut), ct);
                await sink.OnStepCompletedAsync(finalStep, ct);

                return await FinalizeAsync(sink, AgentRunStatus.Completed,
                    roundContent, null, steps, totalInput, totalOutput, ct);
            }

            await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
                stepIndex, roundContent, assembledCalls, stepIn, stepOut), ct);

            messages.Add(new AiChatMessage("assistant", roundContent, ToolCalls: assembledCalls));

            var invocations = new List<AgentToolInvocation>(assembledCalls.Count);
            string? loopBreakTool = null;
            foreach (var call in assembledCalls)
            {
                if (detector.ShouldBreak(call)) { loopBreakTool = call.Name; break; }

                await sink.OnToolCallAsync(new AgentToolCallEvent(stepIndex, call), ct);
                var invStart = DateTimeOffset.UtcNow;
                var dispatch = await toolDispatcher.DispatchAsync(call, ctx.Tools, ct);
                var invEnd = DateTimeOffset.UtcNow;

                invocations.Add(new AgentToolInvocation(
                    call.Id, call.Name, call.ArgumentsJson,
                    dispatch.Json, dispatch.IsError, invStart, invEnd));

                await sink.OnToolResultAsync(new AgentToolResultEvent(
                    stepIndex, call.Id, dispatch.Json, dispatch.IsError), ct);

                messages.Add(new AiChatMessage("tool", dispatch.Json, ToolCallId: call.Id));
            }

            var toolStep = new AgentStepEvent(
                stepIndex, AgentStepKind.ToolCall,
                roundContent, invocations, stepIn, stepOut, finishReason,
                startedAt, DateTimeOffset.UtcNow);
            steps.Add(toolStep);
            await sink.OnStepCompletedAsync(toolStep, ct);

            if (loopBreakTool is not null)
                return await FinalizeAsync(sink, AgentRunStatus.LoopBreak, null,
                    $"Repeated identical tool call: {loopBreakTool}",
                    steps, totalInput, totalOutput, ct);
        }

        return await FinalizeAsync(sink, AgentRunStatus.MaxStepsExceeded, null,
            $"MaxSteps={ctx.MaxSteps} reached",
            steps, totalInput, totalOutput, ct);
    }

    private static async Task<AgentRunResult> FinalizeAsync(
        IAgentRunSink sink,
        AgentRunStatus status,
        string? finalContent,
        string? terminationReason,
        IReadOnlyList<AgentStepEvent> steps,
        int totalInput,
        int totalOutput,
        CancellationToken ct)
    {
        var result = new AgentRunResult(status, finalContent, steps, totalInput, totalOutput, terminationReason);
        await sink.OnRunCompletedAsync(result, ct);
        return result;
    }

    private static int EstimateTokens(int charCount) => Math.Max(1, charCount / 4);

    private sealed class ToolCallAccumulator(string id, string name)
    {
        private readonly StringBuilder _args = new();
        public string Id { get; } = id;
        public string Name { get; } = name;
        public void AppendArguments(string fragment)
        {
            if (!string.IsNullOrEmpty(fragment)) _args.Append(fragment);
        }
        public AiToolCall Build() => new(Id, Name, _args.Length == 0 ? "{}" : _args.ToString());
    }
}
