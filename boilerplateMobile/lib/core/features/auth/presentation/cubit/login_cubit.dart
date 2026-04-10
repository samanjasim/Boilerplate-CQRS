import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/usecases/login_usecase.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/login_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Scoped cubit for the login page. Created per page instance,
/// disposed when the page is popped.
class LoginCubit extends Cubit<LoginState> {
  LoginCubit({
    required LoginUseCase loginUseCase,
    required AuthCubit authCubit,
  })  : _loginUseCase = loginUseCase,
        _authCubit = authCubit,
        super(const LoginState.initial());

  final LoginUseCase _loginUseCase;
  final AuthCubit _authCubit;

  Future<void> login({
    required String email,
    required String password,
    String? twoFactorCode,
  }) async {
    emit(const LoginState.loading());

    final result = await _loginUseCase(
      LoginParams(
        email: email,
        password: password,
        twoFactorCode: twoFactorCode,
      ),
    );

    switch (result) {
      case Success(value: LoginSuccess(session: final session)):
        _authCubit.onLoginSuccess(session);
        emit(const LoginState.success());
      case Success(value: LoginRequires2FA(:final email, :final password)):
        emit(LoginState.requires2FA(email: email, password: password));
      case Err(failure: ValidationFailure(:final errors)):
        emit(LoginState.validationError(errors));
      case Err(failure: final f):
        emit(LoginState.error(f.message));
    }
  }

  /// Re-submit login with the 2FA code after receiving `requires2FA`.
  Future<void> verify2FA({
    required String email,
    required String password,
    required String code,
  }) async {
    await login(email: email, password: password, twoFactorCode: code);
  }
}
