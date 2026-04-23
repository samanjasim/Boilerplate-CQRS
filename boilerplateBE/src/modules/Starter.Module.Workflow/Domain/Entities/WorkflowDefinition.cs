using Starter.Domain.Common;

namespace Starter.Module.Workflow.Domain.Entities;

public sealed class WorkflowDefinition : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public string EntityType { get; private set; } = default!;
    public bool IsTemplate { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? SourceDefinitionId { get; private set; }
    public string? SourceModule { get; private set; }
    public int Version { get; private set; }
    public string StatesJson { get; private set; } = default!;
    public string TransitionsJson { get; private set; } = default!;

    private WorkflowDefinition() { }

    private WorkflowDefinition(
        Guid id,
        Guid? tenantId,
        string name,
        string displayName,
        string entityType,
        string statesJson,
        string transitionsJson,
        bool isTemplate,
        string? sourceModule) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        DisplayName = displayName;
        EntityType = entityType;
        StatesJson = statesJson;
        TransitionsJson = transitionsJson;
        IsTemplate = isTemplate;
        IsActive = true;
        SourceModule = sourceModule;
        Version = 1;
    }

    public static WorkflowDefinition Create(
        Guid? tenantId,
        string name,
        string displayName,
        string entityType,
        string statesJson,
        string transitionsJson,
        bool isTemplate,
        string? sourceModule)
    {
        return new WorkflowDefinition(
            Guid.NewGuid(),
            tenantId,
            name.Trim(),
            displayName.Trim(),
            entityType.Trim(),
            statesJson,
            transitionsJson,
            isTemplate,
            sourceModule);
    }

    public void Update(string displayName, string? description, string statesJson, string transitionsJson)
    {
        DisplayName = displayName.Trim();
        Description = description?.Trim();
        StatesJson = statesJson;
        TransitionsJson = transitionsJson;
        Version++;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
    }

    public WorkflowDefinition Clone(Guid? tenantId)
    {
        var clone = new WorkflowDefinition(
            Guid.NewGuid(),
            tenantId,
            Name,
            DisplayName,
            EntityType,
            StatesJson,
            TransitionsJson,
            isTemplate: false,
            SourceModule);

        clone.Description = Description;
        clone.SourceDefinitionId = Id;

        return clone;
    }
}
