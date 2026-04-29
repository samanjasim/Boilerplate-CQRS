using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Decorator over <see cref="IAiAgentRuntime"/> that enforces per-agent cost caps and
/// rate limits before delegating, then reconciles actual cost after the run completes.
/// Pre-step flow:
///   1. Resolve effective caps (plan ceilings ∧ per-agent overrides).
///   2. Estimate worst-case cost (input tokens × input rate + max_tokens × output rate).
///   3. Atomically claim against monthly + daily windows; refuse with CostCapExceeded if either fails.
///   4. Acquire a sliding-window rate-limit slot; refuse with RateLimitExceeded if exceeded.
///   5. Delegate to inner runtime.
///   6. Reconcile delta = actual - estimated against both windows.
/// Skips enforcement entirely when the run context has no AssistantId or TenantId
/// (legacy callers / tests that don't carry agent identity).
/// </summary>
internal sealed class CostCapEnforcingAgentRuntime(
    IAiAgentRuntime inner,
    ICostCapResolver caps,
    ICostCapAccountant accountant,
    IAgentRateLimiter rateLimiter,
    IModelPricingService pricing,
    ILogger<CostCapEnforcingAgentRuntime> logger) : IAiAgentRuntime
{
    public async Task<AgentRunResult> RunAsync(
        AgentRunContext ctx,
        IAgentRunSink sink,
        CancellationToken ct = default)
    {
        if (ctx.AssistantId is not { } assistantId || ctx.TenantId is not { } tenantId)
        {
            // Legacy / test path with no agent identity: skip enforcement.
            return await inner.RunAsync(ctx, sink, ct);
        }

        EffectiveCaps effective;
        try
        {
            effective = await caps.ResolveAsync(tenantId, assistantId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve cost caps for assistant {AssistantId}; failing closed.", assistantId);
            return new AgentRunResult(
                Status: AgentRunStatus.ProviderError,
                FinalContent: null,
                Steps: Array.Empty<AgentStepEvent>(),
                TotalInputTokens: 0,
                TotalOutputTokens: 0,
                TerminationReason: "Cost-cap resolver unavailable.");
        }

        decimal estimated;
        try
        {
            // Worst-case estimate: actual input tokens (counted in messages) plus max output tokens.
            var estimatedInputTokens = EstimateInputTokens(ctx);
            estimated = await pricing.EstimateCostAsync(
                ctx.ModelConfig.Provider,
                ctx.ModelConfig.Model,
                inputTokens: estimatedInputTokens,
                outputTokens: ctx.ModelConfig.MaxTokens,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            // No pricing row for this (provider, model). In production this means a
            // misconfiguration that the superadmin must address; in test/dev fixtures it
            // typically means the AiModelPricing seed didn't run. Either way, cap
            // enforcement cannot proceed — log and skip so the agent can still execute.
            // Reconciliation skips the recordactual step too. The reconciliation job (N1)
            // will surface unpriced runs via the `ai_unpriced_run` metric.
            logger.LogWarning(ex,
                "No pricing for {Provider}/{Model}; cost-cap enforcement skipped for this run.",
                ctx.ModelConfig.Provider, ctx.ModelConfig.Model);
            return await inner.RunAsync(ctx, sink, ct);
        }

        var claimed = new List<(CapWindow Window, CostCapBucket Bucket)>();

        var monthly = await ClaimAsync(CapWindow.Monthly, effective.MonthlyUsd, CostCapBucket.Total, "monthly");
        if (monthly is not null)
            return monthly;

        var daily = await ClaimAsync(CapWindow.Daily, effective.DailyUsd, CostCapBucket.Total, "daily");
        if (daily is not null)
            return daily;

        if (ctx.ProviderCredentialSource == ProviderCredentialSource.Platform)
        {
            var platformMonthly = await ClaimAsync(
                CapWindow.Monthly,
                effective.PlatformMonthlyUsd,
                CostCapBucket.PlatformCredit,
                "platform monthly");
            if (platformMonthly is not null)
                return platformMonthly;

            var platformDaily = await ClaimAsync(
                CapWindow.Daily,
                effective.PlatformDailyUsd,
                CostCapBucket.PlatformCredit,
                "platform daily");
            if (platformDaily is not null)
                return platformDaily;
        }

        // Rate limit
        if (!await rateLimiter.TryAcquireAsync(assistantId, effective.Rpm, ct))
        {
            foreach (var claim in claimed)
            {
                await accountant.RollbackClaimAsync(tenantId, assistantId, estimated, claim.Window, claim.Bucket, ct);
            }

            return RateLimitExceededResult(effective.Rpm);
        }

        var result = await inner.RunAsync(ctx, sink, ct);

        // Reconcile actual vs estimated.
        try
        {
            var actual = await pricing.EstimateCostAsync(
                ctx.ModelConfig.Provider, ctx.ModelConfig.Model,
                inputTokens: (int)Math.Min(result.TotalInputTokens, int.MaxValue),
                outputTokens: (int)Math.Min(result.TotalOutputTokens, int.MaxValue),
                ct);
            var delta = actual - estimated;
            if (delta != 0m)
            {
                foreach (var claim in claimed)
                {
                    await accountant.RecordActualAsync(tenantId, assistantId, delta, claim.Window, claim.Bucket, ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Cost reconciliation failed for assistant {AssistantId}; nightly job will correct drift.",
                assistantId);
        }

        return result;

        async Task<AgentRunResult?> ClaimAsync(
            CapWindow window,
            decimal capUsd,
            CostCapBucket bucket,
            string tier)
        {
            var claim = await accountant.TryClaimAsync(tenantId, assistantId, estimated, window, capUsd, bucket, ct);
            if (claim.Granted)
            {
                claimed.Add((window, bucket));
                return null;
            }

            foreach (var previous in claimed)
            {
                await accountant.RollbackClaimAsync(tenantId, assistantId, estimated, previous.Window, previous.Bucket, ct);
            }

            return CostCapExceededResult(tier, AiAgentErrors.CostCapExceeded(tier, claim.CapUsd, claim.CurrentUsd));
        }
    }

    private static int EstimateInputTokens(AgentRunContext ctx)
    {
        // Conservative char/4 heuristic; replaced with a proper tokenizer in a follow-up.
        // Worst-case input is system prompt + all messages.
        var totalChars = ctx.SystemPrompt.Length;
        foreach (var msg in ctx.Messages)
            totalChars += (msg.Content?.Length ?? 0) + msg.Role.Length;
        return Math.Max(1, totalChars / 4);
    }

    private static AgentRunResult CostCapExceededResult(string tier, Error error) =>
        new(
            Status: AgentRunStatus.CostCapExceeded,
            FinalContent: null,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 0,
            TotalOutputTokens: 0,
            TerminationReason: error.Description);

    private static AgentRunResult RateLimitExceededResult(int rpm) =>
        new(
            Status: AgentRunStatus.RateLimitExceeded,
            FinalContent: null,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 0,
            TotalOutputTokens: 0,
            TerminationReason: AiAgentErrors.RateLimitExceeded(rpm).Description);
}
