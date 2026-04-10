import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:pretty_dio_logger/pretty_dio_logger.dart';

/// Pretty-prints HTTP requests and responses in **debug builds only**.
///
/// In release builds this interceptor is a transparent pass-through.
class LoggingInterceptor extends Interceptor {
  const LoggingInterceptor();

  /// Lazily initialized logger — only created in debug mode.
  static final PrettyDioLogger _logger = PrettyDioLogger();

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) {
    if (kDebugMode) {
      _logger.onRequest(options, handler);
    } else {
      handler.next(options);
    }
  }

  @override
  void onResponse(
    Response<dynamic> response,
    ResponseInterceptorHandler handler,
  ) {
    if (kDebugMode) {
      _logger.onResponse(response, handler);
    } else {
      handler.next(response);
    }
  }

  @override
  void onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) {
    if (kDebugMode) {
      _logger.onError(err, handler);
    } else {
      handler.next(err);
    }
  }
}
