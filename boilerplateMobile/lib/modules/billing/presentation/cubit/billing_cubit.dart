import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';
import 'package:boilerplate_mobile/modules/billing/domain/usecases/get_plans_usecase.dart';
import 'package:boilerplate_mobile/modules/billing/domain/usecases/get_subscription_usecase.dart';
import 'package:boilerplate_mobile/modules/billing/presentation/cubit/billing_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class BillingCubit extends Cubit<BillingState> {
  BillingCubit({
    required GetPlansUseCase getPlans,
    required GetSubscriptionUseCase getSubscription,
  })  : _getPlans = getPlans,
        _getSubscription = getSubscription,
        super(const BillingState.initial());

  final GetPlansUseCase _getPlans;
  final GetSubscriptionUseCase _getSubscription;

  Future<void> load() async {
    emit(const BillingState.loading());

    final plansResult = await _getPlans(const NoParams());

    switch (plansResult) {
      case Success(value: final plans):
        // Try loading subscription — may 404 if tenant has none.
        final subResult = await _getSubscription(const NoParams());
        final subscription = switch (subResult) {
          Success(value: final s) => s,
          Err() => null,
        };
        emit(BillingState.loaded(plans: plans, subscription: subscription));
      case Err(failure: final f):
        emit(BillingState.error(f.message));
    }
  }
}
