/// Base class for all typed failures in the application.
///
/// Sealed so Dart 3 exhaustive switch checking works: any handler that
/// matches on a `Failure` must cover all subtypes. If a new failure type
/// is added, every existing `switch` breaks at compile time — forcing
/// the developer to handle it.
sealed class Failure {
  const Failure(this.message);
  final String message;

  @override
  String toString() => '${describeType()}: $message';

  /// Override in subclasses if needed; avoids `runtimeType.toString()`
  /// which is unreliable in release builds.
  String describeType() => 'Failure';
}

/// Network unreachable, DNS failure, timeout.
final class NetworkFailure extends Failure {
  const NetworkFailure(super.message);
}

/// 401 — token expired, missing, or invalid. Usually triggers a
/// redirect to the login screen.
final class AuthFailure extends Failure {
  const AuthFailure(super.message);
}

/// 4xx / 5xx from the backend (excluding 401).
final class ServerFailure extends Failure {
  const ServerFailure(this.statusCode, super.message);
  final int statusCode;

  @override
  String describeType() => 'ServerFailure($statusCode)';
}

/// 422 — the backend returned field-level validation errors.
/// [errors] mirrors the shape of `ApiResponse.validationErrors`:
/// `{ "Email": ["Invalid email"], "Password": ["Too short"] }`.
final class ValidationFailure extends Failure {
  const ValidationFailure(super.message, this.errors);
  final Map<String, List<String>> errors;
}

/// Local storage read/write failure (Hive or SecureStorage).
final class CacheFailure extends Failure {
  const CacheFailure(super.message);
}

/// Catch-all for truly unexpected errors.
final class UnknownFailure extends Failure {
  const UnknownFailure(super.message);
}
