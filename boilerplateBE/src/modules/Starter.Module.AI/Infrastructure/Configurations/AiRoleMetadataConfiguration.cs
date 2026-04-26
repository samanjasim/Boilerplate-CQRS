using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiRoleMetadataConfiguration : IEntityTypeConfiguration<AiRoleMetadata>
{
    public void Configure(EntityTypeBuilder<AiRoleMetadata> b)
    {
        b.ToTable("ai_role_metadata");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        b.Property(x => x.RoleId)
            .HasColumnName("role_id")
            .IsRequired();
        b.Property(x => x.IsAgentAssignable)
            .HasColumnName("is_agent_assignable")
            .IsRequired();
        b.HasIndex(x => x.RoleId)
            .IsUnique()
            .HasDatabaseName("ix_ai_role_metadata_role_id");
        // No EF FK to core Role.Id — same pattern as AiUsageLog.TenantId (spec §4.1).
    }
}
