import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/entities/notification_item.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/repositories/notification_repository.dart';
import 'package:boilerplate_mobile/core/network/api_response.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class GetNotificationsParams {
  const GetNotificationsParams({
    this.pageNumber = 1,
    this.pageSize = 20,
    this.isRead,
  });

  final int pageNumber;
  final int pageSize;
  final bool? isRead;
}

class GetNotificationsUseCase
    extends UseCase<PaginatedResponse<NotificationItem>,
        GetNotificationsParams> {
  GetNotificationsUseCase(this._repository);
  final NotificationRepository _repository;

  @override
  Future<Result<PaginatedResponse<NotificationItem>>> call(
    GetNotificationsParams input,
  ) =>
      _repository.getNotifications(
        pageNumber: input.pageNumber,
        pageSize: input.pageSize,
        isRead: input.isRead,
      );
}
