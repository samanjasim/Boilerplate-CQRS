import 'package:boilerplate_mobile/core/storage/secure_storage_service.dart';
import 'package:dio/dio.dart';

/// Adds `X-Tenant-Id` header when multi-tenancy is enabled.
///
/// The tenant ID is extracted from the JWT on login and cached in
/// secure storage. When `multiTenancyEnabled` is false (set in
/// `AppConfig`), this interceptor is a no-op — no tenant header is
/// ever sent.
class TenantInterceptor extends Interceptor {
  TenantInterceptor({
    required SecureStorageService secureStorage,
    required bool multiTenancyEnabled,
  })  : _secureStorage = secureStorage,
        _multiTenancyEnabled = multiTenancyEnabled;

  final SecureStorageService _secureStorage;
  final bool _multiTenancyEnabled;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (!_multiTenancyEnabled) {
      return handler.next(options);
    }

    final tenantId = await _secureStorage.getTenantId();
    if (tenantId != null && tenantId.isNotEmpty) {
      options.headers['X-Tenant-Id'] = tenantId;
    }

    handler.next(options);
  }
}
