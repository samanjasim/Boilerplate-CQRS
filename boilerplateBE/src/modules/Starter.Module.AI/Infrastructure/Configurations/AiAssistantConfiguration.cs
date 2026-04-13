using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAssistantConfiguration : IEntityTypeConfiguration<AiAssistant>
{
    public void Configure(EntityTypeBuilder<AiAssistant> builder)
    {
        builder.ToTable("ai_assistants");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.SystemPrompt)
            .HasColumnName("system_prompt")
            .IsRequired();

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.Model)
            .HasColumnName("model")
            .HasMaxLength(100);

        builder.Property(e => e.Temperature)
            .HasColumnName("temperature")
            .IsRequired();

        builder.Property(e => e.MaxTokens)
            .HasColumnName("max_tokens")
            .IsRequired();

        builder.Property(e => e.EnabledToolNames)
            .HasColumnName("enabled_tool_names")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.KnowledgeBaseDocIds)
            .HasColumnName("knowledge_base_doc_ids")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ExecutionMode)
            .HasColumnName("execution_mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.MaxAgentSteps)
            .HasColumnName("max_agent_steps")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique();
    }
}
