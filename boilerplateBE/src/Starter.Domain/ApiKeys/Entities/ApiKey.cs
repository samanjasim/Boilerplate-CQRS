using Starter.Domain.Common;

namespace Starter.Domain.ApiKeys.Entities;

public sealed class ApiKey : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string KeyPrefix { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;
    public List<string> Scopes { get; private set; } = [];
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public bool IsRevoked { get; private set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsValid => !IsRevoked && !IsExpired;
    public bool IsPlatformKey => TenantId == null;

    private ApiKey() { }

    private ApiKey(
        Guid id,
        Guid? tenantId,
        string name,
        string keyPrefix,
        string keyHash,
        List<string> scopes,
        DateTime? expiresAt,
        Guid? createdBy) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        Scopes = scopes;
        ExpiresAt = expiresAt;
        CreatedBy = createdBy;
    }

    public static ApiKey Create(
        Guid? tenantId,
        string name,
        string keyPrefix,
        string keyHash,
        List<string> scopes,
        DateTime? expiresAt,
        Guid? createdBy)
    {
        return new ApiKey(
            Guid.NewGuid(),
            tenantId,
            name,
            keyPrefix,
            keyHash,
            scopes,
            expiresAt,
            createdBy);
    }

    public void Revoke()
    {
        IsRevoked = true;
    }

    public void UpdateLastUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string? name, List<string>? scopes)
    {
        if (name is not null) Name = name;
        if (scopes is not null) Scopes = scopes;
    }
}
