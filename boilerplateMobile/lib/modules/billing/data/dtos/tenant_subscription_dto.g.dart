// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'tenant_subscription_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_TenantSubscriptionDto _$TenantSubscriptionDtoFromJson(
        Map<String, dynamic> json) =>
    _TenantSubscriptionDto(
      id: json['id'] as String,
      planName: json['planName'] as String,
      planSlug: json['planSlug'] as String,
      status: json['status'] as String,
      billingInterval: json['billingInterval'] as String,
      currentPeriodStart: json['currentPeriodStart'] as String,
      currentPeriodEnd: json['currentPeriodEnd'] as String,
      lockedMonthlyPrice: (json['lockedMonthlyPrice'] as num).toDouble(),
      lockedAnnualPrice: (json['lockedAnnualPrice'] as num).toDouble(),
      currency: json['currency'] as String,
      autoRenew: json['autoRenew'] as bool,
      createdAt: json['createdAt'] as String,
      canceledAt: json['canceledAt'] as String?,
    );

Map<String, dynamic> _$TenantSubscriptionDtoToJson(
        _TenantSubscriptionDto instance) =>
    <String, dynamic>{
      'id': instance.id,
      'planName': instance.planName,
      'planSlug': instance.planSlug,
      'status': instance.status,
      'billingInterval': instance.billingInterval,
      'currentPeriodStart': instance.currentPeriodStart,
      'currentPeriodEnd': instance.currentPeriodEnd,
      'lockedMonthlyPrice': instance.lockedMonthlyPrice,
      'lockedAnnualPrice': instance.lockedAnnualPrice,
      'currency': instance.currency,
      'autoRenew': instance.autoRenew,
      'createdAt': instance.createdAt,
      'canceledAt': instance.canceledAt,
    };
