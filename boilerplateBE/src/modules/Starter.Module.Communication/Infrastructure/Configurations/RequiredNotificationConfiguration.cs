using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class RequiredNotificationConfiguration : IEntityTypeConfiguration<RequiredNotification>
{
    public void Configure(EntityTypeBuilder<RequiredNotification> builder)
    {
        builder.ToTable("communication_required_notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Category, e.Channel }).IsUnique();
        builder.HasIndex(e => e.TenantId);
    }
}
