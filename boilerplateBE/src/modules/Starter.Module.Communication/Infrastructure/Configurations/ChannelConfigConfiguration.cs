using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class ChannelConfigConfiguration : IEntityTypeConfiguration<ChannelConfig>
{
    public void Configure(EntityTypeBuilder<ChannelConfig> builder)
    {
        builder.ToTable("communication_channel_configs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").IsRequired();
        builder.Property(e => e.Provider).HasColumnName("provider").IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CredentialsJson).HasColumnName("credentials_json").HasColumnType("text").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.IsDefault).HasColumnName("is_default").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.LastTestedAt).HasColumnName("last_tested_at");
        builder.Property(e => e.LastTestResult).HasColumnName("last_test_result").HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Channel, e.Provider }).IsUnique();
    }
}
