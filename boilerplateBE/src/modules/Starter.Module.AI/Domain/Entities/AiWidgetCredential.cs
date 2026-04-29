using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiWidgetCredential : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid WidgetId { get; private set; }
    public string KeyPrefix { get; private set; } = default!;
    public string KeyHash { get; private set; } = default!;
    public AiWidgetCredentialStatus Status { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiWidgetCredential() { }

    private AiWidgetCredential(
        Guid tenantId,
        Guid widgetId,
        string keyPrefix,
        string keyHash,
        DateTimeOffset? expiresAt,
        Guid? createdByUserId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        WidgetId = widgetId;
        KeyPrefix = keyPrefix.Trim();
        KeyHash = keyHash;
        Status = AiWidgetCredentialStatus.Active;
        ExpiresAt = expiresAt;
        CreatedByUserId = createdByUserId;
    }

    public static AiWidgetCredential Create(
        Guid tenantId,
        Guid widgetId,
        string keyPrefix,
        string keyHash,
        DateTimeOffset? expiresAt,
        Guid? createdByUserId) =>
        new(tenantId, widgetId, keyPrefix, keyHash, expiresAt, createdByUserId);

    public void Revoke()
    {
        Status = AiWidgetCredentialStatus.Revoked;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
