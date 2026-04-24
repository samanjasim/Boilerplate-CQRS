namespace Starter.Module.AI.Domain.Entities;

public sealed class UserPersona
{
    public Guid UserId { get; private set; }
    public Guid PersonaId { get; private set; }
    public Guid TenantId { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    public AiPersona Persona { get; private set; } = null!;

    private UserPersona() { }

    private UserPersona(
        Guid userId,
        Guid personaId,
        Guid tenantId,
        bool isDefault,
        Guid? assignedBy)
    {
        UserId = userId;
        PersonaId = personaId;
        TenantId = tenantId;
        IsDefault = isDefault;
        AssignedBy = assignedBy;
        AssignedAt = DateTime.UtcNow;
    }

    public static UserPersona Create(
        Guid userId,
        Guid personaId,
        Guid tenantId,
        bool isDefault,
        Guid? assignedBy) =>
        new(userId, personaId, tenantId, isDefault, assignedBy);

    public void MakeDefault() => IsDefault = true;
    public void ClearDefault() => IsDefault = false;
}
