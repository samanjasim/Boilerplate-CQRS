using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

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

        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasIndex(e => e.ConversationId);
        builder.HasIndex(e => e.AgentTaskId);
    }
}
