import 'package:boilerplate_mobile/core/error/dio_error_mapper.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/notifications/data/datasources/notification_remote_datasource.dart';
import 'package:boilerplate_mobile/core/features/notifications/data/dtos/notification_dto.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/entities/notification_item.dart';
import 'package:boilerplate_mobile/core/features/notifications/domain/repositories/notification_repository.dart';
import 'package:boilerplate_mobile/core/network/api_response.dart';
import 'package:dio/dio.dart';

class NotificationRepositoryImpl implements NotificationRepository {
  NotificationRepositoryImpl({
    required NotificationRemoteDataSource remoteDataSource,
  }) : _remote = remoteDataSource;

  final NotificationRemoteDataSource _remote;

  @override
  Future<Result<PaginatedResponse<NotificationItem>>> getNotifications({
    int pageNumber = 1,
    int pageSize = 20,
    bool? isRead,
  }) async {
    try {
      final response = await _remote.getNotifications(
        pageNumber: pageNumber,
        pageSize: pageSize,
        isRead: isRead,
      );

      // Map DTOs to domain entities while preserving pagination metadata.
      final domainItems =
          response.data.map((dto) => dto.toDomain()).toList();

      return Success(
        PaginatedResponse<NotificationItem>(
          pagination: response.pagination,
          data: domainItems,
          success: response.success,
          message: response.message,
        ),
      );
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<int>> getUnreadCount() async {
    try {
      final count = await _remote.getUnreadCount();
      return Success(count);
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<void>> markAsRead(String id) async {
    try {
      await _remote.markAsRead(id);
      return const Success(null);
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<void>> markAllAsRead() async {
    try {
      await _remote.markAllAsRead();
      return const Success(null);
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }
}
