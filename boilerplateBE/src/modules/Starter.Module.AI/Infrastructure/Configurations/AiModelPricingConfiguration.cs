using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiModelPricingConfiguration : IEntityTypeConfiguration<AiModelPricing>
{
    public void Configure(EntityTypeBuilder<AiModelPricing> builder)
    {
        builder.ToTable("ai_model_pricings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasColumnName("model")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.InputUsdPer1KTokens)
            .HasColumnName("input_usd_per_1k_tokens")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(e => e.OutputUsdPer1KTokens)
            .HasColumnName("output_usd_per_1k_tokens")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.EffectiveFrom)
            .HasColumnName("effective_from")
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        // Unique composite: one row per (provider, model, effective_from)
        builder.HasIndex(e => new { e.Provider, e.Model, e.EffectiveFrom })
            .IsUnique()
            .HasDatabaseName("ix_ai_model_pricings_provider_model_effective_from");

        // Query index to speed up "current pricing" lookups
        builder.HasIndex(e => new { e.Provider, e.Model, e.IsActive })
            .HasDatabaseName("ix_ai_model_pricings_provider_model_is_active");
    }
}
