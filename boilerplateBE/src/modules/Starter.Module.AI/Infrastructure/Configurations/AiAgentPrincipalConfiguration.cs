using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAgentPrincipalConfiguration : IEntityTypeConfiguration<AiAgentPrincipal>
{
    public void Configure(EntityTypeBuilder<AiAgentPrincipal> builder)
    {
        builder.ToTable("ai_agent_principals");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.AiAssistantId)
            .HasColumnName("ai_assistant_id")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasOne<AiAssistant>()
            .WithOne()
            .HasForeignKey<AiAgentPrincipal>(x => x.AiAssistantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.AiAssistantId)
            .IsUnique()
            .HasDatabaseName("ix_ai_agent_principals_ai_assistant_id");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_ai_agent_principals_tenant_id");
    }
}
