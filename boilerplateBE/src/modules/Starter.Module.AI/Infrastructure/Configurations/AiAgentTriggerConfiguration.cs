using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAgentTriggerConfiguration : IEntityTypeConfiguration<AiAgentTrigger>
{
    public void Configure(EntityTypeBuilder<AiAgentTrigger> builder)
    {
        builder.ToTable("ai_agent_triggers");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.AssistantId)
            .HasColumnName("assistant_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(e => e.TriggerType)
            .HasColumnName("trigger_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.CronExpression)
            .HasColumnName("cron_expression")
            .HasMaxLength(100);

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(200);

        builder.Property(e => e.Instruction)
            .HasColumnName("instruction")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.LastRunAt)
            .HasColumnName("last_run_at");

        builder.Property(e => e.NextRunAt)
            .HasColumnName("next_run_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique();
    }
}
