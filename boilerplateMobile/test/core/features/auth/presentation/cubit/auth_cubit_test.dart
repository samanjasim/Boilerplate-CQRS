import 'package:bloc_test/bloc_test.dart';
import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/auth_session.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/repositories/auth_repository.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_state.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late MockAuthRepository repository;

  setUp(() {
    repository = MockAuthRepository();
  });

  final testUser = User(
    id: '1',
    email: 'test@test.com',
    firstName: 'Test',
    lastName: 'User',
    roles: ['Admin'],
    permissions: {'Users.View', 'Billing.View'},
    createdAt: DateTime(2024),
  );

  group('AuthCubit', () {
    blocTest<AuthCubit, AuthState>(
      'emits unauthenticated when no session exists',
      build: () {
        when(() => repository.hasSession()).thenAnswer((_) async => false);
        return AuthCubit(repository);
      },
      act: (cubit) => cubit.checkSession(),
      expect: () => [const AuthState.unauthenticated()],
    );

    blocTest<AuthCubit, AuthState>(
      'emits authenticated when session valid',
      build: () {
        when(() => repository.hasSession()).thenAnswer((_) async => true);
        when(() => repository.getCurrentUser())
            .thenAnswer((_) async => Success(testUser));
        return AuthCubit(repository);
      },
      act: (cubit) => cubit.checkSession(),
      expect: () => [
        AuthState.authenticated(
          user: testUser,
          permissions: {'Users.View', 'Billing.View'},
        ),
      ],
    );

    blocTest<AuthCubit, AuthState>(
      'emits unauthenticated when session invalid (getCurrentUser fails)',
      build: () {
        when(() => repository.hasSession()).thenAnswer((_) async => true);
        when(() => repository.getCurrentUser())
            .thenAnswer((_) async => Err(AuthFailure('expired')));
        when(() => repository.logout())
            .thenAnswer((_) async => Success(null));
        return AuthCubit(repository);
      },
      act: (cubit) => cubit.checkSession(),
      expect: () => [const AuthState.unauthenticated()],
    );

    blocTest<AuthCubit, AuthState>(
      'onLoginSuccess emits authenticated',
      build: () => AuthCubit(repository),
      act: (cubit) => cubit.onLoginSuccess(
        AuthSession(
          accessToken: 'token',
          refreshToken: 'refresh',
          user: testUser,
        ),
      ),
      expect: () => [
        AuthState.authenticated(
          user: testUser,
          permissions: {'Users.View', 'Billing.View'},
        ),
      ],
    );

    blocTest<AuthCubit, AuthState>(
      'logout clears session and emits unauthenticated',
      build: () {
        when(() => repository.logout())
            .thenAnswer((_) async => Success(null));
        return AuthCubit(repository);
      },
      act: (cubit) => cubit.logout(),
      expect: () => [const AuthState.unauthenticated()],
      verify: (_) {
        verify(() => repository.logout()).called(1);
      },
    );

    blocTest<AuthCubit, AuthState>(
      'sessionExpired emits unauthenticated',
      build: () => AuthCubit(repository),
      act: (cubit) => cubit.sessionExpired(),
      expect: () => [const AuthState.unauthenticated()],
    );
  });
}
