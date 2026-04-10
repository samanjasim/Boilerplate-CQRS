import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Global singleton cubit tracking the authentication state.
///
/// Registered as a singleton in DI. The router's auth guard and the
/// `MainShellPage` both read from this cubit.
///
/// Lifecycle:
/// 1. App starts → [checkSession] called from bootstrap
/// 2. If tokens exist → calls `getCurrentUser` → `authenticated`
/// 3. Login page calls `onLoginSuccess` → `authenticated`
/// 4. Logout calls `logout` → `unauthenticated`
/// 5. Refresh interceptor failure → `sessionExpired` → `unauthenticated`
class AuthCubit extends Cubit<AuthState> {
  AuthCubit(this._repository) : super(const AuthState.initial());

  final AuthRepository _repository;

  /// Check if there's a saved session on app start.
  Future<void> checkSession() async {
    final hasSession = await _repository.hasSession();
    if (!hasSession) {
      emit(const AuthState.unauthenticated());
      return;
    }

    // Try fetching the user to validate the session.
    final result = await _repository.getCurrentUser();
    switch (result) {
      case Success(value: final user):
        emit(AuthState.authenticated(
          user: user,
          permissions: user.permissions,
        ),);
      case Err():
        // Tokens exist but are invalid — force re-login.
        await _repository.logout();
        emit(const AuthState.unauthenticated());
    }
  }

  /// Called by LoginCubit after a successful login.
  void onLoginSuccess(AuthSession session) {
    emit(AuthState.authenticated(
      user: session.user,
      permissions: session.user.permissions,
    ),);
  }

  /// Called when the refresh interceptor fails — token is irrecoverable.
  void sessionExpired() {
    emit(const AuthState.unauthenticated());
  }

  /// User-initiated logout.
  Future<void> logout() async {
    await _repository.logout();
    emit(const AuthState.unauthenticated());
  }

  /// Refresh the user data (e.g. after profile edit).
  Future<void> refreshUser() async {
    final result = await _repository.getCurrentUser();
    switch (result) {
      case Success(value: final user):
        emit(AuthState.authenticated(
          user: user,
          permissions: user.permissions,
        ),);
      case Err():
        break; // Keep current state on failure.
    }
  }
}
