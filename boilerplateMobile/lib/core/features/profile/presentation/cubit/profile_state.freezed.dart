// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'profile_state.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$ProfileState {
  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is ProfileState);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'ProfileState()';
  }
}

/// @nodoc
class $ProfileStateCopyWith<$Res> {
  $ProfileStateCopyWith(ProfileState _, $Res Function(ProfileState) __);
}

/// Adds pattern-matching-related methods to [ProfileState].
extension ProfileStatePatterns on ProfileState {
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
  TResult maybeMap<TResult extends Object?>({
    TResult Function(ProfileInitial value)? initial,
    TResult Function(ProfileLoading value)? loading,
    TResult Function(ProfileLoaded value)? loaded,
    TResult Function(ProfileSaving value)? saving,
    TResult Function(ProfileSaved value)? saved,
    TResult Function(ProfilePasswordChanged value)? passwordChanged,
    TResult Function(ProfileError value)? error,
    TResult Function(ProfileValidationError value)? validationError,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case ProfileInitial() when initial != null:
        return initial(_that);
      case ProfileLoading() when loading != null:
        return loading(_that);
      case ProfileLoaded() when loaded != null:
        return loaded(_that);
      case ProfileSaving() when saving != null:
        return saving(_that);
      case ProfileSaved() when saved != null:
        return saved(_that);
      case ProfilePasswordChanged() when passwordChanged != null:
        return passwordChanged(_that);
      case ProfileError() when error != null:
        return error(_that);
      case ProfileValidationError() when validationError != null:
        return validationError(_that);
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
  TResult map<TResult extends Object?>({
    required TResult Function(ProfileInitial value) initial,
    required TResult Function(ProfileLoading value) loading,
    required TResult Function(ProfileLoaded value) loaded,
    required TResult Function(ProfileSaving value) saving,
    required TResult Function(ProfileSaved value) saved,
    required TResult Function(ProfilePasswordChanged value) passwordChanged,
    required TResult Function(ProfileError value) error,
    required TResult Function(ProfileValidationError value) validationError,
  }) {
    final _that = this;
    switch (_that) {
      case ProfileInitial():
        return initial(_that);
      case ProfileLoading():
        return loading(_that);
      case ProfileLoaded():
        return loaded(_that);
      case ProfileSaving():
        return saving(_that);
      case ProfileSaved():
        return saved(_that);
      case ProfilePasswordChanged():
        return passwordChanged(_that);
      case ProfileError():
        return error(_that);
      case ProfileValidationError():
        return validationError(_that);
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
  TResult? mapOrNull<TResult extends Object?>({
    TResult? Function(ProfileInitial value)? initial,
    TResult? Function(ProfileLoading value)? loading,
    TResult? Function(ProfileLoaded value)? loaded,
    TResult? Function(ProfileSaving value)? saving,
    TResult? Function(ProfileSaved value)? saved,
    TResult? Function(ProfilePasswordChanged value)? passwordChanged,
    TResult? Function(ProfileError value)? error,
    TResult? Function(ProfileValidationError value)? validationError,
  }) {
    final _that = this;
    switch (_that) {
      case ProfileInitial() when initial != null:
        return initial(_that);
      case ProfileLoading() when loading != null:
        return loading(_that);
      case ProfileLoaded() when loaded != null:
        return loaded(_that);
      case ProfileSaving() when saving != null:
        return saving(_that);
      case ProfileSaved() when saved != null:
        return saved(_that);
      case ProfilePasswordChanged() when passwordChanged != null:
        return passwordChanged(_that);
      case ProfileError() when error != null:
        return error(_that);
      case ProfileValidationError() when validationError != null:
        return validationError(_that);
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
  TResult maybeWhen<TResult extends Object?>({
    TResult Function()? initial,
    TResult Function()? loading,
    TResult Function(User user)? loaded,
    TResult Function()? saving,
    TResult Function()? saved,
    TResult Function()? passwordChanged,
    TResult Function(String message)? error,
    TResult Function(Map<String, List<String>> errors)? validationError,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case ProfileInitial() when initial != null:
        return initial();
      case ProfileLoading() when loading != null:
        return loading();
      case ProfileLoaded() when loaded != null:
        return loaded(_that.user);
      case ProfileSaving() when saving != null:
        return saving();
      case ProfileSaved() when saved != null:
        return saved();
      case ProfilePasswordChanged() when passwordChanged != null:
        return passwordChanged();
      case ProfileError() when error != null:
        return error(_that.message);
      case ProfileValidationError() when validationError != null:
        return validationError(_that.errors);
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
  TResult when<TResult extends Object?>({
    required TResult Function() initial,
    required TResult Function() loading,
    required TResult Function(User user) loaded,
    required TResult Function() saving,
    required TResult Function() saved,
    required TResult Function() passwordChanged,
    required TResult Function(String message) error,
    required TResult Function(Map<String, List<String>> errors) validationError,
  }) {
    final _that = this;
    switch (_that) {
      case ProfileInitial():
        return initial();
      case ProfileLoading():
        return loading();
      case ProfileLoaded():
        return loaded(_that.user);
      case ProfileSaving():
        return saving();
      case ProfileSaved():
        return saved();
      case ProfilePasswordChanged():
        return passwordChanged();
      case ProfileError():
        return error(_that.message);
      case ProfileValidationError():
        return validationError(_that.errors);
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
  TResult? whenOrNull<TResult extends Object?>({
    TResult? Function()? initial,
    TResult? Function()? loading,
    TResult? Function(User user)? loaded,
    TResult? Function()? saving,
    TResult? Function()? saved,
    TResult? Function()? passwordChanged,
    TResult? Function(String message)? error,
    TResult? Function(Map<String, List<String>> errors)? validationError,
  }) {
    final _that = this;
    switch (_that) {
      case ProfileInitial() when initial != null:
        return initial();
      case ProfileLoading() when loading != null:
        return loading();
      case ProfileLoaded() when loaded != null:
        return loaded(_that.user);
      case ProfileSaving() when saving != null:
        return saving();
      case ProfileSaved() when saved != null:
        return saved();
      case ProfilePasswordChanged() when passwordChanged != null:
        return passwordChanged();
      case ProfileError() when error != null:
        return error(_that.message);
      case ProfileValidationError() when validationError != null:
        return validationError(_that.errors);
      case _:
        return null;
    }
  }
}

/// @nodoc

class ProfileInitial implements ProfileState {
  const ProfileInitial();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is ProfileInitial);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'ProfileState.initial()';
  }
}

/// @nodoc

class ProfileLoading implements ProfileState {
  const ProfileLoading();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is ProfileLoading);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'ProfileState.loading()';
  }
}

/// @nodoc

class ProfileLoaded implements ProfileState {
  const ProfileLoaded(this.user);

  final User user;

  /// Create a copy of ProfileState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $ProfileLoadedCopyWith<ProfileLoaded> get copyWith =>
      _$ProfileLoadedCopyWithImpl<ProfileLoaded>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is ProfileLoaded &&
            (identical(other.user, user) || other.user == user));
  }

  @override
  int get hashCode => Object.hash(runtimeType, user);

  @override
  String toString() {
    return 'ProfileState.loaded(user: $user)';
  }
}

/// @nodoc
abstract mixin class $ProfileLoadedCopyWith<$Res>
    implements $ProfileStateCopyWith<$Res> {
  factory $ProfileLoadedCopyWith(
          ProfileLoaded value, $Res Function(ProfileLoaded) _then) =
      _$ProfileLoadedCopyWithImpl;
  @useResult
  $Res call({User user});
}

/// @nodoc
class _$ProfileLoadedCopyWithImpl<$Res>
    implements $ProfileLoadedCopyWith<$Res> {
  _$ProfileLoadedCopyWithImpl(this._self, this._then);

  final ProfileLoaded _self;
  final $Res Function(ProfileLoaded) _then;

  /// Create a copy of ProfileState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? user = null,
  }) {
    return _then(ProfileLoaded(
      null == user
          ? _self.user
          : user // ignore: cast_nullable_to_non_nullable
              as User,
    ));
  }
}

/// @nodoc

class ProfileSaving implements ProfileState {
  const ProfileSaving();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is ProfileSaving);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'ProfileState.saving()';
  }
}

/// @nodoc

class ProfileSaved implements ProfileState {
  const ProfileSaved();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is ProfileSaved);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'ProfileState.saved()';
  }
}

/// @nodoc

class ProfilePasswordChanged implements ProfileState {
  const ProfilePasswordChanged();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is ProfilePasswordChanged);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'ProfileState.passwordChanged()';
  }
}

/// @nodoc

class ProfileError implements ProfileState {
  const ProfileError(this.message);

  final String message;

  /// Create a copy of ProfileState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $ProfileErrorCopyWith<ProfileError> get copyWith =>
      _$ProfileErrorCopyWithImpl<ProfileError>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is ProfileError &&
            (identical(other.message, message) || other.message == message));
  }

  @override
  int get hashCode => Object.hash(runtimeType, message);

  @override
  String toString() {
    return 'ProfileState.error(message: $message)';
  }
}

/// @nodoc
abstract mixin class $ProfileErrorCopyWith<$Res>
    implements $ProfileStateCopyWith<$Res> {
  factory $ProfileErrorCopyWith(
          ProfileError value, $Res Function(ProfileError) _then) =
      _$ProfileErrorCopyWithImpl;
  @useResult
  $Res call({String message});
}

/// @nodoc
class _$ProfileErrorCopyWithImpl<$Res> implements $ProfileErrorCopyWith<$Res> {
  _$ProfileErrorCopyWithImpl(this._self, this._then);

  final ProfileError _self;
  final $Res Function(ProfileError) _then;

  /// Create a copy of ProfileState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? message = null,
  }) {
    return _then(ProfileError(
      null == message
          ? _self.message
          : message // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

/// @nodoc

class ProfileValidationError implements ProfileState {
  const ProfileValidationError(final Map<String, List<String>> errors)
      : _errors = errors;

  final Map<String, List<String>> _errors;
  Map<String, List<String>> get errors {
    if (_errors is EqualUnmodifiableMapView) return _errors;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableMapView(_errors);
  }

  /// Create a copy of ProfileState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $ProfileValidationErrorCopyWith<ProfileValidationError> get copyWith =>
      _$ProfileValidationErrorCopyWithImpl<ProfileValidationError>(
          this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is ProfileValidationError &&
            const DeepCollectionEquality().equals(other._errors, _errors));
  }

  @override
  int get hashCode =>
      Object.hash(runtimeType, const DeepCollectionEquality().hash(_errors));

  @override
  String toString() {
    return 'ProfileState.validationError(errors: $errors)';
  }
}

/// @nodoc
abstract mixin class $ProfileValidationErrorCopyWith<$Res>
    implements $ProfileStateCopyWith<$Res> {
  factory $ProfileValidationErrorCopyWith(ProfileValidationError value,
          $Res Function(ProfileValidationError) _then) =
      _$ProfileValidationErrorCopyWithImpl;
  @useResult
  $Res call({Map<String, List<String>> errors});
}

/// @nodoc
class _$ProfileValidationErrorCopyWithImpl<$Res>
    implements $ProfileValidationErrorCopyWith<$Res> {
  _$ProfileValidationErrorCopyWithImpl(this._self, this._then);

  final ProfileValidationError _self;
  final $Res Function(ProfileValidationError) _then;

  /// Create a copy of ProfileState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? errors = null,
  }) {
    return _then(ProfileValidationError(
      null == errors
          ? _self._errors
          : errors // ignore: cast_nullable_to_non_nullable
              as Map<String, List<String>>,
    ));
  }
}

// dart format on
