using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class TriggerRuleIntegrationTargetConfiguration : IEntityTypeConfiguration<TriggerRuleIntegrationTarget>
{
    public void Configure(EntityTypeBuilder<TriggerRuleIntegrationTarget> builder)
    {
        builder.ToTable("communication_trigger_rule_integration_targets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TriggerRuleId).HasColumnName("trigger_rule_id").IsRequired();
        builder.Property(e => e.IntegrationConfigId).HasColumnName("integration_config_id").IsRequired();
        builder.Property(e => e.TargetChannelId).HasColumnName("target_channel_id").HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
