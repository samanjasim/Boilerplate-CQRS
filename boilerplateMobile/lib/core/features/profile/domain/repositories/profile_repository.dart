import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';

abstract class ProfileRepository {
  Future<Result<User>> getProfile();

  Future<Result<void>> updateProfile({
    required String userId,
    required String firstName,
    required String lastName,
    required String email,
    String? phoneNumber,
  });

  Future<Result<void>> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  });
}
