import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class GetCurrentUserUseCase extends UseCase<User, NoParams> {
  GetCurrentUserUseCase(this._repository);
  final AuthRepository _repository;

  @override
  Future<Result<User>> call(NoParams input) => _repository.getCurrentUser();
}
