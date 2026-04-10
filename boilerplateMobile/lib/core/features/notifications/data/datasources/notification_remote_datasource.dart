import 'package:boilerplate_mobile/core/features/notifications/data/dtos/notification_dto.dart';
import 'package:boilerplate_mobile/core/network/api_response.dart';
import 'package:dio/dio.dart';

class NotificationRemoteDataSource {
  NotificationRemoteDataSource(this._dio);
  final Dio _dio;

  Future<PaginatedResponse<NotificationDto>> getNotifications({
    required int pageNumber,
    required int pageSize,
    bool? isRead,
  }) async {
    final queryParams = <String, dynamic>{
      'pageNumber': pageNumber,
      'pageSize': pageSize,
    };
    if (isRead != null) queryParams['isRead'] = isRead;

    final response = await _dio.get<Map<String, dynamic>>(
      '/Notifications',
      queryParameters: queryParams,
    );
    return PaginatedResponse<NotificationDto>.fromJson(
      response.data!,
      (json) => NotificationDto.fromJson(json! as Map<String, dynamic>),
    );
  }

  Future<int> getUnreadCount() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/Notifications/unread-count',
    );
    final apiResponse = ApiResponse<int>.fromJson(
      response.data!,
      (json) => json! as int,
    );
    return apiResponse.data ?? 0;
  }

  Future<void> markAsRead(String id) async {
    await _dio.post<void>('/Notifications/$id/read');
  }

  Future<void> markAllAsRead() async {
    await _dio.post<void>('/Notifications/read-all');
  }
}
