import 'package:boilerplate_mobile/core/features/notifications/domain/entities/notification_item.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'notification_dto.freezed.dart';
part 'notification_dto.g.dart';

@freezed
abstract class NotificationDto with _$NotificationDto {
  const factory NotificationDto({
    required String id,
    required String type,
    required String title,
    required String message,
    required String createdAt,
    String? data,
    @Default(false) bool isRead,
  }) = _NotificationDto;

  factory NotificationDto.fromJson(Map<String, dynamic> json) =>
      _$NotificationDtoFromJson(json);
}

extension NotificationDtoMapper on NotificationDto {
  NotificationItem toDomain() => NotificationItem(
        id: id,
        type: type,
        title: title,
        message: message,
        data: data,
        isRead: isRead,
        createdAt: DateTime.parse(createdAt),
      );
}
