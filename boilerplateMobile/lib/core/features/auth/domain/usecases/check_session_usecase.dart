import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class CheckSessionUseCase extends UseCase<bool, NoParams> {
  CheckSessionUseCase(this._repository);
  final AuthRepository _repository;

  @override
  Future<Result<bool>> call(NoParams input) async {
    final has = await _repository.hasSession();
    return Success(has);
  }
}
