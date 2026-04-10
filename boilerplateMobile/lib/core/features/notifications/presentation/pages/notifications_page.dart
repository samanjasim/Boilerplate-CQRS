import 'package:boilerplate_mobile/core/features/notifications/domain/entities/notification_item.dart';
import 'package:boilerplate_mobile/core/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:boilerplate_mobile/core/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:boilerplate_mobile/core/widgets/empty_state.dart';
import 'package:boilerplate_mobile/core/widgets/error_view.dart';
import 'package:boilerplate_mobile/core/widgets/loading_view.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class NotificationsPage extends StatefulWidget {
  const NotificationsPage({super.key});

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  @override
  void initState() {
    super.initState();
    context.read<NotificationsCubit>().loadNotifications();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          BlocBuilder<NotificationsCubit, NotificationsState>(
            builder: (context, state) {
              if (state is NotificationsLoaded && state.unreadCount > 0) {
                return TextButton(
                  onPressed: () =>
                      context.read<NotificationsCubit>().markAllAsRead(),
                  child: const Text('Mark all read'),
                );
              }
              return const SizedBox.shrink();
            },
          ),
        ],
      ),
      body: BlocBuilder<NotificationsCubit, NotificationsState>(
        builder: (context, state) => switch (state) {
          NotificationsInitial() || NotificationsLoading() =>
            const LoadingView(message: 'Loading notifications...'),
          NotificationsError(:final message) => ErrorView(
              message: message,
              onRetry: () =>
                  context.read<NotificationsCubit>().loadNotifications(),
            ),
          NotificationsLoaded(
            :final notifications,
            :final pagination,
            :final isLoadingMore,
          ) =>
            notifications.isEmpty
                ? const EmptyState(
                    icon: Icons.notifications_none,
                    title: 'No notifications',
                    description: "You're all caught up!",
                  )
                : RefreshIndicator(
                    onRefresh: () =>
                        context.read<NotificationsCubit>().loadNotifications(),
                    child: ListView.builder(
                      itemCount:
                          notifications.length + (isLoadingMore ? 1 : 0),
                      itemBuilder: (context, index) {
                        if (index == notifications.length) {
                          return const Padding(
                            padding: EdgeInsets.all(16),
                            child:
                                Center(child: CircularProgressIndicator()),
                          );
                        }
                        return _NotificationTile(
                          notification: notifications[index],
                          onTap: () {
                            if (!notifications[index].isRead) {
                              context
                                  .read<NotificationsCubit>()
                                  .markAsRead(notifications[index].id);
                            }
                          },
                        );
                      },
                      // Trigger load more when near the bottom.
                      controller: _ScrollEndListener(
                        onEnd: pagination.hasNextPage && !isLoadingMore
                            ? () => context
                                .read<NotificationsCubit>()
                                .loadMore()
                            : null,
                      ),
                    ),
                  ),
        },
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({
    required this.notification,
    required this.onTap,
  });

  final NotificationItem notification;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isUnread = !notification.isRead;

    return ListTile(
      onTap: onTap,
      tileColor: isUnread
          ? theme.colorScheme.primaryContainer.withValues(alpha: 0.15)
          : null,
      leading: CircleAvatar(
        backgroundColor: isUnread
            ? theme.colorScheme.primary
            : theme.colorScheme.surfaceContainerHighest,
        child: Icon(
          _iconForType(notification.type),
          color: isUnread
              ? theme.colorScheme.onPrimary
              : theme.colorScheme.onSurfaceVariant,
          size: 20,
        ),
      ),
      title: Text(
        notification.title,
        style: theme.textTheme.bodyLarge?.copyWith(
          fontWeight: isUnread ? FontWeight.w600 : FontWeight.w400,
        ),
      ),
      subtitle: Text(
        notification.message,
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        style: theme.textTheme.bodySmall,
      ),
      trailing: Text(
        _timeAgo(notification.createdAt),
        style: theme.textTheme.bodySmall?.copyWith(
          color: theme.colorScheme.onSurfaceVariant,
        ),
      ),
    );
  }

  IconData _iconForType(String type) => switch (type.toLowerCase()) {
        'user' || 'account' => Icons.person_outline,
        'file' || 'upload' => Icons.file_present_outlined,
        'billing' || 'payment' => Icons.payment_outlined,
        'security' || 'auth' => Icons.security_outlined,
        'system' || 'setting' => Icons.settings_outlined,
        _ => Icons.notifications_outlined,
      };

  String _timeAgo(DateTime dateTime) {
    final diff = DateTime.now().difference(dateTime);
    if (diff.inMinutes < 1) return 'now';
    if (diff.inMinutes < 60) return '${diff.inMinutes}m';
    if (diff.inHours < 24) return '${diff.inHours}h';
    if (diff.inDays < 7) return '${diff.inDays}d';
    return '${(diff.inDays / 7).floor()}w';
  }
}

/// A simple scroll controller that calls [onEnd] when the user
/// scrolls near the bottom.
class _ScrollEndListener extends ScrollController {
  _ScrollEndListener({this.onEnd}) {
    addListener(_listener);
  }

  final VoidCallback? onEnd;

  void _listener() {
    if (onEnd == null) return;
    if (position.pixels >= position.maxScrollExtent - 200) {
      onEnd!();
    }
  }

  @override
  void dispose() {
    removeListener(_listener);
    super.dispose();
  }
}
