import 'package:boilerplate_mobile/core/error/dio_error_mapper.dart';
import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('mapDioException', () {
    test('maps connectionTimeout to NetworkFailure', () {
      final e = DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.connectionTimeout,
      );
      expect(mapDioException(e), isA<NetworkFailure>());
    });

    test('maps connectionError to NetworkFailure', () {
      final e = DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.connectionError,
      );
      expect(mapDioException(e), isA<NetworkFailure>());
    });

    test('maps 401 to AuthFailure', () {
      final e = DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: RequestOptions(),
          statusCode: 401,
          data: <String, dynamic>{
            'message': 'Invalid credentials',
          },
        ),
      );
      final failure = mapDioException(e);
      expect(failure, isA<AuthFailure>());
      expect(failure.message, 'Invalid credentials');
    });

    test('maps 422 to ValidationFailure with errors', () {
      final e = DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: RequestOptions(),
          statusCode: 422,
          data: <String, dynamic>{
            'message': 'Validation failed',
            'validationErrors': <String, dynamic>{
              'Email': ['Invalid email format'],
              'Password': ['Too short', 'Must contain uppercase'],
            },
          },
        ),
      );
      final failure = mapDioException(e);
      expect(failure, isA<ValidationFailure>());
      final vf = failure as ValidationFailure;
      expect(vf.errors['Email'], ['Invalid email format']);
      expect(vf.errors['Password'], hasLength(2));
    });

    test('maps 500 to ServerFailure', () {
      final e = DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: RequestOptions(),
          statusCode: 500,
          data: <String, dynamic>{
            'message': 'Internal server error',
          },
        ),
      );
      final failure = mapDioException(e);
      expect(failure, isA<ServerFailure>());
      expect((failure as ServerFailure).statusCode, 500);
    });

    test('maps cancel to NetworkFailure', () {
      final e = DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.cancel,
      );
      expect(mapDioException(e), isA<NetworkFailure>());
    });
  });
}
