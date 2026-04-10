import 'package:boilerplate_mobile/core/network/interceptors/auth_interceptor.dart';
import 'package:boilerplate_mobile/core/network/interceptors/language_interceptor.dart';
import 'package:boilerplate_mobile/core/network/interceptors/logging_interceptor.dart';
import 'package:boilerplate_mobile/core/network/interceptors/refresh_interceptor.dart';
import 'package:boilerplate_mobile/core/network/interceptors/tenant_interceptor.dart';
import 'package:boilerplate_mobile/core/storage/secure_storage_service.dart';
import 'package:dio/dio.dart';

/// Factory that builds a fully configured [Dio] instance.
///
/// The interceptor order matters:
/// 1. [LanguageInterceptor] — sets Accept-Language (always).
/// 2. [TenantInterceptor] — adds X-Tenant-Id if multi-tenancy is enabled.
/// 3. [AuthInterceptor] — injects the Bearer token on every request.
/// 4. [RefreshInterceptor] — intercepts 401 responses, performs a
///    single-flight token refresh, and retries the original request.
/// 5. [LoggingInterceptor] — pretty-prints request/response in debug.
Dio createDioClient({
  required String baseUrl,
  required SecureStorageService secureStorage,
  required bool multiTenancyEnabled,
}) {
  final dio = Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      sendTimeout: const Duration(seconds: 15),
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    ),
  );

  dio.interceptors.addAll([
    LanguageInterceptor(),
    TenantInterceptor(
      secureStorage: secureStorage,
      multiTenancyEnabled: multiTenancyEnabled,
    ),
    AuthInterceptor(secureStorage: secureStorage),
    RefreshInterceptor(dio: dio, secureStorage: secureStorage),
    const LoggingInterceptor(),
  ]);

  return dio;
}
