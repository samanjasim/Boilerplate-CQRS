import 'package:boilerplate_mobile/core/features/auth/data/dtos/user_dto.dart';
import 'package:boilerplate_mobile/core/features/profile/data/dtos/change_password_request_dto.dart';
import 'package:boilerplate_mobile/core/features/profile/data/dtos/update_profile_request_dto.dart';
import 'package:dio/dio.dart';

class ProfileRemoteDataSource {
  ProfileRemoteDataSource(this._dio);
  final Dio _dio;

  Future<UserDto> getProfile() async {
    final response = await _dio.get<Map<String, dynamic>>('/Auth/me');
    final envelope = response.data!;
    final inner = envelope['data'] as Map<String, dynamic>;
    return UserDto.fromJson(inner);
  }

  Future<void> updateProfile(
    String userId,
    UpdateProfileRequestDto request,
  ) async {
    await _dio.put<void>('/Users/$userId', data: request.toJson());
  }

  Future<void> changePassword(ChangePasswordRequestDto request) async {
    await _dio.post<void>('/Auth/change-password', data: request.toJson());
  }
}
