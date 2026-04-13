using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAgentTaskConfiguration : IEntityTypeConfiguration<AiAgentTask>
{
    public void Configure(EntityTypeBuilder<AiAgentTask> builder)
    {
        builder.ToTable("ai_agent_tasks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.AssistantId)
            .HasColumnName("assistant_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Instruction)
            .HasColumnName("instruction")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Steps)
            .HasColumnName("steps")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Result)
            .HasColumnName("result");

        builder.Property(e => e.TotalTokensUsed)
            .HasColumnName("total_tokens_used")
            .IsRequired();

        builder.Property(e => e.StepCount)
            .HasColumnName("step_count")
            .IsRequired();

        builder.Property(e => e.TriggeredBy)
            .HasColumnName("triggered_by")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.TriggerId)
            .HasColumnName("trigger_id");

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(e => new { e.TenantId, e.UserId });
        builder.HasIndex(e => e.Status);
    }
}
