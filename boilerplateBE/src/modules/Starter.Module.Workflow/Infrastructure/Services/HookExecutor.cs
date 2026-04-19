using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Carries all data a hook might need when executing on-enter/on-exit side effects.
/// </summary>
internal sealed record HookContext(
    Guid InstanceId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid InitiatorUserId,
    string CurrentState,
    string? PreviousState,
    string? Action,
    Guid? ActorUserId,
    Guid? AssigneeUserId,
    string? AssigneeRole,
    string DefinitionName);

/// <summary>
/// Executes on-enter/on-exit side-effect hooks defined on workflow states.
/// Supports hook types: "notify", "activity", "webhook", "inAppNotify".
/// All failures are caught and logged — a failing hook never aborts the workflow.
/// </summary>
internal sealed class HookExecutor(
    IMessageDispatcher messageDispatcher,
    IActivityService activityService,
    IWebhookPublisher webhookPublisher,
    INotificationServiceCapability notificationService,
    IUserReader userReader,
    IConfiguration configuration,
    ILogger<HookExecutor> logger)
{
    public async Task ExecuteAsync(
        List<HookConfig>? hooks,
        HookContext context,
        CancellationToken ct)
    {
        if (hooks is null || hooks.Count == 0) return;

        foreach (var hook in hooks)
        {
            try
            {
                await ExecuteHookAsync(hook, context, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Hook {HookType} failed for workflow {InstanceId}",
                    hook.Type, context.InstanceId);
            }
        }
    }

    private Task ExecuteHookAsync(HookConfig hook, HookContext context, CancellationToken ct)
        => hook.Type.ToLowerInvariant() switch
        {
            "notify"       => ExecuteNotifyAsync(hook, context, ct),
            "activity"     => ExecuteActivityAsync(hook, context, ct),
            "webhook"      => ExecuteWebhookAsync(hook, context, ct),
            "inappnotify"  => ExecuteInAppNotifyAsync(hook, context, ct),
            _ => LogUnknownAndSkip(hook, context),
        };

    // ── notify ────────────────────────────────────────────────────────────────

    private async Task ExecuteNotifyAsync(HookConfig hook, HookContext context, CancellationToken ct)
    {
        var recipientId = ResolveTargetUserId(hook.To, context);
        if (recipientId is null)
        {
            logger.LogDebug(
                "notify hook skipped for instance {InstanceId}: could not resolve target '{To}'",
                context.InstanceId, hook.To);
            return;
        }

        var variables = BuildVariables(context);
        await messageDispatcher.SendAsync(
            hook.Template ?? string.Empty,
            recipientId.Value,
            variables,
            context.TenantId,
            ct);
    }

    // ── activity ──────────────────────────────────────────────────────────────

    private async Task ExecuteActivityAsync(HookConfig hook, HookContext context, CancellationToken ct)
    {
        var metadataJson = JsonSerializer.Serialize(new
        {
            instanceId    = context.InstanceId,
            definitionName = context.DefinitionName,
            currentState  = context.CurrentState,
            previousState = context.PreviousState,
            action        = context.Action,
        });

        await activityService.RecordAsync(
            context.EntityType,
            context.EntityId,
            context.TenantId,
            hook.Action ?? "workflow_transition",
            context.ActorUserId,
            metadataJson,
            ct: ct);
    }

    // ── webhook ───────────────────────────────────────────────────────────────

    private async Task ExecuteWebhookAsync(HookConfig hook, HookContext context, CancellationToken ct)
    {
        var payload = new
        {
            instanceId    = context.InstanceId,
            entityType    = context.EntityType,
            entityId      = context.EntityId,
            definitionName = context.DefinitionName,
            currentState  = context.CurrentState,
            previousState = context.PreviousState,
            action        = context.Action,
        };

        await webhookPublisher.PublishAsync(
            hook.Event ?? "workflow.transition",
            context.TenantId,
            payload,
            ct);
    }

    // ── inAppNotify ───────────────────────────────────────────────────────────

    private async Task ExecuteInAppNotifyAsync(HookConfig hook, HookContext context, CancellationToken ct)
    {
        var recipientId = ResolveTargetUserId(hook.To, context);
        if (recipientId is null)
        {
            logger.LogDebug(
                "inAppNotify hook skipped for instance {InstanceId}: could not resolve target '{To}'",
                context.InstanceId, hook.To);
            return;
        }

        var title = $"Workflow state changed: {context.CurrentState}";
        var message = $"The {context.DefinitionName} workflow moved to '{context.CurrentState}'.";

        await notificationService.CreateAsync(
            recipientId.Value,
            context.TenantId,
            "workflow_state_change",
            title,
            message,
            cancellationToken: ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Guid? ResolveTargetUserId(string? to, HookContext context)
        => to?.ToLowerInvariant() switch
        {
            "assignee"  => context.AssigneeUserId,
            "initiator" => context.InitiatorUserId,
            _           => null,
        };

    private static Dictionary<string, object> BuildVariables(HookContext context)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["instanceId"]     = context.InstanceId,
            ["entityType"]     = context.EntityType,
            ["entityId"]       = context.EntityId,
            ["definitionName"] = context.DefinitionName,
            ["currentState"]   = context.CurrentState,
            ["previousState"]  = context.PreviousState ?? string.Empty,
            ["action"]         = context.Action ?? string.Empty,
        };

    private Task LogUnknownAndSkip(HookConfig hook, HookContext context)
    {
        logger.LogWarning(
            "Unknown hook type '{HookType}' on workflow instance {InstanceId}; skipping.",
            hook.Type, context.InstanceId);
        return Task.CompletedTask;
    }
}
