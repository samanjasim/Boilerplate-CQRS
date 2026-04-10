import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/app/modules.config.dart';
import 'package:boilerplate_mobile/core/capabilities/analytics_collector.dart';
import 'package:boilerplate_mobile/core/di/injection.config.dart';
import 'package:boilerplate_mobile/core/features/auth/data/datasources/auth_remote_datasource.dart';
import 'package:boilerplate_mobile/core/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/usecases/login_usecase.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/login_cubit.dart';
import 'package:boilerplate_mobile/core/features/notifications/data/datasources/notification_remote_datasource.dart';
import 'package:boilerplate_mobile/core/features/notifications/data/repositories/notification_repository_impl.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/repositories/notification_repository.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/get_notifications_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/get_unread_count_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/mark_all_read_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/mark_notification_read_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:boilerplate_mobile/core/features/profile/data/datasources/profile_remote_datasource.dart';
import 'package:boilerplate_mobile/core/features/profile/data/repositories/profile_repository_impl.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/repositories/profile_repository.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/usecases/change_password_usecase.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/usecases/get_profile_usecase.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/usecases/update_profile_usecase.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/cubit/profile_cubit.dart';
import 'package:boilerplate_mobile/core/modularity/module_registry.dart';
import 'package:boilerplate_mobile/core/network/dio_client.dart';
import 'package:boilerplate_mobile/core/storage/hive_service.dart';
import 'package:boilerplate_mobile/core/storage/secure_storage_service.dart';
import 'package:dio/dio.dart';
import 'package:get_it/get_it.dart';
import 'package:injectable/injectable.dart';

/// Global service locator instance.
final GetIt sl = GetIt.instance;

/// Initialise all DI registrations.
@InjectableInit()
Future<void> configureDependencies(AppConfig config) async {
  // --- Core services ---
  sl
    ..registerLazySingleton<SecureStorageService>(SecureStorageService.new)
    ..registerLazySingleton<Dio>(
      () => createDioClient(
        baseUrl: config.apiBaseUrl,
        secureStorage: sl<SecureStorageService>(),
        multiTenancyEnabled: config.multiTenancyEnabled,
      ),
    );

  // Hive cache
  final hiveService = HiveService();
  await hiveService.init();
  sl
    ..registerSingleton<HiveService>(hiveService)

    // --- Capability Null Object fallbacks ---
    ..registerLazySingleton<AnalyticsCollector>(NullAnalyticsCollector.new)

    // --- Injectable codegen registrations ---
    ..init()

    // --- Auth feature ---
    ..registerLazySingleton<AuthRemoteDataSource>(
      () => AuthRemoteDataSource(sl<Dio>()),
    )
    ..registerLazySingleton<AuthRepository>(
      () => AuthRepositoryImpl(
        remoteDataSource: sl<AuthRemoteDataSource>(),
        secureStorage: sl<SecureStorageService>(),
        hiveService: sl<HiveService>(),
      ),
    )
    ..registerLazySingleton(() => LoginUseCase(sl<AuthRepository>()))
    ..registerLazySingleton(() => AuthCubit(sl<AuthRepository>()))
    ..registerFactory(
      () => LoginCubit(
        loginUseCase: sl<LoginUseCase>(),
        authCubit: sl<AuthCubit>(),
      ),
    )

    // --- Profile feature ---
    ..registerLazySingleton<ProfileRemoteDataSource>(
      () => ProfileRemoteDataSource(sl<Dio>()),
    )
    ..registerLazySingleton<ProfileRepository>(
      () => ProfileRepositoryImpl(
        remoteDataSource: sl<ProfileRemoteDataSource>(),
      ),
    )
    ..registerLazySingleton(
      () => GetProfileUseCase(sl<ProfileRepository>()),
    )
    ..registerLazySingleton(
      () => UpdateProfileUseCase(sl<ProfileRepository>()),
    )
    ..registerLazySingleton(
      () => ChangePasswordUseCase(sl<ProfileRepository>()),
    )
    ..registerFactory(
      () => ProfileCubit(
        getProfileUseCase: sl<GetProfileUseCase>(),
        updateProfileUseCase: sl<UpdateProfileUseCase>(),
        changePasswordUseCase: sl<ChangePasswordUseCase>(),
        authCubit: sl<AuthCubit>(),
      ),
    )

    // --- Notifications feature ---
    ..registerLazySingleton<NotificationRemoteDataSource>(
      () => NotificationRemoteDataSource(sl<Dio>()),
    )
    ..registerLazySingleton<NotificationRepository>(
      () => NotificationRepositoryImpl(
        remoteDataSource: sl<NotificationRemoteDataSource>(),
      ),
    )
    ..registerLazySingleton(
      () => GetNotificationsUseCase(sl<NotificationRepository>()),
    )
    ..registerLazySingleton(
      () => GetUnreadCountUseCase(sl<NotificationRepository>()),
    )
    ..registerLazySingleton(
      () => MarkNotificationReadUseCase(sl<NotificationRepository>()),
    )
    ..registerLazySingleton(
      () => MarkAllReadUseCase(sl<NotificationRepository>()),
    )
    ..registerFactory(
      () => NotificationsCubit(
        getNotifications: sl<GetNotificationsUseCase>(),
        getUnreadCount: sl<GetUnreadCountUseCase>(),
        markRead: sl<MarkNotificationReadUseCase>(),
        markAllRead: sl<MarkAllReadUseCase>(),
      ),
    );

  // --- Module system ---
  ModuleRegistry.instance.init(activeModules(), sl);
}
