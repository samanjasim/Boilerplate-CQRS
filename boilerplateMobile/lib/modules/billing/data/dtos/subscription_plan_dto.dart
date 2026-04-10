import 'package:boilerplate_mobile/modules/billing/domain/entities/subscription_plan.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'subscription_plan_dto.freezed.dart';
part 'subscription_plan_dto.g.dart';

@freezed
abstract class PlanFeatureEntryDto with _$PlanFeatureEntryDto {
  const factory PlanFeatureEntryDto({
    required String key,
    required String value,
  }) = _PlanFeatureEntryDto;

  factory PlanFeatureEntryDto.fromJson(Map<String, dynamic> json) =>
      _$PlanFeatureEntryDtoFromJson(json);
}

@freezed
abstract class SubscriptionPlanDto with _$SubscriptionPlanDto {
  const factory SubscriptionPlanDto({
    required String id,
    required String name,
    required String slug,
    required double monthlyPrice,
    required double annualPrice,
    required String currency,
    required bool isFree,
    required bool isActive,
    required int displayOrder,
    String? description,
    @Default([]) List<PlanFeatureEntryDto> features,
    @Default(0) int trialDays,
  }) = _SubscriptionPlanDto;

  factory SubscriptionPlanDto.fromJson(Map<String, dynamic> json) =>
      _$SubscriptionPlanDtoFromJson(json);
}

extension SubscriptionPlanDtoMapper on SubscriptionPlanDto {
  SubscriptionPlan toDomain() => SubscriptionPlan(
        id: id,
        name: name,
        slug: slug,
        description: description,
        monthlyPrice: monthlyPrice,
        annualPrice: annualPrice,
        currency: currency,
        features: features
            .map((f) => PlanFeature(key: f.key, value: f.value))
            .toList(),
        isFree: isFree,
        isActive: isActive,
        displayOrder: displayOrder,
        trialDays: trialDays,
      );
}
