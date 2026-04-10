import 'package:freezed_annotation/freezed_annotation.dart';

part 'login_state.freezed.dart';

/// State for the login page's scoped cubit.
@freezed
sealed class LoginState with _$LoginState {
  const factory LoginState.initial() = LoginInitial;
  const factory LoginState.loading() = LoginLoading;
  const factory LoginState.success() = LoginSuccessState;
  const factory LoginState.requires2FA({
    required String email,
    required String password,
  }) = LoginRequires2FAState;
  const factory LoginState.error(String message) = LoginError;
  const factory LoginState.validationError(
    Map<String, List<String>> errors,
  ) = LoginValidationError;
}
