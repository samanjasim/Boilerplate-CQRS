using Starter.Domain.Tenants.Entities;
using Starter.Domain.Tenants.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique();

        // Smart enum conversion for TenantStatus
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                status => status.Name,
                name => TenantStatus.FromName(name)!)
            .IsRequired();

        builder.Property(t => t.ConnectionString)
            .HasColumnName("connection_string")
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(t => t.ModifiedBy)
            .HasColumnName("modified_by");
    }
}
