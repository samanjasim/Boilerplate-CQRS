/// Domain entity for a single notification.
class NotificationItem {
  const NotificationItem({
    required this.id,
    required this.type,
    required this.title,
    required this.message,
    required this.isRead,
    required this.createdAt,
    this.data,
  });

  final String id;
  final String type;
  final String title;
  final String message;
  final String? data;
  final bool isRead;
  final DateTime createdAt;
}
