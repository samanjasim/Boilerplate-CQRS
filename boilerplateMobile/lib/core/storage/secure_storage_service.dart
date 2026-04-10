import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Typed wrapper around [FlutterSecureStorage] for auth-related secrets.
///
/// All token operations go through this service so the Dio interceptors
/// and the AuthCubit never touch the raw storage API.
class SecureStorageService {
  SecureStorageService({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const _keyAccessToken = 'access_token';
  static const _keyRefreshToken = 'refresh_token';
  static const _keyTenantId = 'tenant_id';

  // --- Access token ---

  Future<String?> getAccessToken() => _storage.read(key: _keyAccessToken);

  // --- Refresh token ---

  Future<String?> getRefreshToken() => _storage.read(key: _keyRefreshToken);

  // --- Tenant ID ---

  Future<String?> getTenantId() => _storage.read(key: _keyTenantId);

  Future<void> saveTenantId(String tenantId) =>
      _storage.write(key: _keyTenantId, value: tenantId);

  // --- Bulk operations ---

  /// Save both tokens at once after a login or token refresh.
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await Future.wait([
      _storage.write(key: _keyAccessToken, value: accessToken),
      _storage.write(key: _keyRefreshToken, value: refreshToken),
    ]);
  }

  /// Clear all auth state on logout or session expiry.
  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: _keyAccessToken),
      _storage.delete(key: _keyRefreshToken),
      _storage.delete(key: _keyTenantId),
    ]);
  }

  /// Whether any saved session exists (used by the splash/guard to
  /// decide initial route).
  Future<bool> hasSession() async {
    final token = await getAccessToken();
    return token != null && token.isNotEmpty;
  }
}
