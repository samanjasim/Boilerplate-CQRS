using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Billing.Domain.Entities;

namespace Starter.Module.Billing.Infrastructure.Configurations;

internal sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("tenant_subscriptions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.SubscriptionPlanId)
            .HasColumnName("subscription_plan_id")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(s => s.LockedMonthlyPrice)
            .HasColumnName("locked_monthly_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.LockedAnnualPrice)
            .HasColumnName("locked_annual_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(s => s.ExternalCustomerId)
            .HasColumnName("external_customer_id")
            .HasMaxLength(200);

        builder.Property(s => s.ExternalSubscriptionId)
            .HasColumnName("external_subscription_id")
            .HasMaxLength(200);

        builder.Property(s => s.BillingInterval)
            .HasColumnName("billing_interval")
            .IsRequired();

        builder.Property(s => s.CurrentPeriodStart)
            .HasColumnName("current_period_start")
            .IsRequired();

        builder.Property(s => s.CurrentPeriodEnd)
            .HasColumnName("current_period_end")
            .IsRequired();

        builder.Property(s => s.TrialEndAt)
            .HasColumnName("trial_end_at");

        builder.Property(s => s.CanceledAt)
            .HasColumnName("canceled_at");

        builder.Property(s => s.AutoRenew)
            .HasColumnName("auto_renew")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(s => s.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(s => s.TenantId)
            .IsUnique();

        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.HasMany(s => s.Payments)
            .WithOne(p => p.Subscription)
            .HasForeignKey(p => p.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
