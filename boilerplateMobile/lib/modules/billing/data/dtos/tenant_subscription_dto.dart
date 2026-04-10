import 'package:boilerplate_mobile/modules/billing/domain/entities/tenant_subscription.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'tenant_subscription_dto.freezed.dart';
part 'tenant_subscription_dto.g.dart';

/// Custom converter for enum values that come as int from the BE.
class _StatusConverter implements JsonConverter<String, dynamic> {
  const _StatusConverter();

  static const _map = {0: 'Active', 1: 'Trialing', 2: 'PastDue', 3: 'Canceled', 4: 'Expired'};

  @override
  String fromJson(dynamic json) =>
      json is int ? (_map[json] ?? 'Active') : json.toString();

  @override
  dynamic toJson(String object) => object;
}

class _IntervalConverter implements JsonConverter<String, dynamic> {
  const _IntervalConverter();

  static const _map = {0: 'Monthly', 1: 'Annual'};

  @override
  String fromJson(dynamic json) =>
      json is int ? (_map[json] ?? 'Monthly') : json.toString();

  @override
  dynamic toJson(String object) => object;
}

@freezed
abstract class TenantSubscriptionDto with _$TenantSubscriptionDto {
  const factory TenantSubscriptionDto({
    required String id,
    required String planName,
    required String planSlug,
    @_StatusConverter() required String status,
    @_IntervalConverter() required String billingInterval,
    required String currentPeriodStart,
    required String currentPeriodEnd,
    @JsonKey(fromJson: _toDouble) required double lockedMonthlyPrice,
    @JsonKey(fromJson: _toDouble) required double lockedAnnualPrice,
    required String currency,
    required bool autoRenew,
    required String createdAt,
    String? canceledAt,
  }) = _TenantSubscriptionDto;

  factory TenantSubscriptionDto.fromJson(Map<String, dynamic> json) =>
      _$TenantSubscriptionDtoFromJson(json);
}

double _toDouble(dynamic value) => (value as num).toDouble();

extension TenantSubscriptionDtoMapper on TenantSubscriptionDto {
  TenantSubscription toDomain() => TenantSubscription(
        id: id,
        planName: planName,
        planSlug: planSlug,
        status: _parseStatus(status),
        billingInterval: _parseInterval(billingInterval),
        currentPeriodStart: DateTime.parse(currentPeriodStart),
        currentPeriodEnd: DateTime.parse(currentPeriodEnd),
        lockedMonthlyPrice: lockedMonthlyPrice,
        lockedAnnualPrice: lockedAnnualPrice,
        currency: currency,
        autoRenew: autoRenew,
        createdAt: DateTime.parse(createdAt),
        canceledAt:
            canceledAt != null ? DateTime.tryParse(canceledAt!) : null,
      );

  static SubscriptionStatus _parseStatus(String value) =>
      switch (value.toLowerCase()) {
        'trialing' => SubscriptionStatus.trialing,
        'active' => SubscriptionStatus.active,
        'pastdue' || 'past_due' => SubscriptionStatus.pastDue,
        'canceled' => SubscriptionStatus.canceled,
        'expired' => SubscriptionStatus.expired,
        _ => SubscriptionStatus.active,
      };

  static BillingInterval _parseInterval(String value) =>
      switch (value.toLowerCase()) {
        'annual' || 'yearly' => BillingInterval.annual,
        _ => BillingInterval.monthly,
      };
}
