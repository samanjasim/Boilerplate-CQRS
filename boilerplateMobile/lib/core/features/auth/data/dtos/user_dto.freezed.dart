// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'user_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$UserDto {
  String get id;
  String get email;
  String get firstName;
  String get lastName;
  String get createdAt;
  String? get username;
  String? get phoneNumber;
  String? get status;
  bool get emailConfirmed;
  bool get phoneConfirmed;
  bool get twoFactorEnabled;
  String? get lastLoginAt;
  List<String> get roles;
  List<String>? get permissions;
  String? get tenantId;
  String? get tenantName;
  String? get tenantSlug;
  String? get tenantLogoUrl;
  String? get tenantPrimaryColor;

  /// Create a copy of UserDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $UserDtoCopyWith<UserDto> get copyWith =>
      _$UserDtoCopyWithImpl<UserDto>(this as UserDto, _$identity);

  /// Serializes this UserDto to a JSON map.
  Map<String, dynamic> toJson();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is UserDto &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.firstName, firstName) ||
                other.firstName == firstName) &&
            (identical(other.lastName, lastName) ||
                other.lastName == lastName) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.phoneNumber, phoneNumber) ||
                other.phoneNumber == phoneNumber) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.emailConfirmed, emailConfirmed) ||
                other.emailConfirmed == emailConfirmed) &&
            (identical(other.phoneConfirmed, phoneConfirmed) ||
                other.phoneConfirmed == phoneConfirmed) &&
            (identical(other.twoFactorEnabled, twoFactorEnabled) ||
                other.twoFactorEnabled == twoFactorEnabled) &&
            (identical(other.lastLoginAt, lastLoginAt) ||
                other.lastLoginAt == lastLoginAt) &&
            const DeepCollectionEquality().equals(other.roles, roles) &&
            const DeepCollectionEquality()
                .equals(other.permissions, permissions) &&
            (identical(other.tenantId, tenantId) ||
                other.tenantId == tenantId) &&
            (identical(other.tenantName, tenantName) ||
                other.tenantName == tenantName) &&
            (identical(other.tenantSlug, tenantSlug) ||
                other.tenantSlug == tenantSlug) &&
            (identical(other.tenantLogoUrl, tenantLogoUrl) ||
                other.tenantLogoUrl == tenantLogoUrl) &&
            (identical(other.tenantPrimaryColor, tenantPrimaryColor) ||
                other.tenantPrimaryColor == tenantPrimaryColor));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hashAll([
        runtimeType,
        id,
        email,
        firstName,
        lastName,
        createdAt,
        username,
        phoneNumber,
        status,
        emailConfirmed,
        phoneConfirmed,
        twoFactorEnabled,
        lastLoginAt,
        const DeepCollectionEquality().hash(roles),
        const DeepCollectionEquality().hash(permissions),
        tenantId,
        tenantName,
        tenantSlug,
        tenantLogoUrl,
        tenantPrimaryColor
      ]);

  @override
  String toString() {
    return 'UserDto(id: $id, email: $email, firstName: $firstName, lastName: $lastName, createdAt: $createdAt, username: $username, phoneNumber: $phoneNumber, status: $status, emailConfirmed: $emailConfirmed, phoneConfirmed: $phoneConfirmed, twoFactorEnabled: $twoFactorEnabled, lastLoginAt: $lastLoginAt, roles: $roles, permissions: $permissions, tenantId: $tenantId, tenantName: $tenantName, tenantSlug: $tenantSlug, tenantLogoUrl: $tenantLogoUrl, tenantPrimaryColor: $tenantPrimaryColor)';
  }
}

/// @nodoc
abstract mixin class $UserDtoCopyWith<$Res> {
  factory $UserDtoCopyWith(UserDto value, $Res Function(UserDto) _then) =
      _$UserDtoCopyWithImpl;
  @useResult
  $Res call(
      {String id,
      String email,
      String firstName,
      String lastName,
      String createdAt,
      String? username,
      String? phoneNumber,
      String? status,
      bool emailConfirmed,
      bool phoneConfirmed,
      bool twoFactorEnabled,
      String? lastLoginAt,
      List<String> roles,
      List<String>? permissions,
      String? tenantId,
      String? tenantName,
      String? tenantSlug,
      String? tenantLogoUrl,
      String? tenantPrimaryColor});
}

/// @nodoc
class _$UserDtoCopyWithImpl<$Res> implements $UserDtoCopyWith<$Res> {
  _$UserDtoCopyWithImpl(this._self, this._then);

  final UserDto _self;
  final $Res Function(UserDto) _then;

  /// Create a copy of UserDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? email = null,
    Object? firstName = null,
    Object? lastName = null,
    Object? createdAt = null,
    Object? username = freezed,
    Object? phoneNumber = freezed,
    Object? status = freezed,
    Object? emailConfirmed = null,
    Object? phoneConfirmed = null,
    Object? twoFactorEnabled = null,
    Object? lastLoginAt = freezed,
    Object? roles = null,
    Object? permissions = freezed,
    Object? tenantId = freezed,
    Object? tenantName = freezed,
    Object? tenantSlug = freezed,
    Object? tenantLogoUrl = freezed,
    Object? tenantPrimaryColor = freezed,
  }) {
    return _then(_self.copyWith(
      id: null == id
          ? _self.id
          : id // ignore: cast_nullable_to_non_nullable
              as String,
      email: null == email
          ? _self.email
          : email // ignore: cast_nullable_to_non_nullable
              as String,
      firstName: null == firstName
          ? _self.firstName
          : firstName // ignore: cast_nullable_to_non_nullable
              as String,
      lastName: null == lastName
          ? _self.lastName
          : lastName // ignore: cast_nullable_to_non_nullable
              as String,
      createdAt: null == createdAt
          ? _self.createdAt
          : createdAt // ignore: cast_nullable_to_non_nullable
              as String,
      username: freezed == username
          ? _self.username
          : username // ignore: cast_nullable_to_non_nullable
              as String?,
      phoneNumber: freezed == phoneNumber
          ? _self.phoneNumber
          : phoneNumber // ignore: cast_nullable_to_non_nullable
              as String?,
      status: freezed == status
          ? _self.status
          : status // ignore: cast_nullable_to_non_nullable
              as String?,
      emailConfirmed: null == emailConfirmed
          ? _self.emailConfirmed
          : emailConfirmed // ignore: cast_nullable_to_non_nullable
              as bool,
      phoneConfirmed: null == phoneConfirmed
          ? _self.phoneConfirmed
          : phoneConfirmed // ignore: cast_nullable_to_non_nullable
              as bool,
      twoFactorEnabled: null == twoFactorEnabled
          ? _self.twoFactorEnabled
          : twoFactorEnabled // ignore: cast_nullable_to_non_nullable
              as bool,
      lastLoginAt: freezed == lastLoginAt
          ? _self.lastLoginAt
          : lastLoginAt // ignore: cast_nullable_to_non_nullable
              as String?,
      roles: null == roles
          ? _self.roles
          : roles // ignore: cast_nullable_to_non_nullable
              as List<String>,
      permissions: freezed == permissions
          ? _self.permissions
          : permissions // ignore: cast_nullable_to_non_nullable
              as List<String>?,
      tenantId: freezed == tenantId
          ? _self.tenantId
          : tenantId // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantName: freezed == tenantName
          ? _self.tenantName
          : tenantName // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantSlug: freezed == tenantSlug
          ? _self.tenantSlug
          : tenantSlug // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantLogoUrl: freezed == tenantLogoUrl
          ? _self.tenantLogoUrl
          : tenantLogoUrl // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantPrimaryColor: freezed == tenantPrimaryColor
          ? _self.tenantPrimaryColor
          : tenantPrimaryColor // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

/// Adds pattern-matching-related methods to [UserDto].
extension UserDtoPatterns on UserDto {
  /// A variant of `map` that fallback to returning `orElse`.
  ///
  /// It is equivalent to doing:
  /// ```dart
  /// switch (sealedClass) {
  ///   case final Subclass value:
  ///     return ...;
  ///   case _:
  ///     return orElse();
  /// }
  /// ```

  @optionalTypeArgs
  TResult maybeMap<TResult extends Object?>(
    TResult Function(_UserDto value)? $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _UserDto() when $default != null:
        return $default(_that);
      case _:
        return orElse();
    }
  }

  /// A `switch`-like method, using callbacks.
  ///
  /// Callbacks receives the raw object, upcasted.
  /// It is equivalent to doing:
  /// ```dart
  /// switch (sealedClass) {
  ///   case final Subclass value:
  ///     return ...;
  ///   case final Subclass2 value:
  ///     return ...;
  /// }
  /// ```

  @optionalTypeArgs
  TResult map<TResult extends Object?>(
    TResult Function(_UserDto value) $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UserDto():
        return $default(_that);
      case _:
        throw StateError('Unexpected subclass');
    }
  }

  /// A variant of `map` that fallback to returning `null`.
  ///
  /// It is equivalent to doing:
  /// ```dart
  /// switch (sealedClass) {
  ///   case final Subclass value:
  ///     return ...;
  ///   case _:
  ///     return null;
  /// }
  /// ```

  @optionalTypeArgs
  TResult? mapOrNull<TResult extends Object?>(
    TResult? Function(_UserDto value)? $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UserDto() when $default != null:
        return $default(_that);
      case _:
        return null;
    }
  }

  /// A variant of `when` that fallback to an `orElse` callback.
  ///
  /// It is equivalent to doing:
  /// ```dart
  /// switch (sealedClass) {
  ///   case Subclass(:final field):
  ///     return ...;
  ///   case _:
  ///     return orElse();
  /// }
  /// ```

  @optionalTypeArgs
  TResult maybeWhen<TResult extends Object?>(
    TResult Function(
            String id,
            String email,
            String firstName,
            String lastName,
            String createdAt,
            String? username,
            String? phoneNumber,
            String? status,
            bool emailConfirmed,
            bool phoneConfirmed,
            bool twoFactorEnabled,
            String? lastLoginAt,
            List<String> roles,
            List<String>? permissions,
            String? tenantId,
            String? tenantName,
            String? tenantSlug,
            String? tenantLogoUrl,
            String? tenantPrimaryColor)?
        $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _UserDto() when $default != null:
        return $default(
            _that.id,
            _that.email,
            _that.firstName,
            _that.lastName,
            _that.createdAt,
            _that.username,
            _that.phoneNumber,
            _that.status,
            _that.emailConfirmed,
            _that.phoneConfirmed,
            _that.twoFactorEnabled,
            _that.lastLoginAt,
            _that.roles,
            _that.permissions,
            _that.tenantId,
            _that.tenantName,
            _that.tenantSlug,
            _that.tenantLogoUrl,
            _that.tenantPrimaryColor);
      case _:
        return orElse();
    }
  }

  /// A `switch`-like method, using callbacks.
  ///
  /// As opposed to `map`, this offers destructuring.
  /// It is equivalent to doing:
  /// ```dart
  /// switch (sealedClass) {
  ///   case Subclass(:final field):
  ///     return ...;
  ///   case Subclass2(:final field2):
  ///     return ...;
  /// }
  /// ```

  @optionalTypeArgs
  TResult when<TResult extends Object?>(
    TResult Function(
            String id,
            String email,
            String firstName,
            String lastName,
            String createdAt,
            String? username,
            String? phoneNumber,
            String? status,
            bool emailConfirmed,
            bool phoneConfirmed,
            bool twoFactorEnabled,
            String? lastLoginAt,
            List<String> roles,
            List<String>? permissions,
            String? tenantId,
            String? tenantName,
            String? tenantSlug,
            String? tenantLogoUrl,
            String? tenantPrimaryColor)
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UserDto():
        return $default(
            _that.id,
            _that.email,
            _that.firstName,
            _that.lastName,
            _that.createdAt,
            _that.username,
            _that.phoneNumber,
            _that.status,
            _that.emailConfirmed,
            _that.phoneConfirmed,
            _that.twoFactorEnabled,
            _that.lastLoginAt,
            _that.roles,
            _that.permissions,
            _that.tenantId,
            _that.tenantName,
            _that.tenantSlug,
            _that.tenantLogoUrl,
            _that.tenantPrimaryColor);
      case _:
        throw StateError('Unexpected subclass');
    }
  }

  /// A variant of `when` that fallback to returning `null`
  ///
  /// It is equivalent to doing:
  /// ```dart
  /// switch (sealedClass) {
  ///   case Subclass(:final field):
  ///     return ...;
  ///   case _:
  ///     return null;
  /// }
  /// ```

  @optionalTypeArgs
  TResult? whenOrNull<TResult extends Object?>(
    TResult? Function(
            String id,
            String email,
            String firstName,
            String lastName,
            String createdAt,
            String? username,
            String? phoneNumber,
            String? status,
            bool emailConfirmed,
            bool phoneConfirmed,
            bool twoFactorEnabled,
            String? lastLoginAt,
            List<String> roles,
            List<String>? permissions,
            String? tenantId,
            String? tenantName,
            String? tenantSlug,
            String? tenantLogoUrl,
            String? tenantPrimaryColor)?
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UserDto() when $default != null:
        return $default(
            _that.id,
            _that.email,
            _that.firstName,
            _that.lastName,
            _that.createdAt,
            _that.username,
            _that.phoneNumber,
            _that.status,
            _that.emailConfirmed,
            _that.phoneConfirmed,
            _that.twoFactorEnabled,
            _that.lastLoginAt,
            _that.roles,
            _that.permissions,
            _that.tenantId,
            _that.tenantName,
            _that.tenantSlug,
            _that.tenantLogoUrl,
            _that.tenantPrimaryColor);
      case _:
        return null;
    }
  }
}

/// @nodoc
@JsonSerializable()
class _UserDto implements UserDto {
  const _UserDto(
      {required this.id,
      required this.email,
      required this.firstName,
      required this.lastName,
      required this.createdAt,
      this.username,
      this.phoneNumber,
      this.status,
      this.emailConfirmed = false,
      this.phoneConfirmed = false,
      this.twoFactorEnabled = false,
      this.lastLoginAt,
      final List<String> roles = const [],
      final List<String>? permissions,
      this.tenantId,
      this.tenantName,
      this.tenantSlug,
      this.tenantLogoUrl,
      this.tenantPrimaryColor})
      : _roles = roles,
        _permissions = permissions;
  factory _UserDto.fromJson(Map<String, dynamic> json) =>
      _$UserDtoFromJson(json);

  @override
  final String id;
  @override
  final String email;
  @override
  final String firstName;
  @override
  final String lastName;
  @override
  final String createdAt;
  @override
  final String? username;
  @override
  final String? phoneNumber;
  @override
  final String? status;
  @override
  @JsonKey()
  final bool emailConfirmed;
  @override
  @JsonKey()
  final bool phoneConfirmed;
  @override
  @JsonKey()
  final bool twoFactorEnabled;
  @override
  final String? lastLoginAt;
  final List<String> _roles;
  @override
  @JsonKey()
  List<String> get roles {
    if (_roles is EqualUnmodifiableListView) return _roles;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_roles);
  }

  final List<String>? _permissions;
  @override
  List<String>? get permissions {
    final value = _permissions;
    if (value == null) return null;
    if (_permissions is EqualUnmodifiableListView) return _permissions;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(value);
  }

  @override
  final String? tenantId;
  @override
  final String? tenantName;
  @override
  final String? tenantSlug;
  @override
  final String? tenantLogoUrl;
  @override
  final String? tenantPrimaryColor;

  /// Create a copy of UserDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  _$UserDtoCopyWith<_UserDto> get copyWith =>
      __$UserDtoCopyWithImpl<_UserDto>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$UserDtoToJson(
      this,
    );
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _UserDto &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.firstName, firstName) ||
                other.firstName == firstName) &&
            (identical(other.lastName, lastName) ||
                other.lastName == lastName) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.phoneNumber, phoneNumber) ||
                other.phoneNumber == phoneNumber) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.emailConfirmed, emailConfirmed) ||
                other.emailConfirmed == emailConfirmed) &&
            (identical(other.phoneConfirmed, phoneConfirmed) ||
                other.phoneConfirmed == phoneConfirmed) &&
            (identical(other.twoFactorEnabled, twoFactorEnabled) ||
                other.twoFactorEnabled == twoFactorEnabled) &&
            (identical(other.lastLoginAt, lastLoginAt) ||
                other.lastLoginAt == lastLoginAt) &&
            const DeepCollectionEquality().equals(other._roles, _roles) &&
            const DeepCollectionEquality()
                .equals(other._permissions, _permissions) &&
            (identical(other.tenantId, tenantId) ||
                other.tenantId == tenantId) &&
            (identical(other.tenantName, tenantName) ||
                other.tenantName == tenantName) &&
            (identical(other.tenantSlug, tenantSlug) ||
                other.tenantSlug == tenantSlug) &&
            (identical(other.tenantLogoUrl, tenantLogoUrl) ||
                other.tenantLogoUrl == tenantLogoUrl) &&
            (identical(other.tenantPrimaryColor, tenantPrimaryColor) ||
                other.tenantPrimaryColor == tenantPrimaryColor));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hashAll([
        runtimeType,
        id,
        email,
        firstName,
        lastName,
        createdAt,
        username,
        phoneNumber,
        status,
        emailConfirmed,
        phoneConfirmed,
        twoFactorEnabled,
        lastLoginAt,
        const DeepCollectionEquality().hash(_roles),
        const DeepCollectionEquality().hash(_permissions),
        tenantId,
        tenantName,
        tenantSlug,
        tenantLogoUrl,
        tenantPrimaryColor
      ]);

  @override
  String toString() {
    return 'UserDto(id: $id, email: $email, firstName: $firstName, lastName: $lastName, createdAt: $createdAt, username: $username, phoneNumber: $phoneNumber, status: $status, emailConfirmed: $emailConfirmed, phoneConfirmed: $phoneConfirmed, twoFactorEnabled: $twoFactorEnabled, lastLoginAt: $lastLoginAt, roles: $roles, permissions: $permissions, tenantId: $tenantId, tenantName: $tenantName, tenantSlug: $tenantSlug, tenantLogoUrl: $tenantLogoUrl, tenantPrimaryColor: $tenantPrimaryColor)';
  }
}

/// @nodoc
abstract mixin class _$UserDtoCopyWith<$Res> implements $UserDtoCopyWith<$Res> {
  factory _$UserDtoCopyWith(_UserDto value, $Res Function(_UserDto) _then) =
      __$UserDtoCopyWithImpl;
  @override
  @useResult
  $Res call(
      {String id,
      String email,
      String firstName,
      String lastName,
      String createdAt,
      String? username,
      String? phoneNumber,
      String? status,
      bool emailConfirmed,
      bool phoneConfirmed,
      bool twoFactorEnabled,
      String? lastLoginAt,
      List<String> roles,
      List<String>? permissions,
      String? tenantId,
      String? tenantName,
      String? tenantSlug,
      String? tenantLogoUrl,
      String? tenantPrimaryColor});
}

/// @nodoc
class __$UserDtoCopyWithImpl<$Res> implements _$UserDtoCopyWith<$Res> {
  __$UserDtoCopyWithImpl(this._self, this._then);

  final _UserDto _self;
  final $Res Function(_UserDto) _then;

  /// Create a copy of UserDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $Res call({
    Object? id = null,
    Object? email = null,
    Object? firstName = null,
    Object? lastName = null,
    Object? createdAt = null,
    Object? username = freezed,
    Object? phoneNumber = freezed,
    Object? status = freezed,
    Object? emailConfirmed = null,
    Object? phoneConfirmed = null,
    Object? twoFactorEnabled = null,
    Object? lastLoginAt = freezed,
    Object? roles = null,
    Object? permissions = freezed,
    Object? tenantId = freezed,
    Object? tenantName = freezed,
    Object? tenantSlug = freezed,
    Object? tenantLogoUrl = freezed,
    Object? tenantPrimaryColor = freezed,
  }) {
    return _then(_UserDto(
      id: null == id
          ? _self.id
          : id // ignore: cast_nullable_to_non_nullable
              as String,
      email: null == email
          ? _self.email
          : email // ignore: cast_nullable_to_non_nullable
              as String,
      firstName: null == firstName
          ? _self.firstName
          : firstName // ignore: cast_nullable_to_non_nullable
              as String,
      lastName: null == lastName
          ? _self.lastName
          : lastName // ignore: cast_nullable_to_non_nullable
              as String,
      createdAt: null == createdAt
          ? _self.createdAt
          : createdAt // ignore: cast_nullable_to_non_nullable
              as String,
      username: freezed == username
          ? _self.username
          : username // ignore: cast_nullable_to_non_nullable
              as String?,
      phoneNumber: freezed == phoneNumber
          ? _self.phoneNumber
          : phoneNumber // ignore: cast_nullable_to_non_nullable
              as String?,
      status: freezed == status
          ? _self.status
          : status // ignore: cast_nullable_to_non_nullable
              as String?,
      emailConfirmed: null == emailConfirmed
          ? _self.emailConfirmed
          : emailConfirmed // ignore: cast_nullable_to_non_nullable
              as bool,
      phoneConfirmed: null == phoneConfirmed
          ? _self.phoneConfirmed
          : phoneConfirmed // ignore: cast_nullable_to_non_nullable
              as bool,
      twoFactorEnabled: null == twoFactorEnabled
          ? _self.twoFactorEnabled
          : twoFactorEnabled // ignore: cast_nullable_to_non_nullable
              as bool,
      lastLoginAt: freezed == lastLoginAt
          ? _self.lastLoginAt
          : lastLoginAt // ignore: cast_nullable_to_non_nullable
              as String?,
      roles: null == roles
          ? _self._roles
          : roles // ignore: cast_nullable_to_non_nullable
              as List<String>,
      permissions: freezed == permissions
          ? _self._permissions
          : permissions // ignore: cast_nullable_to_non_nullable
              as List<String>?,
      tenantId: freezed == tenantId
          ? _self.tenantId
          : tenantId // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantName: freezed == tenantName
          ? _self.tenantName
          : tenantName // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantSlug: freezed == tenantSlug
          ? _self.tenantSlug
          : tenantSlug // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantLogoUrl: freezed == tenantLogoUrl
          ? _self.tenantLogoUrl
          : tenantLogoUrl // ignore: cast_nullable_to_non_nullable
              as String?,
      tenantPrimaryColor: freezed == tenantPrimaryColor
          ? _self.tenantPrimaryColor
          : tenantPrimaryColor // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

// dart format on
