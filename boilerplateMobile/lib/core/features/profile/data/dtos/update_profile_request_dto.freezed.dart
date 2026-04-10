// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'update_profile_request_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$UpdateProfileRequestDto {
  String get firstName;
  String get lastName;
  String get email;
  String? get phoneNumber;

  /// Create a copy of UpdateProfileRequestDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $UpdateProfileRequestDtoCopyWith<UpdateProfileRequestDto> get copyWith =>
      _$UpdateProfileRequestDtoCopyWithImpl<UpdateProfileRequestDto>(
          this as UpdateProfileRequestDto, _$identity);

  /// Serializes this UpdateProfileRequestDto to a JSON map.
  Map<String, dynamic> toJson();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is UpdateProfileRequestDto &&
            (identical(other.firstName, firstName) ||
                other.firstName == firstName) &&
            (identical(other.lastName, lastName) ||
                other.lastName == lastName) &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.phoneNumber, phoneNumber) ||
                other.phoneNumber == phoneNumber));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, firstName, lastName, email, phoneNumber);

  @override
  String toString() {
    return 'UpdateProfileRequestDto(firstName: $firstName, lastName: $lastName, email: $email, phoneNumber: $phoneNumber)';
  }
}

/// @nodoc
abstract mixin class $UpdateProfileRequestDtoCopyWith<$Res> {
  factory $UpdateProfileRequestDtoCopyWith(UpdateProfileRequestDto value,
          $Res Function(UpdateProfileRequestDto) _then) =
      _$UpdateProfileRequestDtoCopyWithImpl;
  @useResult
  $Res call(
      {String firstName, String lastName, String email, String? phoneNumber});
}

/// @nodoc
class _$UpdateProfileRequestDtoCopyWithImpl<$Res>
    implements $UpdateProfileRequestDtoCopyWith<$Res> {
  _$UpdateProfileRequestDtoCopyWithImpl(this._self, this._then);

  final UpdateProfileRequestDto _self;
  final $Res Function(UpdateProfileRequestDto) _then;

  /// Create a copy of UpdateProfileRequestDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? firstName = null,
    Object? lastName = null,
    Object? email = null,
    Object? phoneNumber = freezed,
  }) {
    return _then(_self.copyWith(
      firstName: null == firstName
          ? _self.firstName
          : firstName // ignore: cast_nullable_to_non_nullable
              as String,
      lastName: null == lastName
          ? _self.lastName
          : lastName // ignore: cast_nullable_to_non_nullable
              as String,
      email: null == email
          ? _self.email
          : email // ignore: cast_nullable_to_non_nullable
              as String,
      phoneNumber: freezed == phoneNumber
          ? _self.phoneNumber
          : phoneNumber // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

/// Adds pattern-matching-related methods to [UpdateProfileRequestDto].
extension UpdateProfileRequestDtoPatterns on UpdateProfileRequestDto {
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
    TResult Function(_UpdateProfileRequestDto value)? $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _UpdateProfileRequestDto() when $default != null:
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
    TResult Function(_UpdateProfileRequestDto value) $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UpdateProfileRequestDto():
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
    TResult? Function(_UpdateProfileRequestDto value)? $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UpdateProfileRequestDto() when $default != null:
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
    TResult Function(String firstName, String lastName, String email,
            String? phoneNumber)?
        $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _UpdateProfileRequestDto() when $default != null:
        return $default(
            _that.firstName, _that.lastName, _that.email, _that.phoneNumber);
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
    TResult Function(String firstName, String lastName, String email,
            String? phoneNumber)
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UpdateProfileRequestDto():
        return $default(
            _that.firstName, _that.lastName, _that.email, _that.phoneNumber);
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
    TResult? Function(String firstName, String lastName, String email,
            String? phoneNumber)?
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _UpdateProfileRequestDto() when $default != null:
        return $default(
            _that.firstName, _that.lastName, _that.email, _that.phoneNumber);
      case _:
        return null;
    }
  }
}

/// @nodoc
@JsonSerializable()
class _UpdateProfileRequestDto implements UpdateProfileRequestDto {
  const _UpdateProfileRequestDto(
      {required this.firstName,
      required this.lastName,
      required this.email,
      this.phoneNumber});
  factory _UpdateProfileRequestDto.fromJson(Map<String, dynamic> json) =>
      _$UpdateProfileRequestDtoFromJson(json);

  @override
  final String firstName;
  @override
  final String lastName;
  @override
  final String email;
  @override
  final String? phoneNumber;

  /// Create a copy of UpdateProfileRequestDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  _$UpdateProfileRequestDtoCopyWith<_UpdateProfileRequestDto> get copyWith =>
      __$UpdateProfileRequestDtoCopyWithImpl<_UpdateProfileRequestDto>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$UpdateProfileRequestDtoToJson(
      this,
    );
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _UpdateProfileRequestDto &&
            (identical(other.firstName, firstName) ||
                other.firstName == firstName) &&
            (identical(other.lastName, lastName) ||
                other.lastName == lastName) &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.phoneNumber, phoneNumber) ||
                other.phoneNumber == phoneNumber));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode =>
      Object.hash(runtimeType, firstName, lastName, email, phoneNumber);

  @override
  String toString() {
    return 'UpdateProfileRequestDto(firstName: $firstName, lastName: $lastName, email: $email, phoneNumber: $phoneNumber)';
  }
}

/// @nodoc
abstract mixin class _$UpdateProfileRequestDtoCopyWith<$Res>
    implements $UpdateProfileRequestDtoCopyWith<$Res> {
  factory _$UpdateProfileRequestDtoCopyWith(_UpdateProfileRequestDto value,
          $Res Function(_UpdateProfileRequestDto) _then) =
      __$UpdateProfileRequestDtoCopyWithImpl;
  @override
  @useResult
  $Res call(
      {String firstName, String lastName, String email, String? phoneNumber});
}

/// @nodoc
class __$UpdateProfileRequestDtoCopyWithImpl<$Res>
    implements _$UpdateProfileRequestDtoCopyWith<$Res> {
  __$UpdateProfileRequestDtoCopyWithImpl(this._self, this._then);

  final _UpdateProfileRequestDto _self;
  final $Res Function(_UpdateProfileRequestDto) _then;

  /// Create a copy of UpdateProfileRequestDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $Res call({
    Object? firstName = null,
    Object? lastName = null,
    Object? email = null,
    Object? phoneNumber = freezed,
  }) {
    return _then(_UpdateProfileRequestDto(
      firstName: null == firstName
          ? _self.firstName
          : firstName // ignore: cast_nullable_to_non_nullable
              as String,
      lastName: null == lastName
          ? _self.lastName
          : lastName // ignore: cast_nullable_to_non_nullable
              as String,
      email: null == email
          ? _self.email
          : email // ignore: cast_nullable_to_non_nullable
              as String,
      phoneNumber: freezed == phoneNumber
          ? _self.phoneNumber
          : phoneNumber // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

// dart format on
