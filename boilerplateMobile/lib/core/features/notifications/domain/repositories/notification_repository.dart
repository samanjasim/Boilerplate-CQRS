import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/entities/notification_item.dart';
import 'package:boilerplate_mobile/core/network/api_response.dart';

abstract class NotificationRepository {
  Future<Result<PaginatedResponse<NotificationItem>>> getNotifications({
    int pageNumber = 1,
    int pageSize = 20,
    bool? isRead,
  });

  Future<Result<int>> getUnreadCount();

  Future<Result<void>> markAsRead(String id);

  Future<Result<void>> markAllAsRead();
}
