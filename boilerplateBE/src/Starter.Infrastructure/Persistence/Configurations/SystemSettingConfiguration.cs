using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.Key)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Value)
            .HasColumnName("value")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(s => s.Category)
            .HasColumnName("category")
            .HasMaxLength(100);

        builder.Property(s => s.IsSecret)
            .HasColumnName("is_secret")
            .HasDefaultValue(false);

        builder.Property(s => s.DataType)
            .HasColumnName("data_type")
            .HasMaxLength(50)
            .HasDefaultValue("text");

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(s => s.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(s => new { s.Key, s.TenantId })
            .IsUnique();

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.Category);
    }
}
