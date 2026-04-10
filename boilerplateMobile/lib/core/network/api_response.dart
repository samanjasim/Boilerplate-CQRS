import 'package:freezed_annotation/freezed_annotation.dart';

part 'api_response.freezed.dart';
part 'api_response.g.dart';

/// Mirrors the backend's `ApiResponse<T>` envelope.
///
/// Every successful backend response wraps its payload in:
/// ```json
/// { "data": <T>, "success": true, "message": "..." }
/// ```
///
/// Error responses add `errors` or `validationErrors`:
/// ```json
/// { "success": false, "message": "...", "errors": {...}, "validationErrors": {...} }
/// ```
///
/// Use with `genericArgumentFactories: true` so deserialisers like
/// `ApiResponse<UserDto>.fromJson(json, UserDto.fromJson)` work.
@Freezed(genericArgumentFactories: true)
abstract class ApiResponse<T> with _$ApiResponse<T> {
  const factory ApiResponse({
    T? data,
    @Default(false) bool success,
    String? message,
    Map<String, List<String>>? errors,
    Map<String, List<String>>? validationErrors,
  }) = _ApiResponse<T>;

  factory ApiResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? json) fromJsonT,
  ) =>
      _$ApiResponseFromJson(json, fromJsonT);
}

/// Mirrors the backend's `PaginatedResponse<T>` envelope.
@Freezed(genericArgumentFactories: true)
abstract class PaginatedResponse<T> with _$PaginatedResponse<T> {
  const factory PaginatedResponse({
    required PaginationMeta pagination,
    @Default([]) List<T> data,
    @Default(false) bool success,
    String? message,
  }) = _PaginatedResponse<T>;

  factory PaginatedResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? json) fromJsonT,
  ) =>
      _$PaginatedResponseFromJson(json, fromJsonT);
}

/// Pagination metadata returned by the backend inside
/// `PaginatedResponse.pagination`.
@freezed
abstract class PaginationMeta with _$PaginationMeta {
  const factory PaginationMeta({
    @Default(1) int pageNumber,
    @Default(10) int pageSize,
    @Default(0) int totalPages,
    @Default(0) int totalCount,
    @Default(false) bool hasNextPage,
    @Default(false) bool hasPreviousPage,
  }) = _PaginationMeta;

  factory PaginationMeta.fromJson(Map<String, dynamic> json) =>
      _$PaginationMetaFromJson(json);
}
