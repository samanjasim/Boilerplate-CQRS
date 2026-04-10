import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'auth_state.freezed.dart';

/// Global authentication state.
///
/// The router's auth guard listens to this and redirects:
/// - `initial` → splash / loading screen
/// - `authenticated` → main shell
/// - `unauthenticated` → login page
@freezed
sealed class AuthState with _$AuthState {
  const factory AuthState.initial() = AuthInitial;
  const factory AuthState.authenticated({
    required User user,
    required Set<String> permissions,
  }) = AuthAuthenticated;
  const factory AuthState.unauthenticated() = AuthUnauthenticated;
}
