import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/repositories/notification_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class MarkNotificationReadUseCase extends UseCase<void, String> {
  MarkNotificationReadUseCase(this._repository);
  final NotificationRepository _repository;

  @override
  Future<Result<void>> call(String id) => _repository.markAsRead(id);
}
