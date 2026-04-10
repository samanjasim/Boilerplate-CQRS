import 'package:boilerplate_mobile/core/features/notifications/domain/entities/notification_item.dart';
import 'package:boilerplate_mobile/core/network/api_response.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'notifications_state.freezed.dart';

@freezed
sealed class NotificationsState with _$NotificationsState {
  const factory NotificationsState.initial() = NotificationsInitial;
  const factory NotificationsState.loading() = NotificationsLoading;
  const factory NotificationsState.loaded({
    required List<NotificationItem> notifications,
    required PaginationMeta pagination,
    required int unreadCount,
    @Default(false) bool isLoadingMore,
  }) = NotificationsLoaded;
  const factory NotificationsState.error(String message) = NotificationsError;
}
