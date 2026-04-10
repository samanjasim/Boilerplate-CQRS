import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/subscription_plan.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/tenant_subscription.dart';

abstract class BillingRepository {
  Future<Result<List<SubscriptionPlan>>> getPlans();
  Future<Result<TenantSubscription>> getSubscription();
}
