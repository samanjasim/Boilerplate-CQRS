using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAgentPrincipal : BaseEntity, ITenantEntity
{
    public Guid AiAssistantId { get; private set; }
    public Guid? TenantId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private AiAgentPrincipal() { }

    private AiAgentPrincipal(Guid id, Guid assistantId, Guid tenantId, bool isActive) : base(id)
    {
        AiAssistantId = assistantId;
        TenantId = tenantId;
        IsActive = isActive;
    }

    public static AiAgentPrincipal Create(Guid assistantId, Guid tenantId, bool isActive) =>
        new(Guid.NewGuid(), assistantId, tenantId, isActive);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Revoke()
    {
        IsActive = false;
        RevokedAt = DateTimeOffset.UtcNow;
    }
}
