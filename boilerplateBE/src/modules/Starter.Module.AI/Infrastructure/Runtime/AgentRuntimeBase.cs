using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Persistence;
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
    AiDbContext aiDb,
    IAgentPermissionResolver agentPermissions,
    ILogger<AgentRuntimeBase> logger) : IAiAgentRuntime
{
    protected ILogger<AgentRuntimeBase> Logger { get; } = logger;

    public virtual async Task<AgentRunResult> RunAsync(
        AgentRunContext ctx,
        IAgentRunSink sink,
        CancellationToken ct = default)
    {
        using var activity = AiAgentMetrics.Source.StartActivity("ai.agent.run");
        activity?.SetTag("ai.provider", ctx.ModelConfig.Provider.ToString());
        activity?.SetTag("ai.model", ctx.ModelConfig.Model);
        activity?.SetTag("ai.max_steps", ctx.MaxSteps);
        activity?.SetTag("ai.streaming", ctx.Streaming);

        // Plan 5d-1: install AgentExecutionScope so AgentToolDispatcher (and any audit
        // writes inside the run) sees hybrid-intersection permissions. When AssistantId is
        // null (legacy/test callers without a paired principal), skip the scope and fall
        // back to the default IExecutionContext behaviour.
        AgentExecutionScope? scope = null;
        if (ctx.AssistantId is { } assistantId)
        {
            var principal = await aiDb.AiAgentPrincipals
                .AsNoTracking()
                .Where(p => p.AiAssistantId == assistantId && p.IsActive)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (principal != Guid.Empty)
            {
                var agentPerms = await agentPermissions.GetPermissionsAsync(principal, ct);
                scope = AgentExecutionScope.Begin(
                    userId: ctx.CallerUserId,
                    agentPrincipalId: principal,
                    tenantId: ctx.TenantId,
                    callerHasPermission: ctx.CallerHasPermission,
                    agentHasPermission: agentPerms.Contains);
                scope.AttachRunId(activity?.Id is { } id && Guid.TryParse(id, out var runId)
                    ? runId
                    : Guid.NewGuid());
            }
        }

        try
        {
            var result = ctx.Streaming
                ? await RunStreamingAsync(ctx, sink, ct)
                : await RunNonStreamingAsync(ctx, sink, ct);
            return PostProcess(ctx, activity, result);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static AgentRunResult PostProcess(
        AgentRunContext ctx,
        System.Diagnostics.Activity? activity,
        AgentRunResult result)
    {

        activity?.SetTag("ai.run_status", result.Status.ToString());
        activity?.SetTag("ai.step_count", result.Steps.Count);
        activity?.SetTag("ai.input_tokens", result.TotalInputTokens);
        activity?.SetTag("ai.output_tokens", result.TotalOutputTokens);

        AiAgentMetrics.StepCount.Record(result.Steps.Count,
            new KeyValuePair<string, object?>("provider", ctx.ModelConfig.Provider.ToString()),
            new KeyValuePair<string, object?>("status", result.Status.ToString()));

        if (result.Status == AgentRunStatus.LoopBreak)
        {
            // TerminationReason format: "Repeated identical tool call: {toolName}"
            var toolName = result.TerminationReason is { Length: > 0 } reason && reason.Contains(':')
                ? reason[(reason.IndexOf(':') + 1)..].Trim()
                : "unknown";
            AiAgentMetrics.LoopBreaks.Add(1,
                new KeyValuePair<string, object?>("provider", ctx.ModelConfig.Provider.ToString()),
                new KeyValuePair<string, object?>("tool_name", toolName));
        }

        if (result.Status == AgentRunStatus.MaxStepsExceeded)
            AiAgentMetrics.MaxStepsExceeded.Add(1,
                new KeyValuePair<string, object?>("provider", ctx.ModelConfig.Provider.ToString()));

        return result;
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
        long totalInput = 0;
        long totalOutput = 0;

        for (var stepIndex = 0; stepIndex < ctx.MaxSteps; stepIndex++)
        {
            if (ct.IsCancellationRequested)
                return await FinalizeAsync(sink, AgentRunStatus.Cancelled, null,
                    "cancelled", steps, totalInput, totalOutput, ct);

            await sink.OnStepStartedAsync(stepIndex, ct);
            var startedAt = DateTimeOffset.UtcNow;

            using var stepActivity = AiAgentMetrics.Source.StartActivity("ai.agent.step");
            stepActivity?.SetTag("step.index", stepIndex);

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

                stepActivity?.SetTag("step.kind", AgentStepKind.Final.ToString());
                stepActivity?.SetTag("step.input_tokens", completion.InputTokens);
                stepActivity?.SetTag("step.output_tokens", completion.OutputTokens);
                stepActivity?.SetTag("step.tool_count", 0);

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

            stepActivity?.SetTag("step.kind", AgentStepKind.ToolCall.ToString());
            stepActivity?.SetTag("step.input_tokens", completion.InputTokens);
            stepActivity?.SetTag("step.output_tokens", completion.OutputTokens);
            stepActivity?.SetTag("step.tool_count", toolCalls.Count);

            if (loopBreakTool is not null)
            {
                Logger.LogInformation(
                    "Agent run terminated {Status} step={StepIndex} tool={ToolName} steps={StepCount}",
                    AgentRunStatus.LoopBreak, stepIndex, loopBreakTool, steps.Count);
                return await FinalizeAsync(sink, AgentRunStatus.LoopBreak, null,
                    $"Repeated identical tool call: {loopBreakTool}",
                    steps, totalInput, totalOutput, ct);
            }
        }

        Logger.LogInformation(
            "Agent run terminated {Status} max_steps={MaxSteps} steps={StepCount}",
            AgentRunStatus.MaxStepsExceeded, ctx.MaxSteps, steps.Count);
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
        long totalInput = 0;
        long totalOutput = 0;
        var priorPromptChars = 0;

        for (var stepIndex = 0; stepIndex < ctx.MaxSteps; stepIndex++)
        {
            if (ct.IsCancellationRequested)
                return await FinalizeAsync(sink, AgentRunStatus.Cancelled, null,
                    "cancelled", steps, totalInput, totalOutput, ct);

            await sink.OnStepStartedAsync(stepIndex, ct);
            var startedAt = DateTimeOffset.UtcNow;

            using var stepActivity = AiAgentMetrics.Source.StartActivity("ai.agent.step");
            stepActivity?.SetTag("step.index", stepIndex);

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

                stepActivity?.SetTag("step.kind", AgentStepKind.Final.ToString());
                stepActivity?.SetTag("step.input_tokens", stepIn);
                stepActivity?.SetTag("step.output_tokens", stepOut);
                stepActivity?.SetTag("step.tool_count", 0);

                await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
                    stepIndex, roundContent, Array.Empty<AiToolCall>(), stepIn, stepOut), ct);
                await sink.OnStepCompletedAsync(finalStep, ct);

                return await FinalizeAsync(sink, AgentRunStatus.Completed,
                    roundContent, null, steps, totalInput, totalOutput, ct);
            }

            await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
                stepIndex, roundContent, assembledCalls, roundIn ?? 0, roundOut ?? 0), ct);

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

            stepActivity?.SetTag("step.kind", AgentStepKind.ToolCall.ToString());
            stepActivity?.SetTag("step.input_tokens", stepIn);
            stepActivity?.SetTag("step.output_tokens", stepOut);
            stepActivity?.SetTag("step.tool_count", assembledCalls.Count);

            if (loopBreakTool is not null)
            {
                Logger.LogInformation(
                    "Agent run terminated {Status} step={StepIndex} tool={ToolName} steps={StepCount}",
                    AgentRunStatus.LoopBreak, stepIndex, loopBreakTool, steps.Count);
                return await FinalizeAsync(sink, AgentRunStatus.LoopBreak, null,
                    $"Repeated identical tool call: {loopBreakTool}",
                    steps, totalInput, totalOutput, ct);
            }
        }

        Logger.LogInformation(
            "Agent run terminated {Status} max_steps={MaxSteps} steps={StepCount}",
            AgentRunStatus.MaxStepsExceeded, ctx.MaxSteps, steps.Count);
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
        long totalInput,
        long totalOutput,
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
