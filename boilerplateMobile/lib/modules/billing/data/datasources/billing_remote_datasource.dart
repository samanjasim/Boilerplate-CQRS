import 'package:boilerplate_mobile/core/network/api_response.dart';
import 'package:boilerplate_mobile/modules/billing/data/dtos/subscription_plan_dto.dart';
import 'package:boilerplate_mobile/modules/billing/data/dtos/tenant_subscription_dto.dart';
import 'package:dio/dio.dart';

class BillingRemoteDataSource {
  BillingRemoteDataSource(this._dio);
  final Dio _dio;

  Future<List<SubscriptionPlanDto>> getPlans() async {
    final response = await _dio.get<Map<String, dynamic>>('/Billing/plans');
    final apiResponse = ApiResponse<List<dynamic>>.fromJson(
      response.data!,
      (json) => json! as List<dynamic>,
    );
    return (apiResponse.data ?? [])
        .map((e) => SubscriptionPlanDto.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<TenantSubscriptionDto> getSubscription() async {
    final response =
        await _dio.get<Map<String, dynamic>>('/Billing/subscription');
    final apiResponse = ApiResponse<Map<String, dynamic>>.fromJson(
      response.data!,
      (json) => json! as Map<String, dynamic>,
    );
    return TenantSubscriptionDto.fromJson(apiResponse.data!);
  }
}
