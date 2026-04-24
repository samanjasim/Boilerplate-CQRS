using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class UserPersonaConfiguration : IEntityTypeConfiguration<UserPersona>
{
    public void Configure(EntityTypeBuilder<UserPersona> builder)
    {
        builder.ToTable("ai_user_personas");
        builder.HasKey(e => new { e.UserId, e.PersonaId });

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.PersonaId).HasColumnName("persona_id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(e => e.AssignedAt).HasColumnName("assigned_at").IsRequired();
        builder.Property(e => e.AssignedBy).HasColumnName("assigned_by");

        builder.HasOne(e => e.Persona)
            .WithMany()
            .HasForeignKey(e => e.PersonaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.UserId })
            .HasDatabaseName("ix_ai_user_personas_tenant_user");
        builder.HasIndex(e => e.PersonaId)
            .HasDatabaseName("ix_ai_user_personas_persona");

        builder.HasIndex(e => new { e.UserId, e.TenantId })
            .IsUnique()
            .HasFilter("is_default = TRUE")
            .HasDatabaseName("ux_ai_user_personas_user_tenant_default");
    }
}
