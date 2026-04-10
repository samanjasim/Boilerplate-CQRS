import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/repositories/profile_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class ChangePasswordParams {
  const ChangePasswordParams({
    required this.currentPassword,
    required this.newPassword,
    required this.confirmNewPassword,
  });

  final String currentPassword;
  final String newPassword;
  final String confirmNewPassword;
}

class ChangePasswordUseCase extends UseCase<void, ChangePasswordParams> {
  ChangePasswordUseCase(this._repository);
  final ProfileRepository _repository;

  @override
  Future<Result<void>> call(ChangePasswordParams input) =>
      _repository.changePassword(
        currentPassword: input.currentPassword,
        newPassword: input.newPassword,
        confirmNewPassword: input.confirmNewPassword,
      );
}
