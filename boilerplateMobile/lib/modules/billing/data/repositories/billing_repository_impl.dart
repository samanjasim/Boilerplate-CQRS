import 'package:boilerplate_mobile/core/error/dio_error_mapper.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/modules/billing/data/datasources/billing_remote_datasource.dart';
import 'package:boilerplate_mobile/modules/billing/data/dtos/subscription_plan_dto.dart';
import 'package:boilerplate_mobile/modules/billing/data/dtos/tenant_subscription_dto.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/subscription_plan.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/tenant_subscription.dart';
import 'package:boilerplate_mobile/modules/billing/domain/repositories/billing_repository.dart';
import 'package:dio/dio.dart';

class BillingRepositoryImpl implements BillingRepository {
  BillingRepositoryImpl({required BillingRemoteDataSource remoteDataSource})
      : _remote = remoteDataSource;

  final BillingRemoteDataSource _remote;

  @override
  Future<Result<List<SubscriptionPlan>>> getPlans() async {
    try {
      final dtos = await _remote.getPlans();
      final plans = dtos
          .where((d) => d.isActive)
          .map((d) => d.toDomain())
          .toList()
        ..sort((a, b) => a.displayOrder.compareTo(b.displayOrder));
      return Success(plans);
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<TenantSubscription>> getSubscription() async {
    try {
      final dto = await _remote.getSubscription();
      return Success(dto.toDomain());
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }
}
