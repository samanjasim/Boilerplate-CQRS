using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class TriggerRuleConfiguration : IEntityTypeConfiguration<TriggerRule>
{
    public void Configure(EntityTypeBuilder<TriggerRule> builder)
    {
        builder.ToTable("communication_trigger_rules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventName).HasColumnName("event_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.MessageTemplateId).HasColumnName("message_template_id").IsRequired();
        builder.Property(e => e.RecipientMode).HasColumnName("recipient_mode").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ChannelSequenceJson).HasColumnName("channel_sequence_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.DelaySeconds).HasColumnName("delay_seconds").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.ConditionJson).HasColumnName("condition_json").HasColumnType("jsonb");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");
        builder.HasIndex(e => e.TenantId);
        builder.HasOne<MessageTemplate>()
            .WithMany()
            .HasForeignKey(e => e.MessageTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.IntegrationTargets)
            .WithOne()
            .HasForeignKey(e => e.TriggerRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
