using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Webhooks.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("webhook_endpoints");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Url)
            .HasColumnName("url")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(e => e.Secret)
            .HasColumnName("secret")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.Events)
            .HasColumnName("events")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(e => e.TenantId);

        builder.HasMany(e => e.Deliveries)
            .WithOne(d => d.Endpoint)
            .HasForeignKey(d => d.WebhookEndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
