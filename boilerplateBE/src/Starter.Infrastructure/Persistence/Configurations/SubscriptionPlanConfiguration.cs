using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Billing.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Slug)
            .HasColumnName("slug")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(p => p.Translations)
            .HasColumnName("translations")
            .HasColumnType("jsonb");

        builder.Property(p => p.MonthlyPrice)
            .HasColumnName("monthly_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.AnnualPrice)
            .HasColumnName("annual_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(p => p.Features)
            .HasColumnName("features")
            .HasColumnType("jsonb");

        builder.Property(p => p.IsFree)
            .HasColumnName("is_free")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.IsPublic)
            .HasColumnName("is_public")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(p => p.TrialDays)
            .HasColumnName("trial_days")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(p => p.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.HasIndex(p => new { p.IsActive, p.IsPublic, p.DisplayOrder });

        builder.HasMany(p => p.Subscriptions)
            .WithOne(s => s.Plan)
            .HasForeignKey(s => s.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PriceHistory)
            .WithOne(h => h.Plan)
            .HasForeignKey(h => h.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
