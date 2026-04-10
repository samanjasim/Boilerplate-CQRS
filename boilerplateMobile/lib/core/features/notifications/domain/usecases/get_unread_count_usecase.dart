import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/repositories/notification_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class GetUnreadCountUseCase extends UseCase<int, NoParams> {
  GetUnreadCountUseCase(this._repository);
  final NotificationRepository _repository;

  @override
  Future<Result<int>> call(NoParams input) => _repository.getUnreadCount();
}
