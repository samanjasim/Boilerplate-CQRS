import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:dio/dio.dart';

/// Maps a [DioException] to a typed [Failure].
///
/// Called by repositories in their catch blocks so presentation-layer
/// code never sees a DioException — only sealed Failure subtypes.
Failure mapDioException(DioException e) {
  return switch (e.type) {
    DioExceptionType.connectionTimeout ||
    DioExceptionType.sendTimeout ||
    DioExceptionType.receiveTimeout =>
      const NetworkFailure('Connection timed out'),
    DioExceptionType.connectionError =>
      const NetworkFailure('No internet connection'),
    DioExceptionType.badCertificate =>
      const NetworkFailure('Certificate verification failed'),
    DioExceptionType.cancel => const NetworkFailure('Request was cancelled'),
    DioExceptionType.badResponse => _mapBadResponse(e.response),
    DioExceptionType.unknown =>
      NetworkFailure(e.message ?? 'Unknown network error'),
  };
}

Failure _mapBadResponse(Response<dynamic>? response) {
  if (response == null) {
    return const ServerFailure(0, 'No response from server');
  }

  final statusCode = response.statusCode ?? 0;
  final data = response.data;

  // Try to extract the backend's ApiResponse envelope.
  if (data is Map<String, dynamic>) {
    final message = data['message'] as String? ?? 'An error occurred';

    // 401 → auth failure (refresh interceptor handles retry;
    // if we still get here it means refresh also failed).
    if (statusCode == 401) {
      return AuthFailure(message);
    }

    // 422 → validation errors from the backend.
    if (statusCode == 422) {
      final rawErrors = data['validationErrors'] as Map<String, dynamic>?;
      final errors = rawErrors?.map(
            (key, value) => MapEntry(
              key,
              (value as List<dynamic>).cast<String>(),
            ),
          ) ??
          {};
      return ValidationFailure(message, errors);
    }

    return ServerFailure(statusCode, message);
  }

  return ServerFailure(statusCode, 'Server error ($statusCode)');
}
