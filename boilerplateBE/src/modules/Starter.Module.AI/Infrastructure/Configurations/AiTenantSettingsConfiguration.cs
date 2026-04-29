using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiTenantSettingsConfiguration : IEntityTypeConfiguration<AiTenantSettings>
{
    public void Configure(EntityTypeBuilder<AiTenantSettings> builder)
    {
        builder.ToTable("ai_tenant_settings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.RequestedProviderCredentialPolicy).HasColumnName("requested_provider_credential_policy").HasConversion<int>().IsRequired();
        builder.Property(e => e.DefaultSafetyPreset).HasColumnName("default_safety_preset").HasConversion<int>().IsRequired();
        builder.Property(e => e.MonthlyCostCapUsd).HasColumnName("monthly_cost_cap_usd").HasColumnType("decimal(18,4)");
        builder.Property(e => e.DailyCostCapUsd).HasColumnName("daily_cost_cap_usd").HasColumnType("decimal(18,4)");
        builder.Property(e => e.PlatformMonthlyCostCapUsd).HasColumnName("platform_monthly_cost_cap_usd").HasColumnType("decimal(18,4)");
        builder.Property(e => e.PlatformDailyCostCapUsd).HasColumnName("platform_daily_cost_cap_usd").HasColumnType("decimal(18,4)");
        builder.Property(e => e.RequestsPerMinute).HasColumnName("requests_per_minute");
        builder.Property(e => e.PublicMonthlyTokenCap).HasColumnName("public_monthly_token_cap");
        builder.Property(e => e.PublicDailyTokenCap).HasColumnName("public_daily_token_cap");
        builder.Property(e => e.PublicRequestsPerMinute).HasColumnName("public_requests_per_minute");
        builder.Property(e => e.AssistantDisplayName).HasColumnName("assistant_display_name").HasMaxLength(200);
        builder.Property(e => e.Tone).HasColumnName("tone").HasMaxLength(100);
        builder.Property(e => e.AvatarFileId).HasColumnName("avatar_file_id");
        builder.Property(e => e.BrandInstructions).HasColumnName("brand_instructions");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_ai_tenant_settings_tenant_id");
    }
}
