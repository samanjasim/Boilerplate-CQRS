using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Webhooks.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.WebhookEndpointId)
            .HasColumnName("webhook_endpoint_id")
            .IsRequired();

        builder.Property(d => d.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.RequestPayload)
            .HasColumnName("request_payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(d => d.ResponseStatusCode)
            .HasColumnName("response_status_code");

        builder.Property(d => d.ResponseBody)
            .HasColumnName("response_body")
            .HasMaxLength(4096);

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(d => d.Duration)
            .HasColumnName("duration");

        builder.Property(d => d.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(d => d.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(d => d.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(d => new { d.WebhookEndpointId, d.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(d => d.TenantId);
    }
}
