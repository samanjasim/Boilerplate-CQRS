import 'dart:async';

import 'package:boilerplate_mobile/core/storage/secure_storage_service.dart';
import 'package:dio/dio.dart';
import 'package:logger/logger.dart';

/// Intercepts 401 responses and performs a **single-flight** token refresh.
///
/// "Single-flight" means: if three requests fail with 401 simultaneously,
/// only ONE refresh call is made. The other two wait for the same
/// `Completer<String>` and retry with the new token once it resolves.
///
/// If the refresh itself fails (network error, or the refresh token is
/// expired), a [DioException] with status 401 is propagated and the
/// presentation layer is expected to redirect to login.
class RefreshInterceptor extends QueuedInterceptor {
  RefreshInterceptor({
    required Dio dio,
    required SecureStorageService secureStorage,
  })  : _dio = dio,
        _secureStorage = secureStorage;

  final Dio _dio;
  final SecureStorageService _secureStorage;
  final _log = Logger(printer: SimplePrinter());

  /// When non-null, a refresh is in-flight and other 401s should await it.
  Completer<String>? _refreshCompleter;

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode != 401) {
      return handler.next(err);
    }

    // If the failing request was itself the refresh call, don't loop.
    if (err.requestOptions.path.contains('/auth/refresh-token')) {
      await _secureStorage.clearTokens();
      return handler.next(err);
    }

    try {
      final newToken = await _refreshAccessToken();
      // Retry the original request with the new token.
      final retryOptions = err.requestOptions
        ..headers['Authorization'] = 'Bearer $newToken';
      final response = await _dio.fetch<dynamic>(retryOptions);
      return handler.resolve(response);
    } on DioException catch (refreshErr) {
      // Refresh failed — propagate the original 401 so the app can
      // redirect to login.
      return handler.next(refreshErr);
    }
  }

  /// Either starts a refresh or joins an in-flight one.
  Future<String> _refreshAccessToken() async {
    // Another request already started a refresh — wait for it.
    if (_refreshCompleter != null) {
      return _refreshCompleter!.future;
    }

    _refreshCompleter = Completer<String>();

    try {
      final refreshToken = await _secureStorage.getRefreshToken();
      if (refreshToken == null || refreshToken.isEmpty) {
        throw DioException(
          requestOptions: RequestOptions(path: '/auth/refresh-token'),
          type: DioExceptionType.cancel,
          message: 'No refresh token available',
        );
      }

      _log.d('Refreshing access token…');

      // Use a separate Dio instance to avoid this interceptor re-catching
      // the refresh response.
      final freshDio = Dio(BaseOptions(baseUrl: _dio.options.baseUrl));
      final response = await freshDio.post<Map<String, dynamic>>(
        '/auth/refresh-token',
        data: {'refreshToken': refreshToken},
      );

      final data = response.data!;
      final newAccess = data['accessToken'] as String;
      final newRefresh = data['refreshToken'] as String;

      await _secureStorage.saveTokens(
        accessToken: newAccess,
        refreshToken: newRefresh,
      );

      _log.d('Token refreshed successfully');
      _refreshCompleter!.complete(newAccess);
      return newAccess;
    } catch (e) {
      _log.e('Token refresh failed', error: e);
      await _secureStorage.clearTokens();

      if (!_refreshCompleter!.isCompleted) {
        if (e is DioException) {
          _refreshCompleter!.completeError(e);
        } else {
          _refreshCompleter!.completeError(
            DioException(
              requestOptions: RequestOptions(path: '/auth/refresh-token'),
              error: e,
            ),
          );
        }
      }
      rethrow;
    } finally {
      _refreshCompleter = null;
    }
  }
}
