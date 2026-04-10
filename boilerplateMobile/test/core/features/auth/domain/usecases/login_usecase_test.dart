import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/usecases/login_usecase.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late MockAuthRepository repository;
  late LoginUseCase useCase;

  setUp(() {
    repository = MockAuthRepository();
    useCase = LoginUseCase(repository);
  });

  final testUser = User(
    id: '1',
    email: 'test@test.com',
    firstName: 'Test',
    lastName: 'User',
    roles: ['Admin'],
    permissions: {'Users.View'},
    createdAt: DateTime(2024),
  );

  final testSession = AuthSession(
    accessToken: 'access',
    refreshToken: 'refresh',
    user: testUser,
  );

  group('LoginUseCase', () {
    test('returns LoginSuccess on successful login', () async {
      when(
        () => repository.login(
          email: any(named: 'email'),
          password: any(named: 'password'),
          twoFactorCode: any(named: 'twoFactorCode'),
        ),
      ).thenAnswer((_) async => Success(LoginSuccess(testSession)));

      final result = await useCase(
        const LoginParams(email: 'test@test.com', password: 'pass'),
      );

      expect(result, isA<Success<LoginResult>>());
      final login = (result as Success<LoginResult>).value;
      expect(login, isA<LoginSuccess>());
      expect((login as LoginSuccess).session.user.email, 'test@test.com');
    });

    test('returns LoginRequires2FA when 2FA needed', () async {
      when(
        () => repository.login(
          email: any(named: 'email'),
          password: any(named: 'password'),
          twoFactorCode: any(named: 'twoFactorCode'),
        ),
      ).thenAnswer(
        (_) async => Success(
          LoginRequires2FA(email: 'test@test.com', password: 'pass'),
        ),
      );

      final result = await useCase(
        const LoginParams(email: 'test@test.com', password: 'pass'),
      );

      expect(result, isA<Success<LoginResult>>());
      final login = (result as Success<LoginResult>).value;
      expect(login, isA<LoginRequires2FA>());
    });

    test('returns Err on failure', () async {
      when(
        () => repository.login(
          email: any(named: 'email'),
          password: any(named: 'password'),
          twoFactorCode: any(named: 'twoFactorCode'),
        ),
      ).thenAnswer(
        (_) async => Err(AuthFailure('Invalid credentials')),
      );

      final result = await useCase(
        const LoginParams(email: 'test@test.com', password: 'wrong'),
      );

      expect(result, isA<Err<LoginResult>>());
      expect(
        (result as Err<LoginResult>).failure,
        isA<AuthFailure>(),
      );
    });

    test('passes twoFactorCode to repository', () async {
      when(
        () => repository.login(
          email: any(named: 'email'),
          password: any(named: 'password'),
          twoFactorCode: any(named: 'twoFactorCode'),
        ),
      ).thenAnswer((_) async => Success(LoginSuccess(testSession)));

      await useCase(
        const LoginParams(
          email: 'test@test.com',
          password: 'pass',
          twoFactorCode: '123456',
        ),
      );

      verify(
        () => repository.login(
          email: 'test@test.com',
          password: 'pass',
          twoFactorCode: '123456',
        ),
      ).called(1);
    });
  });
}
