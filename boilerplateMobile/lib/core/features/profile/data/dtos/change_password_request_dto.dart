import 'package:freezed_annotation/freezed_annotation.dart';

part 'change_password_request_dto.freezed.dart';
part 'change_password_request_dto.g.dart';

@freezed
abstract class ChangePasswordRequestDto with _$ChangePasswordRequestDto {
  const factory ChangePasswordRequestDto({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) = _ChangePasswordRequestDto;

  factory ChangePasswordRequestDto.fromJson(Map<String, dynamic> json) =>
      _$ChangePasswordRequestDtoFromJson(json);
}
