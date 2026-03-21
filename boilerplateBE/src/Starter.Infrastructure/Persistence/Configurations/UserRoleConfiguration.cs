using Starter.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id");

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id");

        builder.Property(ur => ur.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(ur => ur.AssignedBy)
            .HasColumnName("assigned_by");

        builder.HasIndex(ur => ur.UserId);
        builder.HasIndex(ur => ur.RoleId);

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
