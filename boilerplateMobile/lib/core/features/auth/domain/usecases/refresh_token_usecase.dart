import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class RefreshTokenUseCase extends UseCase<AuthSession, String> {
  RefreshTokenUseCase(this._repository);
  final AuthRepository _repository;

  @override
  Future<Result<AuthSession>> call(String refreshToken) =>
      _repository.refreshToken(refreshToken);
}
