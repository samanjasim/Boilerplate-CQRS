import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';

/// Represents a fully authenticated session with tokens and user info.
class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.refreshToken,
    required this.user,
    this.expiresAt,
  });

  final String accessToken;
  final String refreshToken;
  final User user;
  final DateTime? expiresAt;
}

/// Login result — either a full session or a 2FA challenge.
sealed class LoginResult {
  const LoginResult();
}

/// Login succeeded — full session available.
final class LoginSuccess extends LoginResult {
  const LoginSuccess(this.session);
  final AuthSession session;
}

/// Login requires 2FA verification before completing.
final class LoginRequires2FA extends LoginResult {
  const LoginRequires2FA({required this.email, required this.password});

  /// Cached credentials to re-send with the 2FA code.
  final String email;
  final String password;
}
