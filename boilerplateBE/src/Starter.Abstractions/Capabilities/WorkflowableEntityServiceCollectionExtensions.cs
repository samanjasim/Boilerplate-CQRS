using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Registers an entity type as workflowable — marks it for workflow integration.
/// The workflow module uses this registry to validate entity types and provide
/// UI hints (e.g., showing "Start Workflow" on entity detail pages).
/// </summary>
public static class WorkflowableEntityServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowableEntity(
        this IServiceCollection services,
        string entityType,
        Action<WorkflowableEntityBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        var builder = new WorkflowableEntityBuilder(entityType);
        configure?.Invoke(builder);

        services.AddSingleton<IWorkflowableEntityRegistration>(
            new WorkflowableEntityRegistration(builder));

        return services;
    }
}

/// <summary>
/// Builder surface for <see cref="WorkflowableEntityServiceCollectionExtensions.AddWorkflowableEntity"/>.
/// </summary>
public sealed class WorkflowableEntityBuilder
{
    public string EntityType { get; }
    public string? DefaultDefinitionName { get; set; }
    public string? DisplayNameProperty { get; set; }

    public WorkflowableEntityBuilder(string entityType) => EntityType = entityType;
}

/// <summary>
/// Registration record for a workflowable entity type.
/// </summary>
public interface IWorkflowableEntityRegistration
{
    string EntityType { get; }
    string? DefaultDefinitionName { get; }
    string? DisplayNameProperty { get; }
}

public sealed class WorkflowableEntityRegistration(WorkflowableEntityBuilder builder) : IWorkflowableEntityRegistration
{
    public string EntityType => builder.EntityType;
    public string? DefaultDefinitionName => builder.DefaultDefinitionName;
    public string? DisplayNameProperty => builder.DisplayNameProperty;
}
