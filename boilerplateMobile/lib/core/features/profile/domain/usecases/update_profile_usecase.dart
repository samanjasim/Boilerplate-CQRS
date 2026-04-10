import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/repositories/profile_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class UpdateProfileParams {
  const UpdateProfileParams({
    required this.userId,
    required this.firstName,
    required this.lastName,
    required this.email,
    this.phoneNumber,
  });

  final String userId;
  final String firstName;
  final String lastName;
  final String email;
  final String? phoneNumber;
}

class UpdateProfileUseCase extends UseCase<void, UpdateProfileParams> {
  UpdateProfileUseCase(this._repository);
  final ProfileRepository _repository;

  @override
  Future<Result<void>> call(UpdateProfileParams input) =>
      _repository.updateProfile(
        userId: input.userId,
        firstName: input.firstName,
        lastName: input.lastName,
        email: input.email,
        phoneNumber: input.phoneNumber,
      );
}
