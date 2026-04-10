import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/get_notifications_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/get_unread_count_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/mark_all_read_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/usecases/mark_notification_read_usecase.dart';
import 'package:boilerplate_mobile/core/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class NotificationsCubit extends Cubit<NotificationsState> {
  NotificationsCubit({
    required GetNotificationsUseCase getNotifications,
    required GetUnreadCountUseCase getUnreadCount,
    required MarkNotificationReadUseCase markRead,
    required MarkAllReadUseCase markAllRead,
  })  : _getNotifications = getNotifications,
        _getUnreadCount = getUnreadCount,
        _markRead = markRead,
        _markAllRead = markAllRead,
        super(const NotificationsState.initial());

  final GetNotificationsUseCase _getNotifications;
  final GetUnreadCountUseCase _getUnreadCount;
  final MarkNotificationReadUseCase _markRead;
  final MarkAllReadUseCase _markAllRead;

  Future<void> loadNotifications() async {
    emit(const NotificationsState.loading());

    final notifResult =
        await _getNotifications(const GetNotificationsParams());
    final countResult = await _getUnreadCount(const NoParams());

    switch (notifResult) {
      case Success(value: final response):
        final unread = switch (countResult) {
          Success(value: final c) => c,
          _ => 0,
        };
        emit(
          NotificationsState.loaded(
            notifications: response.data,
            pagination: response.pagination,
            unreadCount: unread,
          ),
        );
      case Err(failure: final f):
        emit(NotificationsState.error(f.message));
    }
  }

  Future<void> loadMore() async {
    final current = state;
    if (current is! NotificationsLoaded || !current.pagination.hasNextPage) {
      return;
    }

    emit(current.copyWith(isLoadingMore: true));

    final result = await _getNotifications(
      GetNotificationsParams(
        pageNumber: current.pagination.pageNumber + 1,
      ),
    );

    switch (result) {
      case Success(value: final response):
        emit(
          NotificationsState.loaded(
            notifications: [...current.notifications, ...response.data],
            pagination: response.pagination,
            unreadCount: current.unreadCount,
          ),
        );
      case Err():
        // Keep current data on load-more failure.
        emit(current.copyWith(isLoadingMore: false));
    }
  }

  Future<void> markAsRead(String id) async {
    final result = await _markRead(id);
    if (result.isSuccess) {
      await _refreshAfterAction();
    }
  }

  Future<void> markAllAsRead() async {
    final result = await _markAllRead(const NoParams());
    if (result.isSuccess) {
      await _refreshAfterAction();
    }
  }

  /// Refresh the unread count — called on app foreground.
  Future<void> refreshUnreadCount() async {
    final result = await _getUnreadCount(const NoParams());
    final current = state;
    if (result is Success<int> && current is NotificationsLoaded) {
      emit(current.copyWith(unreadCount: result.value));
    }
  }

  Future<void> _refreshAfterAction() async {
    // Re-fetch first page and unread count.
    await loadNotifications();
  }
}
