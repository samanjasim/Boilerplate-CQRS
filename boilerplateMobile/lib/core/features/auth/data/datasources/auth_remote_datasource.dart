import 'package:boilerplate_mobile/core/features/auth/data/dtos/login_request_dto.dart';
import 'package:boilerplate_mobile/core/features/auth/data/dtos/login_response_dto.dart';
import 'package:boilerplate_mobile/core/features/auth/data/dtos/user_dto.dart';
import 'package:dio/dio.dart';

/// Raw Dio calls against the BE auth endpoints.
///
/// Every BE response is wrapped in `ApiResponse<T>`: `{ "data": <T>, "success": true }`.
/// We unwrap the envelope here so the repository gets clean DTOs.
class AuthRemoteDataSource {
  AuthRemoteDataSource(this._dio);
  final Dio _dio;

  Future<LoginResponseDto> login(LoginRequestDto request) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/Auth/login',
      data: request.toJson(),
    );
    final envelope = response.data!;
    final inner = envelope['data'] as Map<String, dynamic>;
    return LoginResponseDto.fromJson(inner);
  }

  Future<LoginResponseDto> refreshToken(String refreshToken) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/Auth/refresh-token',
      data: {'refreshToken': refreshToken},
    );
    final envelope = response.data!;
    final inner = envelope['data'] as Map<String, dynamic>;
    return LoginResponseDto.fromJson(inner);
  }

  Future<UserDto> getCurrentUser() async {
    final response = await _dio.get<Map<String, dynamic>>('/Auth/me');
    final envelope = response.data!;
    final inner = envelope['data'] as Map<String, dynamic>;
    return UserDto.fromJson(inner);
  }
}
