using Starter.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(Role.MaxNameLength)
            .IsRequired();

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(Role.MaxDescriptionLength);

        builder.Property(r => r.IsSystemRole)
            .HasColumnName("is_system_role")
            .HasDefaultValue(false);

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(r => r.ModifiedBy)
            .HasColumnName("modified_by");

        builder.Navigation(r => r.UserRoles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(r => r.RolePermissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
