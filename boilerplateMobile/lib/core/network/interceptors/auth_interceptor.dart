import 'package:boilerplate_mobile/core/storage/secure_storage_service.dart';
import 'package:dio/dio.dart';

/// Injects the `Authorization: Bearer <token>` header on every request
/// that is not itself a token-refresh call.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({required SecureStorageService secureStorage})
      : _secureStorage = secureStorage;

  final SecureStorageService _secureStorage;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    // Skip auth header for the refresh endpoint itself to avoid circular
    // dependency (refresh sends the refresh token in the body, not the
    // access token in the header).
    if (options.path.contains('/auth/refresh-token')) {
      return handler.next(options);
    }

    final token = await _secureStorage.getAccessToken();
    if (token != null && token.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer $token';
    }

    handler.next(options);
  }
}
