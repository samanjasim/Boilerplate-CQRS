import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Result', () {
    test('Success.isSuccess is true', () {
      const result = Success(42);
      expect(result.isSuccess, isTrue);
      expect(result.isError, isFalse);
      expect(result.value, 42);
    });

    test('Err.isError is true', () {
      const result = Err<int>(NetworkFailure('offline'));
      expect(result.isError, isTrue);
      expect(result.isSuccess, isFalse);
      expect(result.failure, isA<NetworkFailure>());
    });

    test('getOrElse returns value on Success', () {
      const result = Success(42);
      expect(result.getOrElse((_) => -1), 42);
    });

    test('getOrElse calls onError on Err', () {
      const result = Err<int>(NetworkFailure('offline'));
      expect(result.getOrElse((_) => -1), -1);
    });

    test('map transforms Success value', () {
      const result = Success(21);
      final mapped = result.map((v) => v * 2);
      expect(mapped, isA<Success<int>>());
      expect((mapped as Success<int>).value, 42);
    });

    test('map passes Err through', () {
      const result = Err<int>(NetworkFailure('offline'));
      final mapped = result.map((v) => v * 2);
      expect(mapped, isA<Err<int>>());
    });

    test('flatMap chains Success', () async {
      const result = Success(21);
      final chained =
          await result.flatMap((v) async => Success(v * 2));
      expect((chained as Success<int>).value, 42);
    });

    test('flatMap short-circuits Err', () async {
      const result = Err<int>(NetworkFailure('offline'));
      final chained =
          await result.flatMap((v) async => Success(v * 2));
      expect(chained, isA<Err<int>>());
    });

    test('pattern matching works with switch', () {
      const Result<int> result = Success(42);
      final message = switch (result) {
        Success(value: final v) => 'Got $v',
        Err(failure: final f) => 'Failed: ${f.message}',
      };
      expect(message, 'Got 42');
    });

    test('Failure subtypes are exhaustive in switch', () {
      const Failure failure = ValidationFailure('bad', {'field': ['err']});
      final kind = switch (failure) {
        NetworkFailure() => 'network',
        AuthFailure() => 'auth',
        ServerFailure() => 'server',
        ValidationFailure() => 'validation',
        CacheFailure() => 'cache',
        UnknownFailure() => 'unknown',
      };
      expect(kind, 'validation');
    });
  });
}
