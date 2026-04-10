import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';

class LoginParams {
  const LoginParams({
    required this.email,
    required this.password,
    this.twoFactorCode,
  });

  final String email;
  final String password;
  final String? twoFactorCode;
}

class LoginUseCase extends UseCase<LoginResult, LoginParams> {
  LoginUseCase(this._repository);
  final AuthRepository _repository;

  @override
  Future<Result<LoginResult>> call(LoginParams input) =>
      _repository.login(
        email: input.email,
        password: input.password,
        twoFactorCode: input.twoFactorCode,
      );
}
