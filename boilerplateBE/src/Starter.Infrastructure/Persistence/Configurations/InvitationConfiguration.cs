using Starter.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(i => i.Email);

        builder.Property(i => i.Token)
            .HasColumnName("token")
            .HasMaxLength(Invitation.TokenLength)
            .IsRequired();

        builder.HasIndex(i => i.Token)
            .IsUnique();

        builder.Property(i => i.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.HasIndex(i => i.TenantId);

        builder.Property(i => i.InvitedBy)
            .HasColumnName("invited_by")
            .IsRequired();

        builder.Property(i => i.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(i => i.IsAccepted)
            .HasColumnName("is_accepted")
            .HasDefaultValue(false);

        builder.Property(i => i.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(i => i.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(i => i.ModifiedBy)
            .HasColumnName("modified_by");
    }
}
