using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Application.Messages;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.ResendDelivery;

internal sealed class ResendDeliveryCommandHandler(
    CommunicationDbContext context,
    IApplicationDbContext appDb,
    ITemplateEngine templateEngine,
    IIntegrationEventCollector eventCollector,
    ILogger<ResendDeliveryCommandHandler> logger)
    : IRequestHandler<ResendDeliveryCommand, Result<DeliveryLogDto>>
{
    public async Task<Result<DeliveryLogDto>> Handle(
        ResendDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var deliveryLog = await context.DeliveryLogs
            .Include(d => d.Attempts)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (deliveryLog is null)
            return Result.Failure<DeliveryLogDto>(
                new Error("DeliveryLog.NotFound", "Delivery log not found.", ErrorType.NotFound));

        if (deliveryLog.Status is not (DeliveryStatus.Failed or DeliveryStatus.Bounced))
            return Result.Failure<DeliveryLogDto>(
                new Error("DeliveryLog.NotResendable", "Only failed or bounced deliveries can be resent.", ErrorType.Validation));

        var channel = deliveryLog.Channel ?? NotificationChannel.Email;
        var recipientAddress = deliveryLog.RecipientAddress ?? string.Empty;

        // Re-render from template + stored variables instead of using truncated BodyPreview
        string? renderedSubject = deliveryLog.Subject;
        string renderedBody;

        if (deliveryLog.MessageTemplateId.HasValue && !string.IsNullOrWhiteSpace(deliveryLog.VariablesJson))
        {
            var template = await context.MessageTemplates
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == deliveryLog.MessageTemplateId.Value, cancellationToken);

            if (template is not null)
            {
                var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(deliveryLog.VariablesJson) ?? [];

                // Check for tenant override
                var tenantOverride = await context.MessageTemplateOverrides
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(o => o.MessageTemplateId == template.Id
                        && o.TenantId == deliveryLog.TenantId
                        && o.IsActive, cancellationToken);

                var subjectTemplate = tenantOverride?.SubjectTemplate ?? template.SubjectTemplate;
                var bodyTemplate = tenantOverride?.BodyTemplate ?? template.BodyTemplate;

                try
                {
                    renderedSubject = subjectTemplate is not null ? templateEngine.Render(subjectTemplate, variables) : null;
                    renderedBody = templateEngine.Render(bodyTemplate, variables);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to re-render template for resend, using stored preview");
                    renderedBody = deliveryLog.BodyPreview ?? string.Empty;
                }
            }
            else
            {
                renderedBody = deliveryLog.BodyPreview ?? string.Empty;
            }
        }
        else
        {
            renderedBody = deliveryLog.BodyPreview ?? string.Empty;
        }

        var message = new DispatchMessageMessage(
            DeliveryLogId: deliveryLog.Id,
            TenantId: deliveryLog.TenantId,
            RecipientUserId: deliveryLog.RecipientUserId,
            RecipientAddress: recipientAddress,
            TemplateName: deliveryLog.TemplateName,
            RenderedSubject: renderedSubject,
            RenderedBody: renderedBody,
            Channel: channel,
            FallbackChannels: [],
            CurrentFallbackIndex: -1,
            QueuedAt: DateTime.UtcNow);

        deliveryLog.MarkQueued();
        await context.SaveChangesAsync(cancellationToken);

        // Schedule via outbox collector. appDb.SaveChangesAsync flushes the
        // message into ApplicationDbContext's outbox atomically — even though
        // we have no other entity changes on appDb here, the SaveChanges fires
        // IntegrationEventOutboxInterceptor which writes the outbox row.
        eventCollector.Schedule(message);
        await appDb.SaveChangesAsync(cancellationToken);

        var attemptCount = deliveryLog.Attempts.Count;
        return Result.Success(deliveryLog.ToDto(attemptCount));
    }
}
