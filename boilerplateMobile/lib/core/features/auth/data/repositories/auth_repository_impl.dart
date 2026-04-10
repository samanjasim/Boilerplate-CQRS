import 'package:boilerplate_mobile/core/error/dio_error_mapper.dart';
import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/data/datasources/auth_remote_datasource.dart';
import 'package:boilerplate_mobile/core/features/auth/data/dtos/login_request_dto.dart';
import 'package:boilerplate_mobile/core/features/auth/data/dtos/user_dto.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/storage/hive_service.dart';
import 'package:boilerplate_mobile/core/storage/secure_storage_service.dart';
import 'package:dio/dio.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({
    required AuthRemoteDataSource remoteDataSource,
    required SecureStorageService secureStorage,
    required HiveService hiveService,
  })  : _remote = remoteDataSource,
        _secureStorage = secureStorage,
        _hiveService = hiveService;

  final AuthRemoteDataSource _remote;
  final SecureStorageService _secureStorage;
  final HiveService _hiveService;

  @override
  Future<Result<LoginResult>> login({
    required String email,
    required String password,
    String? twoFactorCode,
  }) async {
    try {
      final response = await _remote.login(
        LoginRequestDto(
          email: email,
          password: password,
          twoFactorCode: twoFactorCode,
        ),
      );

      if (response.requiresTwoFactor) {
        return Success(
          LoginRequires2FA(email: email, password: password),
        );
      }

      if (response.accessToken == null || response.refreshToken == null) {
        return const Err(AuthFailure('Login failed — no tokens received'));
      }

      await _secureStorage.saveTokens(
        accessToken: response.accessToken!,
        refreshToken: response.refreshToken!,
      );

      final user = response.user!.toDomain();

      // Cache tenant ID for the TenantInterceptor.
      if (user.tenantId != null) {
        await _secureStorage.saveTenantId(user.tenantId!);
      }

      // Cache user profile in Hive for offline reads.
      await _hiveService.put('current_user', response.user!.toJson());

      return Success(
        LoginSuccess(
          AuthSession(
            accessToken: response.accessToken!,
            refreshToken: response.refreshToken!,
            user: user,
            expiresAt: response.expiresAt != null
                ? DateTime.tryParse(response.expiresAt!)
                : null,
          ),
        ),
      );
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<AuthSession>> refreshToken(String refreshToken) async {
    try {
      final response = await _remote.refreshToken(refreshToken);

      if (response.accessToken == null || response.refreshToken == null) {
        return const Err(AuthFailure('Refresh failed — no tokens received'));
      }

      await _secureStorage.saveTokens(
        accessToken: response.accessToken!,
        refreshToken: response.refreshToken!,
      );

      final user = response.user!.toDomain();

      return Success(
        AuthSession(
          accessToken: response.accessToken!,
          refreshToken: response.refreshToken!,
          user: user,
          expiresAt: response.expiresAt != null
              ? DateTime.tryParse(response.expiresAt!)
              : null,
        ),
      );
    } on DioException catch (e) {
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<User>> getCurrentUser() async {
    try {
      final dto = await _remote.getCurrentUser();
      final user = dto.toDomain();

      // Update cached user.
      await _hiveService.put('current_user', dto.toJson());

      return Success(user);
    } on DioException catch (e) {
      // If offline, try returning cached user.
      if (e.type == DioExceptionType.connectionError ||
          e.type == DioExceptionType.connectionTimeout) {
        final cached = _hiveService.get('current_user');
        if (cached != null) {
          return Success(UserDto.fromJson(cached).toDomain());
        }
      }
      return Err(mapDioException(e));
    }
  }

  @override
  Future<Result<void>> logout() async {
    try {
      await _secureStorage.clearTokens();
      await _hiveService.clear();
      return const Success(null);
    } catch (e) {
      return Err(CacheFailure('Failed to clear session: $e'));
    }
  }

  @override
  Future<bool> hasSession() => _secureStorage.hasSession();
}
