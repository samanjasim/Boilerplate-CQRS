using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Billing.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class PlanPriceHistoryConfiguration : IEntityTypeConfiguration<PlanPriceHistory>
{
    public void Configure(EntityTypeBuilder<PlanPriceHistory> builder)
    {
        builder.ToTable("plan_price_history");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(h => h.SubscriptionPlanId)
            .HasColumnName("subscription_plan_id")
            .IsRequired();

        builder.Property(h => h.MonthlyPrice)
            .HasColumnName("monthly_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(h => h.AnnualPrice)
            .HasColumnName("annual_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(h => h.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(h => h.ChangedBy)
            .HasColumnName("changed_by")
            .IsRequired();

        builder.Property(h => h.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1000);

        builder.Property(h => h.EffectiveFrom)
            .HasColumnName("effective_from")
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(h => h.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(h => h.SubscriptionPlanId);
    }
}
