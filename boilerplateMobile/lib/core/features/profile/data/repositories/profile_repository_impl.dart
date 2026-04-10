import 'package:boilerplate_mobile/core/error/dio_error_mapper.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/data/dtos/user_dto.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/profile/data/datasources/profile_remote_datasource.dart';
import 'package:boilerplate_mobile/core/features/profile/data/dtos/change_password_request_dto.dart';
import 'package:boilerplate_mobile/core/features/profile/data/dtos/update_profile_request_dto.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/repositories/profile_repository.dart';
import 'package:dio/dio.dart';

class ProfileRepositoryImpl implements ProfileRepository {
  ProfileRepositoryImpl({required ProfileRemoteDataSource remoteDataSource})
      : _remote = remoteDataSource;

  final ProfileRemoteDataSource _remote;

  @override
  Future<Result<User>> getProfile() async {
    try {
      final dto = await _remote.getProfile();
      return Success(dto.toDomain());
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<void>> updateProfile({
    required String userId,
    required String firstName,
    required String lastName,
    required String email,
    String? phoneNumber,
  }) async {
    try {
      await _remote.updateProfile(
        userId,
        UpdateProfileRequestDto(
          firstName: firstName,
          lastName: lastName,
          email: email,
          phoneNumber: phoneNumber,
        ),
      );
      return const Success(null);
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<void>> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    try {
      await _remote.changePassword(
        ChangePasswordRequestDto(
          currentPassword: currentPassword,
          newPassword: newPassword,
          confirmNewPassword: confirmNewPassword,
        ),
      );
      return const Success(null);
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }
}
