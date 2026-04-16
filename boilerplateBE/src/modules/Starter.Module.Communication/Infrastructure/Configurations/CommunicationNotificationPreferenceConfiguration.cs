using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class CommunicationNotificationPreferenceConfiguration : IEntityTypeConfiguration<CommunicationNotificationPreference>
{
    public void Configure(EntityTypeBuilder<CommunicationNotificationPreference> builder)
    {
        builder.ToTable("communication_notification_preferences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EmailEnabled).HasColumnName("email_enabled").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.SmsEnabled).HasColumnName("sms_enabled").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.PushEnabled).HasColumnName("push_enabled").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.WhatsAppEnabled).HasColumnName("whatsapp_enabled").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.InAppEnabled).HasColumnName("in_app_enabled").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.HasIndex(e => new { e.UserId, e.TenantId, e.Category }).IsUnique();
        builder.HasIndex(e => e.UserId);
    }
}
