using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiSafetyPresetProfileConfiguration : IEntityTypeConfiguration<AiSafetyPresetProfile>
{
    public void Configure(EntityTypeBuilder<AiSafetyPresetProfile> b)
    {
        b.ToTable("ai_safety_preset_profiles");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Preset).HasColumnName("preset").HasConversion<int>().IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<int>().IsRequired();
        b.Property(x => x.CategoryThresholdsJson).HasColumnName("category_thresholds").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.BlockedCategoriesJson).HasColumnName("blocked_categories").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.FailureMode).HasColumnName("failure_mode").HasConversion<int>().IsRequired();
        b.Property(x => x.RedactPii).HasColumnName("redact_pii").IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.ModifiedBy).HasColumnName("modified_by");

        // Unique active row per (tenant_id, preset, provider). NULL tenant_id = platform default.
        b.HasIndex(x => new { x.TenantId, x.Preset, x.Provider })
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ux_ai_safety_preset_profiles_tenant_preset_provider_active");
    }
}
