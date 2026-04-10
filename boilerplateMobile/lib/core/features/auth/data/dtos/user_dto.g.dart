// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'user_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_UserDto _$UserDtoFromJson(Map<String, dynamic> json) => _UserDto(
      id: json['id'] as String,
      email: json['email'] as String,
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      createdAt: json['createdAt'] as String,
      username: json['username'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      status: json['status'] as String?,
      emailConfirmed: json['emailConfirmed'] as bool? ?? false,
      phoneConfirmed: json['phoneConfirmed'] as bool? ?? false,
      twoFactorEnabled: json['twoFactorEnabled'] as bool? ?? false,
      lastLoginAt: json['lastLoginAt'] as String?,
      roles:
          (json['roles'] as List<dynamic>?)?.map((e) => e as String).toList() ??
              const [],
      permissions: (json['permissions'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      tenantId: json['tenantId'] as String?,
      tenantName: json['tenantName'] as String?,
      tenantSlug: json['tenantSlug'] as String?,
      tenantLogoUrl: json['tenantLogoUrl'] as String?,
      tenantPrimaryColor: json['tenantPrimaryColor'] as String?,
    );

Map<String, dynamic> _$UserDtoToJson(_UserDto instance) => <String, dynamic>{
      'id': instance.id,
      'email': instance.email,
      'firstName': instance.firstName,
      'lastName': instance.lastName,
      'createdAt': instance.createdAt,
      'username': instance.username,
      'phoneNumber': instance.phoneNumber,
      'status': instance.status,
      'emailConfirmed': instance.emailConfirmed,
      'phoneConfirmed': instance.phoneConfirmed,
      'twoFactorEnabled': instance.twoFactorEnabled,
      'lastLoginAt': instance.lastLoginAt,
      'roles': instance.roles,
      'permissions': instance.permissions,
      'tenantId': instance.tenantId,
      'tenantName': instance.tenantName,
      'tenantSlug': instance.tenantSlug,
      'tenantLogoUrl': instance.tenantLogoUrl,
      'tenantPrimaryColor': instance.tenantPrimaryColor,
    };
