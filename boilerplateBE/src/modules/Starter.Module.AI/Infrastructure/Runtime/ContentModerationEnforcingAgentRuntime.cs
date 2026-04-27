using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime.Moderation;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Outermost decorator over <see cref="IAiAgentRuntime"/>. Scans user input pre-flight
/// (refuses before delegating to the inner cost-cap layer if blocked), then either
/// streams output through (Standard) or buffers it for a post-run scan (ChildSafe /
/// ProfessionalModerated). Collects one <see cref="AiModerationEvent"/> per non-Allowed
/// outcome on the result so the chat layer can persist them in its own atomic write.
/// Skips moderation entirely when the run context has no AssistantId or TenantId
/// (legacy callers / tests that don't carry agent identity).
/// </summary>
internal sealed class ContentModerationEnforcingAgentRuntime(
    IAiAgentRuntime inner,
    IContentModerator moderator,
    IPiiRedactor redactor,
    ISafetyProfileResolver profileResolver,
    IModerationRefusalProvider refusals,
    AiDbContext db,
    IWebhookPublisher webhooks,
    ILogger<ContentModerationEnforcingAgentRuntime> logger) : IAiAgentRuntime
{
    public async Task<AgentRunResult> RunAsync(
        AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
    {
        if (ctx.AssistantId is not { } assistantId || ctx.TenantId is not { } tenantId)
            return await inner.RunAsync(ctx, sink, ct);

        var assistant = await db.AiAssistants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assistantId, ct);
        if (assistant is null)
        {
            logger.LogWarning(
                "Assistant {AssistantId} not found in moderation decorator; bypassing moderation. " +
                "This usually means a deleted/foreign assistant slipped through resource ACL — investigate.",
                assistantId);
            return await inner.RunAsync(ctx, sink, ct);
        }

        var profile = await profileResolver.ResolveAsync(
            tenantId, assistant, ctx.Persona?.Safety,
            ModerationProvider.OpenAi, ct);

        // The decorator collects moderation events to be persisted by the chat layer in its
        // own transaction. We do NOT call db.SaveChangesAsync here — the chat layer's
        // FinalizeTurnAsync owns the unit of work. This keeps the user message + assistant
        // message + moderation events + usage log in a single atomic write.
        var moderationEvents = new List<AiModerationEvent>();

        // 1. Input scan (last user message)
        var inputText = ctx.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        var inputVerdict = await moderator.ScanAsync(
            inputText, ModerationStage.Input, profile, ctx.Persona?.Slug, ct);

        AiAgentMetrics.ModerationLatency.Record(inputVerdict.LatencyMs,
            new KeyValuePair<string, object?>("ai.moderation.stage", "input"),
            new KeyValuePair<string, object?>("ai.moderation.provider", profile.Provider.ToString()));

        if (HandleUnavailable(inputVerdict, profile, ModerationStage.Input) is { } unavailableInput)
            return unavailableInput;

        if (inputVerdict.Outcome == ModerationOutcome.Blocked)
            return BuildBlockedResult(
                assistant, profile, ModerationStage.Input, inputVerdict, ctx,
                moderationEvents, innerInputTokens: 0, innerOutputTokens: 0);

        // 2. Choose sink wrapper based on preset (Standard streams live; safe presets buffer)
        var bufferingSink = profile.Preset == SafetyPreset.Standard
            ? null
            : new BufferingSink(sink);
        var wrappedSink = bufferingSink ?? (IAgentRunSink)new PassthroughSink(sink);

        var innerResult = await inner.RunAsync(ctx, wrappedSink, ct);
        if (innerResult.Status != AgentRunStatus.Completed)
            return innerResult;

        var outputText = bufferingSink?.BufferedContent ?? innerResult.FinalContent ?? string.Empty;

        // 3. Output scan
        var outputVerdict = await moderator.ScanAsync(
            outputText, ModerationStage.Output, profile, ctx.Persona?.Slug, ct);

        AiAgentMetrics.ModerationLatency.Record(outputVerdict.LatencyMs,
            new KeyValuePair<string, object?>("ai.moderation.stage", "output"),
            new KeyValuePair<string, object?>("ai.moderation.provider", profile.Provider.ToString()));

        if (HandleUnavailable(outputVerdict, profile, ModerationStage.Output) is { } unavailableOut)
            return unavailableOut;

        if (outputVerdict.Outcome == ModerationOutcome.Blocked)
            return BuildBlockedResult(
                assistant, profile, ModerationStage.Output, outputVerdict, ctx,
                moderationEvents,
                innerInputTokens: innerResult.TotalInputTokens,
                innerOutputTokens: innerResult.TotalOutputTokens);

        // 4. PII redaction (ProfessionalModerated). The redactor is a no-op when
        // profile.RedactPii is false, so this is safe to call on every preset.
        var redaction = await redactor.RedactAsync(outputText, profile, ct);
        if (redaction.Outcome == ModerationOutcome.Redacted)
        {
            moderationEvents.Add(AiModerationEvent.Create(
                tenantId: assistant.TenantId,
                assistantId: assistant.Id,
                agentPrincipalId: null,
                conversationId: null, // chat layer can backfill via the join key if it cares
                agentTaskId: null,
                messageId: null,
                stage: ModerationStage.Output,
                preset: profile.Preset,
                outcome: ModerationOutcome.Redacted,
                categoriesJson: JsonSerializer.Serialize(
                    redaction.Hits.ToDictionary(kv => kv.Key, kv => (double)kv.Value)),
                provider: profile.Provider,
                latencyMs: outputVerdict.LatencyMs,
                redactionFailed: redaction.Failed));
            AiAgentMetrics.ModerationOutcomes.Add(1,
                new KeyValuePair<string, object?>("ai.moderation.outcome", "redacted"),
                new KeyValuePair<string, object?>("ai.moderation.preset", profile.Preset.ToString()));
        }

        var finalText = redaction.Outcome == ModerationOutcome.Redacted ? redaction.Text : outputText;

        // 5. Release buffered content if we suppressed streaming
        if (bufferingSink is not null)
            await bufferingSink.ReleaseAsync(finalText, ct);

        return innerResult with
        {
            FinalContent = finalText,
            ModerationEvents = moderationEvents.Count == 0 ? null : moderationEvents
        };
    }

    private AgentRunResult? HandleUnavailable(
        ModerationVerdict verdict, ResolvedSafetyProfile profile, ModerationStage stage)
    {
        if (!verdict.ProviderUnavailable) return null;
        AiAgentMetrics.ModerationProviderUnavailable.Add(1,
            new KeyValuePair<string, object?>("ai.moderation.failure_mode", profile.FailureMode.ToString()),
            new KeyValuePair<string, object?>("ai.moderation.preset", profile.Preset.ToString()));
        if (profile.FailureMode == ModerationFailureMode.FailOpen)
        {
            logger.LogWarning("Moderation provider unavailable; FailOpen on preset {Preset} stage {Stage} — allowing.",
                profile.Preset, stage);
            return null;
        }
        var refusal = refusals.GetProviderUnavailable(profile.Preset, CultureInfo.CurrentUICulture);
        return new AgentRunResult(
            Status: AgentRunStatus.ModerationProviderUnavailable,
            FinalContent: refusal,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 0,
            TotalOutputTokens: 0,
            TerminationReason: AiModerationErrors.ProviderUnavailable.Description);
    }

    private AgentRunResult BuildBlockedResult(
        AiAssistant assistant, ResolvedSafetyProfile profile,
        ModerationStage stage, ModerationVerdict verdict, AgentRunContext ctx,
        List<AiModerationEvent> moderationEvents,
        long innerInputTokens, long innerOutputTokens)
    {
        moderationEvents.Add(AiModerationEvent.Create(
            tenantId: assistant.TenantId,
            assistantId: assistant.Id,
            agentPrincipalId: null,
            conversationId: null,
            agentTaskId: null,
            messageId: null,
            stage: stage,
            preset: profile.Preset,
            outcome: ModerationOutcome.Blocked,
            categoriesJson: JsonSerializer.Serialize(verdict.Categories),
            provider: profile.Provider,
            latencyMs: verdict.LatencyMs,
            blockedReason: verdict.BlockedReason));

        AiAgentMetrics.ModerationOutcomes.Add(1,
            new KeyValuePair<string, object?>("ai.moderation.outcome", "blocked"),
            new KeyValuePair<string, object?>("ai.moderation.preset", profile.Preset.ToString()),
            new KeyValuePair<string, object?>("ai.moderation.stage", stage.ToString()));

        // Webhook fan-out (best-effort; failures are logged and swallowed so we never
        // turn a moderation block into a request error). Detached so a slow webhook
        // dispatcher never gates the user-visible refusal latency.
        _ = Task.Run(async () =>
        {
            try
            {
                await webhooks.PublishAsync("ai.moderation.blocked", assistant.TenantId, new
                {
                    assistant.TenantId,
                    AssistantId = assistant.Id,
                    Stage = stage,
                    Preset = profile.Preset,
                    Categories = verdict.Categories,
                    Reason = verdict.BlockedReason
                }, default);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ai.moderation.blocked webhook publish failed.");
            }
        });

        var audience = ctx.Persona?.Audience ?? PersonaAudienceType.Internal;
        var refusal = refusals.GetRefusal(profile.Preset, audience, CultureInfo.CurrentUICulture);
        var status = stage == ModerationStage.Input ? AgentRunStatus.InputBlocked : AgentRunStatus.OutputBlocked;

        // Token accounting on blocked turns:
        //   * Input stage: no LLM call happened, so tokens are zero.
        //   * Output stage: the inner runtime already consumed tokens — preserve them so
        //     the chat layer's usage log records actual spend (the user is still billed
        //     for the model call even though we suppressed the unsafe content).
        return new AgentRunResult(
            Status: status,
            FinalContent: refusal,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: innerInputTokens,
            TotalOutputTokens: innerOutputTokens,
            TerminationReason: $"moderation: {verdict.BlockedReason ?? "blocked"}",
            ModerationEvents: moderationEvents);
    }
}
