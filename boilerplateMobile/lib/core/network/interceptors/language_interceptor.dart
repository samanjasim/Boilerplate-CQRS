import 'dart:ui';

import 'package:dio/dio.dart';

/// Sets the `Accept-Language` header based on the device locale.
class LanguageInterceptor extends Interceptor {
  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) {
    final locale = PlatformDispatcher.instance.locale;
    options.headers['Accept-Language'] = locale.toLanguageTag();
    handler.next(options);
  }
}
