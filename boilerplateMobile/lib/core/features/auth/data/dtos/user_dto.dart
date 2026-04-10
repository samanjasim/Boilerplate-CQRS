import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'user_dto.freezed.dart';
part 'user_dto.g.dart';

@freezed
abstract class UserDto with _$UserDto {
  const factory UserDto({
    required String id,
    required String email,
    required String firstName,
    required String lastName,
    required String createdAt,
    String? username,
    String? phoneNumber,
    String? status,
    @Default(false) bool emailConfirmed,
    @Default(false) bool phoneConfirmed,
    @Default(false) bool twoFactorEnabled,
    String? lastLoginAt,
    @Default([]) List<String> roles,
    List<String>? permissions,
    String? tenantId,
    String? tenantName,
    String? tenantSlug,
    String? tenantLogoUrl,
    String? tenantPrimaryColor,
  }) = _UserDto;

  factory UserDto.fromJson(Map<String, dynamic> json) =>
      _$UserDtoFromJson(json);
}

/// Extension to map DTO to domain entity.
extension UserDtoMapper on UserDto {
  User toDomain() => User(
        id: id,
        email: email,
        firstName: firstName,
        lastName: lastName,
        username: username,
        phoneNumber: phoneNumber,
        status: status,
        emailConfirmed: emailConfirmed,
        twoFactorEnabled: twoFactorEnabled,
        lastLoginAt:
            lastLoginAt != null ? DateTime.tryParse(lastLoginAt!) : null,
        createdAt: DateTime.parse(createdAt),
        roles: roles,
        permissions: permissions?.toSet() ?? {},
        tenantId: tenantId,
        tenantName: tenantName,
        tenantSlug: tenantSlug,
      );
}
