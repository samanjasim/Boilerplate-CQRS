using Starter.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(Permission.MaxNameLength)
            .IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(Permission.MaxDescriptionLength);

        builder.Property(p => p.Module)
            .HasColumnName("module")
            .HasMaxLength(100);

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Navigation(p => p.RolePermissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
