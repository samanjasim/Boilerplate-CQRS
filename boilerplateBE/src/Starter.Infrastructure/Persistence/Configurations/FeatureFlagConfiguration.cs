using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(f => f.DefaultValue).HasColumnName("default_value").HasMaxLength(4000).IsRequired();
        builder.Property(f => f.ValueType).HasColumnName("value_type").IsRequired();
        builder.Property(f => f.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(f => f.IsSystem).HasColumnName("is_system").IsRequired();

        builder.HasIndex(f => f.Key).IsUnique();
        builder.HasIndex(f => f.Category);

        builder.HasMany(f => f.TenantOverrides)
            .WithOne(t => t.FeatureFlag)
            .HasForeignKey(t => t.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
