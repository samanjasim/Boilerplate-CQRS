import 'package:boilerplate_mobile/core/error/result.dart';

/// Abstract base for all use cases.
///
/// A use case encapsulates a single user action. Cubits call use cases;
/// use cases call repositories. This keeps business logic out of both
/// the UI layer and the data layer.
///
/// [Output] — the success type returned inside `Result<Output>`.
/// [Input]  — the parameter type. Use [NoParams] when no input is needed.
// ignore: one_member_abstracts
abstract class UseCase<Output, Input> {
  Future<Result<Output>> call(Input input);
}

/// Marker type for use cases that take no input parameters.
///
/// ```dart
/// class GetUnreadCountUseCase extends UseCase<int, NoParams> {
///   @override
///   Future<Result<int>> call(NoParams _) async { ... }
/// }
/// ```
class NoParams {
  const NoParams();
}
