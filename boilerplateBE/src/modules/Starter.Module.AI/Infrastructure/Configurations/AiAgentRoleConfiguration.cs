using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAgentRoleConfiguration : IEntityTypeConfiguration<AiAgentRole>
{
    public void Configure(EntityTypeBuilder<AiAgentRole> builder)
    {
        builder.ToTable("ai_agent_roles");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.AgentPrincipalId)
            .HasColumnName("agent_principal_id")
            .IsRequired();

        builder.Property(e => e.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(e => e.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(e => e.AssignedByUserId)
            .HasColumnName("assigned_by_user_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // FK to AiAgentPrincipal with cascade delete
        // No EF FK to core Role — RoleId stored as bare GUID (cross-context pattern, spec §4.1)
        builder.HasOne<AiAgentPrincipal>()
            .WithMany()
            .HasForeignKey(e => e.AgentPrincipalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.AgentPrincipalId, e.RoleId })
            .IsUnique()
            .HasDatabaseName("ix_ai_agent_roles_agent_principal_id_role_id");
    }
}
