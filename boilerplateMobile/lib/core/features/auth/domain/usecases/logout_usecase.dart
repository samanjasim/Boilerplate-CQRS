import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class LogoutUseCase extends UseCase<void, NoParams> {
  LogoutUseCase(this._repository);
  final AuthRepository _repository;

  @override
  Future<Result<void>> call(NoParams input) => _repository.logout();
}
