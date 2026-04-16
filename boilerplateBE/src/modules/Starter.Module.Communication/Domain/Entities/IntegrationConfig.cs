using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class IntegrationConfig : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public IntegrationType IntegrationType { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string CredentialsJson { get; private set; } = default!;
    public string? ChannelMappingsJson { get; private set; }
    public IntegrationConfigStatus Status { get; private set; }
    public DateTime? LastTestedAt { get; private set; }
    public string? LastTestResult { get; private set; }

    private IntegrationConfig() { }

    private IntegrationConfig(Guid id, Guid tenantId, IntegrationType integrationType,
        string displayName, string credentialsJson) : base(id)
    {
        TenantId = tenantId;
        IntegrationType = integrationType;
        DisplayName = displayName;
        CredentialsJson = credentialsJson;
        Status = IntegrationConfigStatus.Active;
    }

    public static IntegrationConfig Create(Guid tenantId, IntegrationType integrationType,
        string displayName, string credentialsJson)
    {
        return new IntegrationConfig(Guid.NewGuid(), tenantId, integrationType,
            displayName.Trim(), credentialsJson);
    }

    public void Update(string displayName, string credentialsJson, string? channelMappingsJson)
    {
        DisplayName = displayName.Trim();
        CredentialsJson = credentialsJson;
        ChannelMappingsJson = channelMappingsJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate() { Status = IntegrationConfigStatus.Active; ModifiedAt = DateTime.UtcNow; }
    public void Deactivate() { Status = IntegrationConfigStatus.Inactive; ModifiedAt = DateTime.UtcNow; }

    public void RecordTestResult(bool success, string? result)
    {
        LastTestedAt = DateTime.UtcNow;
        LastTestResult = result;
        if (!success) Status = IntegrationConfigStatus.Error;
        ModifiedAt = DateTime.UtcNow;
    }
}
