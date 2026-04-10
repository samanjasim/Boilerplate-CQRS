import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/repositories/profile_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class GetProfileUseCase extends UseCase<User, NoParams> {
  GetProfileUseCase(this._repository);
  final ProfileRepository _repository;

  @override
  Future<Result<User>> call(NoParams input) => _repository.getProfile();
}
