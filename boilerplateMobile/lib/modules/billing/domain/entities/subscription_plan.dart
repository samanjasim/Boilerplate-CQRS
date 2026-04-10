class PlanFeature {
  const PlanFeature({required this.key, required this.value});
  final String key;
  final String value;
}

class SubscriptionPlan {
  const SubscriptionPlan({
    required this.id,
    required this.name,
    required this.slug,
    required this.monthlyPrice,
    required this.annualPrice,
    required this.currency,
    required this.isFree,
    required this.isActive,
    required this.displayOrder,
    this.description,
    this.features = const [],
    this.trialDays = 0,
  });

  final String id;
  final String name;
  final String slug;
  final String? description;
  final double monthlyPrice;
  final double annualPrice;
  final String currency;
  final List<PlanFeature> features;
  final bool isFree;
  final bool isActive;
  final int displayOrder;
  final int trialDays;
}
