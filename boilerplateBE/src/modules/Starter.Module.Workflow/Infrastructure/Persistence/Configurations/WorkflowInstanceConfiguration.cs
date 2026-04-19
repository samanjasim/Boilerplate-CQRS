using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Workflow.Domain.Entities;

namespace Starter.Module.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("workflow_instances");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(i => i.DefinitionId)
            .HasColumnName("definition_id")
            .IsRequired();

        builder.Property(i => i.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(i => i.CurrentState)
            .HasColumnName("current_state")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(i => i.StartedByUserId)
            .HasColumnName("started_by_user_id")
            .IsRequired();

        builder.Property(i => i.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(i => i.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(i => i.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(i => i.CancelledByUserId)
            .HasColumnName("cancelled_by_user_id");

        builder.Property(i => i.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(2000);

        builder.Property(i => i.ContextJson)
            .HasColumnName("context_json")
            .HasColumnType("text");

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(i => i.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(i => i.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasOne(i => i.Definition)
            .WithMany()
            .HasForeignKey(i => i.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Steps)
            .WithOne()
            .HasForeignKey(s => s.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Tasks)
            .WithOne(t => t.Instance)
            .HasForeignKey(t => t.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.EntityType, i.EntityId });
        builder.HasIndex(i => new { i.TenantId, i.Status });
    }
}
