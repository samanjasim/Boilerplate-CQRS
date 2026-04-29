using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiProviderCredential : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public AiProviderType Provider { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string EncryptedSecret { get; private set; } = default!;
    public string KeyPrefix { get; private set; } = default!;
    public ProviderCredentialStatus Status { get; private set; }
    public DateTimeOffset? LastValidatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiProviderCredential() { }

    private AiProviderCredential(
        Guid tenantId,
        AiProviderType provider,
        string displayName,
        string encryptedSecret,
        string keyPrefix,
        Guid? createdByUserId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        Provider = provider;
        DisplayName = displayName.Trim();
        EncryptedSecret = encryptedSecret;
        KeyPrefix = keyPrefix.Trim();
        Status = ProviderCredentialStatus.Active;
        CreatedByUserId = createdByUserId;
    }

    public static AiProviderCredential Create(
        Guid tenantId,
        AiProviderType provider,
        string displayName,
        string encryptedSecret,
        string keyPrefix,
        Guid? createdByUserId) =>
        new(tenantId, provider, displayName, encryptedSecret, keyPrefix, createdByUserId);

    public void Revoke()
    {
        Status = ProviderCredentialStatus.Revoked;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkValidated()
    {
        LastValidatedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
