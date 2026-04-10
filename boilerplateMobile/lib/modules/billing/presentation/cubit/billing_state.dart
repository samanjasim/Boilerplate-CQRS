import 'package:boilerplate_mobile/modules/billing/domain/entities/subscription_plan.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/tenant_subscription.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'billing_state.freezed.dart';

@freezed
sealed class BillingState with _$BillingState {
  const factory BillingState.initial() = BillingInitial;
  const factory BillingState.loading() = BillingLoading;
  const factory BillingState.loaded({
    required List<SubscriptionPlan> plans,
    TenantSubscription? subscription,
  }) = BillingLoaded;
  const factory BillingState.error(String message) = BillingError;
}
