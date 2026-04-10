import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/subscription_plan.dart';
import 'package:boilerplate_mobile/modules/billing/domain/repositories/billing_repository.dart';

class GetPlansUseCase extends UseCase<List<SubscriptionPlan>, NoParams> {
  GetPlansUseCase(this._repository);
  final BillingRepository _repository;

  @override
  Future<Result<List<SubscriptionPlan>>> call(NoParams input) =>
      _repository.getPlans();
}
