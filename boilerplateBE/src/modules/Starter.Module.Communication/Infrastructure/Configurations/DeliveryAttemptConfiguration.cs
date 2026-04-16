using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("communication_delivery_attempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.DeliveryLogId).HasColumnName("delivery_log_id").IsRequired();
        builder.Property(e => e.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel");
        builder.Property(e => e.IntegrationType).HasColumnName("integration_type");
        builder.Property(e => e.Provider).HasColumnName("provider");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.ProviderResponse).HasColumnName("provider_response").HasMaxLength(4000);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.DurationMs).HasColumnName("duration_ms");
        builder.Property(e => e.AttemptedAt).HasColumnName("attempted_at").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(e => e.DeliveryLogId);
    }
}
