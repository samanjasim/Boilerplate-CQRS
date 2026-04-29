using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("ai_usage_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.ConversationId)
            .HasColumnName("conversation_id");

        builder.Property(e => e.AgentTaskId)
            .HasColumnName("agent_task_id");

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasColumnName("model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.InputTokens)
            .HasColumnName("input_tokens")
            .IsRequired();

        builder.Property(e => e.OutputTokens)
            .HasColumnName("output_tokens")
            .IsRequired();

        builder.Property(e => e.EstimatedCost)
            .HasColumnName("estimated_cost")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(e => e.RequestType)
            .HasColumnName("request_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(x => x.AiAssistantId)
            .HasColumnName("ai_assistant_id");

        builder.Property(x => x.AgentPrincipalId)
            .HasColumnName("agent_principal_id");

        builder.Property(e => e.ProviderCredentialSource)
            .HasColumnName("provider_credential_source")
            .HasConversion<int>()
            .HasDefaultValue(ProviderCredentialSource.Platform)
            .IsRequired();

        builder.Property(e => e.ProviderCredentialId)
            .HasColumnName("provider_credential_id");

        builder.HasIndex(x => new { x.TenantId, x.AiAssistantId, x.CreatedAt })
               .HasDatabaseName("ix_ai_usage_logs_tenant_id_ai_assistant_id_created_at");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
               .HasDatabaseName("ix_ai_usage_logs_tenant_id_created_at");

        builder.HasIndex(e => e.ConversationId);
        builder.HasIndex(e => e.AgentTaskId);
    }
}
