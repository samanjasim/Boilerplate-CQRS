using Starter.Domain.Common.Enums;

namespace Starter.Domain.Common;

public class AuditLog : ITenantEntity
{
    public Guid Id { get; set; }
    public AuditEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? Changes { get; set; }
    public Guid? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? TenantId { get; set; }

    // Dual-attribution: populated when an agent acts on behalf of a human user.
    public Guid? OnBehalfOfUserId { get; set; }
    public Guid? AgentPrincipalId { get; set; }
    public Guid? AgentRunId { get; set; }
}
