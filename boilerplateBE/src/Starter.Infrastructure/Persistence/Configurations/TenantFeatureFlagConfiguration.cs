using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class TenantFeatureFlagConfiguration : IEntityTypeConfiguration<TenantFeatureFlag>
{
    public void Configure(EntityTypeBuilder<TenantFeatureFlag> builder)
    {
        builder.ToTable("tenant_feature_flags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.FeatureFlagId).HasColumnName("feature_flag_id").IsRequired();
        builder.Property(t => t.Value).HasColumnName("value").HasMaxLength(4000).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.FeatureFlagId }).IsUnique();
        builder.HasIndex(t => t.TenantId);
    }
}
