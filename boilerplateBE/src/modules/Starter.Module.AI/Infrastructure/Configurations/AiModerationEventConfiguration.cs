using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiModerationEventConfiguration : IEntityTypeConfiguration<AiModerationEvent>
{
    public void Configure(EntityTypeBuilder<AiModerationEvent> b)
    {
        b.ToTable("ai_moderation_events");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.AssistantId).HasColumnName("assistant_id");
        b.Property(x => x.AgentPrincipalId).HasColumnName("agent_principal_id");
        b.Property(x => x.ConversationId).HasColumnName("conversation_id");
        b.Property(x => x.AgentTaskId).HasColumnName("agent_task_id");
        b.Property(x => x.MessageId).HasColumnName("message_id");
        b.Property(x => x.Stage).HasColumnName("stage").HasConversion<int>().IsRequired();
        b.Property(x => x.Preset).HasColumnName("preset").HasConversion<int>().IsRequired();
        b.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<int>().IsRequired();
        b.Property(x => x.CategoriesJson).HasColumnName("categories").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<int>().IsRequired();
        b.Property(x => x.BlockedReason).HasColumnName("blocked_reason");
        b.Property(x => x.RedactionFailed).HasColumnName("redaction_failed").IsRequired();
        b.Property(x => x.LatencyMs).HasColumnName("latency_ms").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.ModifiedAt).HasColumnName("modified_at");

        b.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("ix_ai_moderation_events_tenant_id_created_at")
            .IsDescending(false, true);
        b.HasIndex(x => new { x.TenantId, x.Outcome })
            .HasDatabaseName("ix_ai_moderation_events_tenant_id_outcome");
        b.HasIndex(x => x.MessageId)
            .HasDatabaseName("ix_ai_moderation_events_message_id")
            .HasFilter("message_id IS NOT NULL");
    }
}
