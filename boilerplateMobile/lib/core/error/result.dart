import 'package:boilerplate_mobile/core/error/failure.dart';

/// A monadic result type — every repository and use case returns
/// `Result<T>` instead of throwing. This forces callers to handle both
/// success and error paths explicitly.
///
/// Usage:
/// ```dart
/// switch (await loginUseCase(params)) {
///   case Success(value: final session):
///     emit(LoginState.success(session));
///   case Err(failure: NetworkFailure()):
///     emit(LoginState.error('No internet'));
///   case Err(failure: final f):
///     emit(LoginState.error(f.message));
/// }
/// ```
sealed class Result<T> {
  const Result();

  /// True when this is a [Success] variant.
  bool get isSuccess => this is Success<T>;

  /// True when this is an [Err] variant.
  bool get isError => this is Err<T>;

  /// Extract the value if [Success], or call [onError] for [Err].
  T getOrElse(T Function(Failure failure) onError) => switch (this) {
        Success(value: final v) => v,
        Err(failure: final f) => onError(f),
      };

  /// Map the success value, pass failures through unchanged.
  Result<R> map<R>(R Function(T value) transform) => switch (this) {
        Success(value: final v) => Success(transform(v)),
        Err(failure: final f) => Err(f),
      };

  /// Chain another async operation that also returns a Result.
  Future<Result<R>> flatMap<R>(
    Future<Result<R>> Function(T value) transform,
  ) async =>
      switch (this) {
        Success(value: final v) => transform(v),
        Err(failure: final f) => Err(f),
      };
}

/// The operation succeeded with [value].
final class Success<T> extends Result<T> {
  const Success(this.value);
  final T value;

  @override
  String toString() => 'Success($value)';
}

/// The operation failed with a typed [failure].
final class Err<T> extends Result<T> {
  const Err(this.failure);
  final Failure failure;

  @override
  String toString() => 'Err($failure)';
}
