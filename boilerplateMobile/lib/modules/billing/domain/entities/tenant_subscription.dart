enum SubscriptionStatus { trialing, active, pastDue, canceled, expired }

enum BillingInterval { monthly, annual }

class TenantSubscription {
  const TenantSubscription({
    required this.id,
    required this.planName,
    required this.planSlug,
    required this.status,
    required this.billingInterval,
    required this.currentPeriodStart,
    required this.currentPeriodEnd,
    required this.lockedMonthlyPrice,
    required this.lockedAnnualPrice,
    required this.currency,
    required this.autoRenew,
    required this.createdAt,
    this.canceledAt,
  });

  final String id;
  final String planName;
  final String planSlug;
  final SubscriptionStatus status;
  final BillingInterval billingInterval;
  final DateTime currentPeriodStart;
  final DateTime currentPeriodEnd;
  final double lockedMonthlyPrice;
  final double lockedAnnualPrice;
  final String currency;
  final bool autoRenew;
  final DateTime createdAt;
  final DateTime? canceledAt;

  double get currentPrice => billingInterval == BillingInterval.monthly
      ? lockedMonthlyPrice
      : lockedAnnualPrice;
}
