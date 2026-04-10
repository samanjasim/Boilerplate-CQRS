import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/tenant_subscription.dart';
import 'package:boilerplate_mobile/modules/billing/domain/repositories/billing_repository.dart';

class GetSubscriptionUseCase
    extends UseCase<TenantSubscription, NoParams> {
  GetSubscriptionUseCase(this._repository);
  final BillingRepository _repository;

  @override
  Future<Result<TenantSubscription>> call(NoParams input) =>
      _repository.getSubscription();
}
