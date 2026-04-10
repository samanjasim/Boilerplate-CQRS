import 'package:boilerplate_mobile/core/features/auth/data/dtos/user_dto.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'login_response_dto.freezed.dart';
part 'login_response_dto.g.dart';

@freezed
abstract class LoginResponseDto with _$LoginResponseDto {
  const factory LoginResponseDto({
    String? accessToken,
    String? refreshToken,
    String? expiresAt,
    UserDto? user,
    @Default(false) bool requiresTwoFactor,
  }) = _LoginResponseDto;

  factory LoginResponseDto.fromJson(Map<String, dynamic> json) =>
      _$LoginResponseDtoFromJson(json);
}
