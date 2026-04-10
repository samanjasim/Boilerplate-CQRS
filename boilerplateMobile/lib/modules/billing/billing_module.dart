import 'package:boilerplate_mobile/core/modularity/app_module.dart';
import 'package:boilerplate_mobile/core/modularity/module_nav_item.dart';
import 'package:boilerplate_mobile/core/modularity/module_permission.dart';
import 'package:boilerplate_mobile/core/modularity/slot_contribution.dart';
import 'package:boilerplate_mobile/modules/billing/data/datasources/billing_remote_datasource.dart';
import 'package:boilerplate_mobile/modules/billing/data/repositories/billing_repository_impl.dart';
import 'package:boilerplate_mobile/modules/billing/domain/repositories/billing_repository.dart';
import 'package:boilerplate_mobile/modules/billing/domain/usecases/get_plans_usecase.dart';
import 'package:boilerplate_mobile/modules/billing/domain/usecases/get_subscription_usecase.dart';
import 'package:boilerplate_mobile/modules/billing/presentation/cubit/billing_cubit.dart';
import 'package:boilerplate_mobile/modules/billing/presentation/pages/billing_page.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:get_it/get_it.dart';

/// Billing module — pairs with BE `Starter.Module.Billing`.
///
/// Demonstrates the full module template: DI registration, nav item
/// contribution (permission-gated), slot contribution to profile page,
/// and clean removal via `rename.ps1 -Modules "webhooks,importExport"`.
class BillingModule extends AppModule {
  @override
  String get name => 'billing';

  @override
  String get displayName => 'Billing';

  @override
  String get version => '1.0.0';

  @override
  List<String> get dependencies => [];

  @override
  void registerDependencies(GetIt sl) {
    sl
      ..registerLazySingleton<BillingRemoteDataSource>(
        () => BillingRemoteDataSource(sl<Dio>()),
      )
      ..registerLazySingleton<BillingRepository>(
        () => BillingRepositoryImpl(
          remoteDataSource: sl<BillingRemoteDataSource>(),
        ),
      )
      ..registerLazySingleton(
        () => GetPlansUseCase(sl<BillingRepository>()),
      )
      ..registerLazySingleton(
        () => GetSubscriptionUseCase(sl<BillingRepository>()),
      )
      ..registerFactory(
        () => BillingCubit(
          getPlans: sl<GetPlansUseCase>(),
          getSubscription: sl<GetSubscriptionUseCase>(),
        ),
      );
  }

  @override
  List<ModuleNavItem> getNavItems() => [
        ModuleNavItem(
          label: 'Billing',
          icon: Icons.payments_outlined,
          activeIcon: Icons.payments,
          routePath: '/billing',
          requiredPermissions: const [BillingPermissions.view],
          order: 200,
          pageBuilder: () => BlocProvider(
            create: (_) => GetIt.instance<BillingCubit>(),
            child: const BillingPage(),
          ),
        ),
      ];

  @override
  Map<String, SlotContribution> getSlotContributions() => {
        'profile-info': SlotContribution(
          builder: (context, {args}) {
            return Card(
              child: ListTile(
                leading: Icon(
                  Icons.payments_outlined,
                  color: Theme.of(context).colorScheme.primary,
                ),
                title: const Text('Billing & Subscription'),
                subtitle: const Text('View your plan and billing details'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute<void>(
                      builder: (_) => BlocProvider(
                        create: (_) => GetIt.instance<BillingCubit>(),
                        child: const BillingPage(),
                      ),
                    ),
                  );
                },
              ),
            );
          },
        ),
      };

  @override
  List<ModulePermission> getDeclaredPermissions() => const [
        ModulePermission(
          key: BillingPermissions.view,
          displayName: 'View billing information',
          module: 'billing',
        ),
        ModulePermission(
          key: BillingPermissions.manage,
          displayName: 'Manage subscription',
          module: 'billing',
        ),
      ];
}

/// Billing permission constants — mirrors BE BillingPermissions.cs.
abstract final class BillingPermissions {
  static const view = 'Billing.View';
  static const manage = 'Billing.Manage';
  static const viewPlans = 'Billing.ViewPlans';
  static const managePlans = 'Billing.ManagePlans';
  static const manageTenantSubscriptions =
      'Billing.ManageTenantSubscriptions';
}
