// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'subscription_plan_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_PlanFeatureEntryDto _$PlanFeatureEntryDtoFromJson(Map<String, dynamic> json) =>
    _PlanFeatureEntryDto(
      key: json['key'] as String,
      value: json['value'] as String,
    );

Map<String, dynamic> _$PlanFeatureEntryDtoToJson(
        _PlanFeatureEntryDto instance) =>
    <String, dynamic>{
      'key': instance.key,
      'value': instance.value,
    };

_SubscriptionPlanDto _$SubscriptionPlanDtoFromJson(Map<String, dynamic> json) =>
    _SubscriptionPlanDto(
      id: json['id'] as String,
      name: json['name'] as String,
      slug: json['slug'] as String,
      monthlyPrice: (json['monthlyPrice'] as num).toDouble(),
      annualPrice: (json['annualPrice'] as num).toDouble(),
      currency: json['currency'] as String,
      isFree: json['isFree'] as bool,
      isActive: json['isActive'] as bool,
      displayOrder: (json['displayOrder'] as num).toInt(),
      description: json['description'] as String?,
      features: (json['features'] as List<dynamic>?)
              ?.map((e) =>
                  PlanFeatureEntryDto.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
      trialDays: (json['trialDays'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$SubscriptionPlanDtoToJson(
        _SubscriptionPlanDto instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'slug': instance.slug,
      'monthlyPrice': instance.monthlyPrice,
      'annualPrice': instance.annualPrice,
      'currency': instance.currency,
      'isFree': instance.isFree,
      'isActive': instance.isActive,
      'displayOrder': instance.displayOrder,
      'description': instance.description,
      'features': instance.features,
      'trialDays': instance.trialDays,
    };
