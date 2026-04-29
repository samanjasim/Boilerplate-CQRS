using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Module.AI.Application.Events;

namespace Starter.Module.AI.Infrastructure.Consumers;

/// <summary>
/// Writes <c>AuditLog</c> rows in <c>ApplicationDbContext</c> when a provider-credential
/// mutation event arrives via MassTransit's transactional outbox. Replaces the previous
/// in-line <c>coreDb.SaveChangesAsync</c> path so transient core-DB failures retry under
/// MT's policy (3 attempts at 1 s / 5 s / 15 s, then dead-letter).
///
/// <para>Idempotent by <c>(EntityId, Action, PerformedAt)</c> — at-least-once delivery
/// can replay a message; we skip the second write rather than create duplicate audit
/// rows. <c>OccurredAt</c> is set once by the producing handler so retries collide on
/// the timestamp.</para>
/// </summary>
public sealed class AiProviderCredentialAuditConsumer(IServiceScopeFactory scopeFactory)
    : IConsumer<AiProviderCredentialCreatedEvent>,
      IConsumer<AiProviderCredentialRotatedEvent>,
      IConsumer<AiProviderCredentialRevokedEvent>,
      IConsumer<AiProviderCredentialTestedEvent>
{
    public Task Consume(ConsumeContext<AiProviderCredentialCreatedEvent> context) =>
        WriteAsync(
            context.Message.TenantId,
            context.Message.CredentialId,
            context.Message.Provider.ToString(),
            context.Message.KeyPrefix,
            context.Message.PerformedBy,
            context.Message.PerformedByEmail,
            context.Message.OccurredAt,
            actionCode: "AiProviderCredential.Created",
            action: AuditAction.Created,
            context.CancellationToken);

    public Task Consume(ConsumeContext<AiProviderCredentialRotatedEvent> context) =>
        WriteAsync(
            context.Message.TenantId,
            context.Message.CredentialId,
            context.Message.Provider.ToString(),
            context.Message.KeyPrefix,
            context.Message.PerformedBy,
            context.Message.PerformedByEmail,
            context.Message.OccurredAt,
            actionCode: "AiProviderCredential.Rotated",
            action: AuditAction.Updated,
            context.CancellationToken);

    public Task Consume(ConsumeContext<AiProviderCredentialRevokedEvent> context) =>
        WriteAsync(
            context.Message.TenantId,
            context.Message.CredentialId,
            context.Message.Provider.ToString(),
            context.Message.KeyPrefix,
            context.Message.PerformedBy,
            context.Message.PerformedByEmail,
            context.Message.OccurredAt,
            actionCode: "AiProviderCredential.Revoked",
            action: AuditAction.Deleted,
            context.CancellationToken);

    public Task Consume(ConsumeContext<AiProviderCredentialTestedEvent> context) =>
        WriteAsync(
            context.Message.TenantId,
            context.Message.CredentialId,
            context.Message.Provider.ToString(),
            context.Message.KeyPrefix,
            context.Message.PerformedBy,
            context.Message.PerformedByEmail,
            context.Message.OccurredAt,
            actionCode: "AiProviderCredential.Tested",
            action: AuditAction.Updated,
            context.CancellationToken);

    private async Task WriteAsync(
        Guid tenantId,
        Guid credentialId,
        string provider,
        string keyPrefix,
        Guid? performedBy,
        string? performedByEmail,
        DateTime occurredAt,
        string actionCode,
        AuditAction action,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        // Idempotency: skip if a row with the same (entity, action, occurredAt) already
        // exists. At-least-once delivery means a redelivery after a transient failure
        // can land here a second time; the audit table is append-only so we de-dupe by
        // the producer-stamped timestamp rather than rely on the consumer reaching the
        // DB exactly once.
        var alreadyWritten = await appDb.AuditLogs
            .IgnoreQueryFilters()
            .AnyAsync(a => a.EntityId == credentialId
                && a.Action == action
                && a.PerformedAt == occurredAt, ct);
        if (alreadyWritten)
            return;

        appDb.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.AiProviderCredential,
            EntityId = credentialId,
            Action = action,
            Changes = JsonSerializer.Serialize(new
            {
                actionCode,
                Id = credentialId,
                Provider = provider,
                KeyPrefix = keyPrefix
            }),
            PerformedBy = performedBy,
            PerformedByName = performedByEmail,
            PerformedAt = occurredAt,
            TenantId = tenantId
        });

        await appDb.SaveChangesAsync(ct);
    }
}
