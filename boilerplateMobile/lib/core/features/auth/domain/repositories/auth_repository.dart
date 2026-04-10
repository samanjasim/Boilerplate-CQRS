import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';

/// Abstract contract for auth operations.
///
/// The domain layer defines this interface; the data layer implements
/// it. Cubits and use cases depend only on this abstraction.
abstract class AuthRepository {
  /// Authenticate with email/password. Optionally include a
  /// [twoFactorCode] if the previous attempt returned
  /// [LoginRequires2FA].
  Future<Result<LoginResult>> login({
    required String email,
    required String password,
    String? twoFactorCode,
  });

  /// Exchange a refresh token for a new access/refresh pair.
  Future<Result<AuthSession>> refreshToken(String refreshToken);

  /// Fetch the currently authenticated user's profile.
  Future<Result<User>> getCurrentUser();

  /// Clear stored tokens and session state (local-only, no BE call).
  Future<Result<void>> logout();

  /// Check if a stored session exists (tokens present in secure storage).
  Future<bool> hasSession();
}
