using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Workflow.Domain.Entities;

namespace Starter.Module.Workflow.Infrastructure.Persistence.Configurations;

public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("workflow_steps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.InstanceId)
            .HasColumnName("instance_id")
            .IsRequired();

        builder.Property(s => s.FromState)
            .HasColumnName("from_state")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.ToState)
            .HasColumnName("to_state")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.StepType)
            .HasColumnName("step_type")
            .IsRequired();

        builder.Property(s => s.Action)
            .HasColumnName("action")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(s => s.Comment)
            .HasColumnName("comment")
            .HasMaxLength(4000);

        builder.Property(s => s.MetadataJson)
            .HasColumnName("metadata_json")
            .HasColumnType("text");

        builder.Property(s => s.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(s => s.InstanceId);
    }
}
