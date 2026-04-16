using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class DeliveryLogConfiguration : IEntityTypeConfiguration<DeliveryLog>
{
    public void Configure(EntityTypeBuilder<DeliveryLog> builder)
    {
        builder.ToTable("communication_delivery_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(e => e.RecipientAddress).HasColumnName("recipient_address").HasMaxLength(500);
        builder.Property(e => e.MessageTemplateId).HasColumnName("message_template_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Channel).HasColumnName("channel");
        builder.Property(e => e.IntegrationType).HasColumnName("integration_type");
        builder.Property(e => e.Provider).HasColumnName("provider");
        builder.Property(e => e.Subject).HasColumnName("subject").HasMaxLength(500);
        builder.Property(e => e.BodyPreview).HasColumnName("body_preview").HasMaxLength(1000);
        builder.Property(e => e.VariablesJson).HasColumnName("variables_json").HasColumnType("jsonb");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(500);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.TriggerRuleId).HasColumnName("trigger_rule_id");
        builder.Property(e => e.TotalDurationMs).HasColumnName("total_duration_ms");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasMany(e => e.Attempts)
            .WithOne()
            .HasForeignKey(e => e.DeliveryLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
