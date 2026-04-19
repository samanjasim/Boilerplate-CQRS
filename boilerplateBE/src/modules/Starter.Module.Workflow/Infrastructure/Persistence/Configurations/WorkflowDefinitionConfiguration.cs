using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Workflow.Domain.Entities;

namespace Starter.Module.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("workflow_definitions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(d => d.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.IsTemplate)
            .HasColumnName("is_template")
            .IsRequired();

        builder.Property(d => d.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(d => d.SourceDefinitionId)
            .HasColumnName("source_definition_id");

        builder.Property(d => d.SourceModule)
            .HasColumnName("source_module")
            .HasMaxLength(200);

        builder.Property(d => d.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(d => d.StatesJson)
            .HasColumnName("states_json")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(d => d.TransitionsJson)
            .HasColumnName("transitions_json")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(d => d.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(d => d.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(d => new { d.TenantId, d.Name })
            .IsUnique();

        builder.HasIndex(d => d.EntityType);
    }
}
