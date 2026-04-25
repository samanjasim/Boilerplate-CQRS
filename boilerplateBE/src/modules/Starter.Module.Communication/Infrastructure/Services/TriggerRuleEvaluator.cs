using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.Messages;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

internal interface ITriggerRuleEvaluator
{
    Task EvaluateAsync(
        string eventName,
        Guid tenantId,
        Guid? actorUserId,
        Dictionary<string, object> eventData,
        CancellationToken ct = default);
}

internal sealed class TriggerRuleEvaluator(
    CommunicationDbContext dbContext,
    IMessageDispatcher messageDispatcher,
    IIntegrationEventCollector eventCollector,
    ILogger<TriggerRuleEvaluator> logger) : ITriggerRuleEvaluator, ICommunicationEventNotifier
{
    public async Task EvaluateAsync(
        string eventName,
        Guid tenantId,
        Guid? actorUserId,
        Dictionary<string, object> eventData,
        CancellationToken ct = default)
    {
        var rules = await dbContext.TriggerRules
            .IgnoreQueryFilters()
            .Include(r => r.IntegrationTargets)
            .Where(r => r.TenantId == tenantId
                && r.EventName == eventName
                && r.Status == TriggerRuleStatus.Active)
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            logger.LogDebug(
                "No active trigger rules for event {EventName} in tenant {TenantId}",
                eventName, tenantId);
            return;
        }

        logger.LogInformation(
            "Found {Count} trigger rules for event {EventName} in tenant {TenantId}",
            rules.Count, eventName, tenantId);

        foreach (var rule in rules)
        {
            try
            {
                var template = await dbContext.MessageTemplates
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == rule.MessageTemplateId, ct);

                if (template is null)
                {
                    logger.LogWarning(
                        "Template {TemplateId} not found for trigger rule {RuleId}",
                        rule.MessageTemplateId, rule.Id);
                    continue;
                }

                var recipientUserId = ResolveRecipient(rule.RecipientMode, actorUserId, eventData);
                if (!recipientUserId.HasValue)
                {
                    logger.LogWarning(
                        "Could not resolve recipient for rule {RuleId} with mode {RecipientMode}",
                        rule.Id, rule.RecipientMode);
                    continue;
                }

                var deliveryLogId = await messageDispatcher.SendAsync(
                    template.Name,
                    recipientUserId.Value,
                    eventData,
                    tenantId,
                    ct);

                logger.LogInformation(
                    "Dispatched message for rule {RuleName}: template={TemplateName}, recipient={RecipientId}, deliveryLog={DeliveryLogId}",
                    rule.Name, template.Name, recipientUserId.Value, deliveryLogId);

                if (rule.IntegrationTargets.Count > 0)
                {
                    await DispatchIntegrationTargetsAsync(rule, template.Name, eventData, tenantId, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error evaluating trigger rule {RuleId} for event {EventName}",
                    rule.Id, eventName);
            }
        }
    }

    public Task NotifyAsync(
        string eventName, Guid tenantId, Guid? actorUserId,
        Dictionary<string, object> eventData, CancellationToken ct = default)
        => EvaluateAsync(eventName, tenantId, actorUserId, eventData, ct);

    private static Guid? ResolveRecipient(
        string recipientMode,
        Guid? actorUserId,
        Dictionary<string, object> eventData)
    {
        if (recipientMode.Equals("event_user", StringComparison.OrdinalIgnoreCase))
            return actorUserId;

        if (recipientMode.StartsWith("specific:", StringComparison.OrdinalIgnoreCase))
        {
            var userIdStr = recipientMode["specific:".Length..];
            return Guid.TryParse(userIdStr, out var userId) ? userId : null;
        }

        if (eventData.TryGetValue("userId", out var userIdObj))
        {
            if (userIdObj is Guid uid) return uid;
            if (userIdObj is string uidStr && Guid.TryParse(uidStr, out var parsed)) return parsed;
        }

        return actorUserId;
    }

    private async Task DispatchIntegrationTargetsAsync(
        Domain.Entities.TriggerRule rule,
        string templateName,
        Dictionary<string, object> eventData,
        Guid tenantId,
        CancellationToken ct)
    {
        foreach (var target in rule.IntegrationTargets)
        {
            try
            {
                var message = $"[{templateName}] Event triggered: {rule.EventName}\n" +
                    string.Join("\n", eventData.Select(kv => $"  {kv.Key}: {kv.Value}"));

                var deliveryLog = Domain.Entities.DeliveryLog.Create(
                    tenantId, null, null, null, templateName,
                    null, null, null, message, null);

                dbContext.DeliveryLogs.Add(deliveryLog);
                await dbContext.SaveChangesAsync(ct);

                // Scheduled on the request-scoped collector; flushed when the
                // outer ApplicationDbContext.SaveChangesAsync runs the
                // IntegrationEventOutboxInterceptor (this evaluator runs from
                // a MediatR notification handler, which fires during the
                // originating handler's SavingChangesAsync).
                eventCollector.Schedule(new DispatchIntegrationMessage(
                    deliveryLog.Id,
                    tenantId,
                    target.IntegrationConfigId,
                    target.TargetChannelId ?? "",
                    message,
                    DateTime.UtcNow));

                logger.LogInformation(
                    "Dispatched integration message for rule {RuleName} to integration {IntegrationId}",
                    rule.Name, target.IntegrationConfigId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error dispatching integration for rule {RuleId}, target {TargetId}",
                    rule.Id, target.Id);
            }
        }
    }
}
