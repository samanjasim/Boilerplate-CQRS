using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class ChannelConfig : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public ChannelProvider Provider { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string CredentialsJson { get; private set; } = default!;
    public ChannelConfigStatus Status { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime? LastTestedAt { get; private set; }
    public string? LastTestResult { get; private set; }

    private ChannelConfig() { }

    private ChannelConfig(Guid id, Guid tenantId, NotificationChannel channel, ChannelProvider provider,
        string displayName, string credentialsJson, bool isDefault) : base(id)
    {
        TenantId = tenantId;
        Channel = channel;
        Provider = provider;
        DisplayName = displayName;
        CredentialsJson = credentialsJson;
        Status = ChannelConfigStatus.Active;
        IsDefault = isDefault;
    }

    public static ChannelConfig Create(Guid tenantId, NotificationChannel channel, ChannelProvider provider,
        string displayName, string credentialsJson, bool isDefault = false)
    {
        return new ChannelConfig(Guid.NewGuid(), tenantId, channel, provider,
            displayName.Trim(), credentialsJson, isDefault);
    }

    public void Update(string displayName, string credentialsJson)
    {
        DisplayName = displayName.Trim();
        CredentialsJson = credentialsJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = ChannelConfigStatus.Active;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = ChannelConfigStatus.Inactive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void RecordTestResult(bool success, string? result)
    {
        LastTestedAt = DateTime.UtcNow;
        LastTestResult = result;
        if (!success) Status = ChannelConfigStatus.Error;
        ModifiedAt = DateTime.UtcNow;
    }
}
