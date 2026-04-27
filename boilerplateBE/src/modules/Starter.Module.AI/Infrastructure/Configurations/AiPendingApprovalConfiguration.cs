using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiPendingApprovalConfiguration : IEntityTypeConfiguration<AiPendingApproval>
{
    public void Configure(EntityTypeBuilder<AiPendingApproval> b)
    {
        b.ToTable("ai_pending_approvals");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.AssistantId).HasColumnName("assistant_id").IsRequired();
        b.Property(x => x.AssistantName).HasColumnName("assistant_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.AgentPrincipalId).HasColumnName("agent_principal_id").IsRequired();
        b.Property(x => x.ConversationId).HasColumnName("conversation_id");
        b.Property(x => x.AgentTaskId).HasColumnName("agent_task_id");
        b.Property(x => x.RequestingUserId).HasColumnName("requesting_user_id");
        b.Property(x => x.ToolName).HasColumnName("tool_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.CommandTypeName).HasColumnName("command_type_name").HasMaxLength(500).IsRequired();
        b.Property(x => x.ArgumentsJson).HasColumnName("arguments_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ReasonHint).HasColumnName("reason_hint").HasMaxLength(500);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        b.Property(x => x.DecisionUserId).HasColumnName("decision_user_id");
        b.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(1000);
        b.Property(x => x.DecidedAt).HasColumnName("decided_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.ModifiedBy).HasColumnName("modified_by");

        b.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_ai_pending_approvals_tenant_status_expires");
        b.HasIndex(x => new { x.RequestingUserId, x.Status })
            .HasDatabaseName("ix_ai_pending_approvals_requesting_user_status")
            .HasFilter("requesting_user_id IS NOT NULL");
    }
}
