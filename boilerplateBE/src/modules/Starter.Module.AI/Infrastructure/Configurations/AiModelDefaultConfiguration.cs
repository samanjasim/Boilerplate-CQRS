using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiModelDefaultConfiguration : IEntityTypeConfiguration<AiModelDefault>
{
    public void Configure(EntityTypeBuilder<AiModelDefault> builder)
    {
        builder.ToTable("ai_model_defaults");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.AgentClass).HasColumnName("agent_class").HasConversion<int>().IsRequired();
        builder.Property(e => e.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.Model).HasColumnName("model").HasMaxLength(200).IsRequired();
        builder.Property(e => e.MaxTokens).HasColumnName("max_tokens");
        builder.Property(e => e.Temperature).HasColumnName("temperature");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.AgentClass })
            .IsUnique()
            .HasDatabaseName("ux_ai_model_defaults_tenant_agent_class");
    }
}
