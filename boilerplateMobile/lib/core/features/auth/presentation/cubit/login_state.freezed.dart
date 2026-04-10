// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'login_state.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$LoginState {
  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is LoginState);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'LoginState()';
  }
}

/// @nodoc
class $LoginStateCopyWith<$Res> {
  $LoginStateCopyWith(LoginState _, $Res Function(LoginState) __);
}

/// Adds pattern-matching-related methods to [LoginState].
extension LoginStatePatterns on LoginState {
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
    TResult Function(LoginInitial value)? initial,
    TResult Function(LoginLoading value)? loading,
    TResult Function(LoginSuccessState value)? success,
    TResult Function(LoginRequires2FAState value)? requires2FA,
    TResult Function(LoginError value)? error,
    TResult Function(LoginValidationError value)? validationError,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case LoginInitial() when initial != null:
        return initial(_that);
      case LoginLoading() when loading != null:
        return loading(_that);
      case LoginSuccessState() when success != null:
        return success(_that);
      case LoginRequires2FAState() when requires2FA != null:
        return requires2FA(_that);
      case LoginError() when error != null:
        return error(_that);
      case LoginValidationError() when validationError != null:
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
    required TResult Function(LoginInitial value) initial,
    required TResult Function(LoginLoading value) loading,
    required TResult Function(LoginSuccessState value) success,
    required TResult Function(LoginRequires2FAState value) requires2FA,
    required TResult Function(LoginError value) error,
    required TResult Function(LoginValidationError value) validationError,
  }) {
    final _that = this;
    switch (_that) {
      case LoginInitial():
        return initial(_that);
      case LoginLoading():
        return loading(_that);
      case LoginSuccessState():
        return success(_that);
      case LoginRequires2FAState():
        return requires2FA(_that);
      case LoginError():
        return error(_that);
      case LoginValidationError():
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
    TResult? Function(LoginInitial value)? initial,
    TResult? Function(LoginLoading value)? loading,
    TResult? Function(LoginSuccessState value)? success,
    TResult? Function(LoginRequires2FAState value)? requires2FA,
    TResult? Function(LoginError value)? error,
    TResult? Function(LoginValidationError value)? validationError,
  }) {
    final _that = this;
    switch (_that) {
      case LoginInitial() when initial != null:
        return initial(_that);
      case LoginLoading() when loading != null:
        return loading(_that);
      case LoginSuccessState() when success != null:
        return success(_that);
      case LoginRequires2FAState() when requires2FA != null:
        return requires2FA(_that);
      case LoginError() when error != null:
        return error(_that);
      case LoginValidationError() when validationError != null:
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
    TResult Function()? success,
    TResult Function(String email, String password)? requires2FA,
    TResult Function(String message)? error,
    TResult Function(Map<String, List<String>> errors)? validationError,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case LoginInitial() when initial != null:
        return initial();
      case LoginLoading() when loading != null:
        return loading();
      case LoginSuccessState() when success != null:
        return success();
      case LoginRequires2FAState() when requires2FA != null:
        return requires2FA(_that.email, _that.password);
      case LoginError() when error != null:
        return error(_that.message);
      case LoginValidationError() when validationError != null:
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
    required TResult Function() success,
    required TResult Function(String email, String password) requires2FA,
    required TResult Function(String message) error,
    required TResult Function(Map<String, List<String>> errors) validationError,
  }) {
    final _that = this;
    switch (_that) {
      case LoginInitial():
        return initial();
      case LoginLoading():
        return loading();
      case LoginSuccessState():
        return success();
      case LoginRequires2FAState():
        return requires2FA(_that.email, _that.password);
      case LoginError():
        return error(_that.message);
      case LoginValidationError():
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
    TResult? Function()? success,
    TResult? Function(String email, String password)? requires2FA,
    TResult? Function(String message)? error,
    TResult? Function(Map<String, List<String>> errors)? validationError,
  }) {
    final _that = this;
    switch (_that) {
      case LoginInitial() when initial != null:
        return initial();
      case LoginLoading() when loading != null:
        return loading();
      case LoginSuccessState() when success != null:
        return success();
      case LoginRequires2FAState() when requires2FA != null:
        return requires2FA(_that.email, _that.password);
      case LoginError() when error != null:
        return error(_that.message);
      case LoginValidationError() when validationError != null:
        return validationError(_that.errors);
      case _:
        return null;
    }
  }
}

/// @nodoc

class LoginInitial implements LoginState {
  const LoginInitial();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is LoginInitial);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'LoginState.initial()';
  }
}

/// @nodoc

class LoginLoading implements LoginState {
  const LoginLoading();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is LoginLoading);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'LoginState.loading()';
  }
}

/// @nodoc

class LoginSuccessState implements LoginState {
  const LoginSuccessState();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is LoginSuccessState);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'LoginState.success()';
  }
}

/// @nodoc

class LoginRequires2FAState implements LoginState {
  const LoginRequires2FAState({required this.email, required this.password});

  final String email;
  final String password;

  /// Create a copy of LoginState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $LoginRequires2FAStateCopyWith<LoginRequires2FAState> get copyWith =>
      _$LoginRequires2FAStateCopyWithImpl<LoginRequires2FAState>(
          this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is LoginRequires2FAState &&
            (identical(other.email, email) || other.email == email) &&
            (identical(other.password, password) ||
                other.password == password));
  }

  @override
  int get hashCode => Object.hash(runtimeType, email, password);

  @override
  String toString() {
    return 'LoginState.requires2FA(email: $email, password: $password)';
  }
}

/// @nodoc
abstract mixin class $LoginRequires2FAStateCopyWith<$Res>
    implements $LoginStateCopyWith<$Res> {
  factory $LoginRequires2FAStateCopyWith(LoginRequires2FAState value,
          $Res Function(LoginRequires2FAState) _then) =
      _$LoginRequires2FAStateCopyWithImpl;
  @useResult
  $Res call({String email, String password});
}

/// @nodoc
class _$LoginRequires2FAStateCopyWithImpl<$Res>
    implements $LoginRequires2FAStateCopyWith<$Res> {
  _$LoginRequires2FAStateCopyWithImpl(this._self, this._then);

  final LoginRequires2FAState _self;
  final $Res Function(LoginRequires2FAState) _then;

  /// Create a copy of LoginState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? email = null,
    Object? password = null,
  }) {
    return _then(LoginRequires2FAState(
      email: null == email
          ? _self.email
          : email // ignore: cast_nullable_to_non_nullable
              as String,
      password: null == password
          ? _self.password
          : password // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

/// @nodoc

class LoginError implements LoginState {
  const LoginError(this.message);

  final String message;

  /// Create a copy of LoginState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $LoginErrorCopyWith<LoginError> get copyWith =>
      _$LoginErrorCopyWithImpl<LoginError>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is LoginError &&
            (identical(other.message, message) || other.message == message));
  }

  @override
  int get hashCode => Object.hash(runtimeType, message);

  @override
  String toString() {
    return 'LoginState.error(message: $message)';
  }
}

/// @nodoc
abstract mixin class $LoginErrorCopyWith<$Res>
    implements $LoginStateCopyWith<$Res> {
  factory $LoginErrorCopyWith(
          LoginError value, $Res Function(LoginError) _then) =
      _$LoginErrorCopyWithImpl;
  @useResult
  $Res call({String message});
}

/// @nodoc
class _$LoginErrorCopyWithImpl<$Res> implements $LoginErrorCopyWith<$Res> {
  _$LoginErrorCopyWithImpl(this._self, this._then);

  final LoginError _self;
  final $Res Function(LoginError) _then;

  /// Create a copy of LoginState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? message = null,
  }) {
    return _then(LoginError(
      null == message
          ? _self.message
          : message // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

/// @nodoc

class LoginValidationError implements LoginState {
  const LoginValidationError(final Map<String, List<String>> errors)
      : _errors = errors;

  final Map<String, List<String>> _errors;
  Map<String, List<String>> get errors {
    if (_errors is EqualUnmodifiableMapView) return _errors;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableMapView(_errors);
  }

  /// Create a copy of LoginState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $LoginValidationErrorCopyWith<LoginValidationError> get copyWith =>
      _$LoginValidationErrorCopyWithImpl<LoginValidationError>(
          this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is LoginValidationError &&
            const DeepCollectionEquality().equals(other._errors, _errors));
  }

  @override
  int get hashCode =>
      Object.hash(runtimeType, const DeepCollectionEquality().hash(_errors));

  @override
  String toString() {
    return 'LoginState.validationError(errors: $errors)';
  }
}

/// @nodoc
abstract mixin class $LoginValidationErrorCopyWith<$Res>
    implements $LoginStateCopyWith<$Res> {
  factory $LoginValidationErrorCopyWith(LoginValidationError value,
          $Res Function(LoginValidationError) _then) =
      _$LoginValidationErrorCopyWithImpl;
  @useResult
  $Res call({Map<String, List<String>> errors});
}

/// @nodoc
class _$LoginValidationErrorCopyWithImpl<$Res>
    implements $LoginValidationErrorCopyWith<$Res> {
  _$LoginValidationErrorCopyWithImpl(this._self, this._then);

  final LoginValidationError _self;
  final $Res Function(LoginValidationError) _then;

  /// Create a copy of LoginState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? errors = null,
  }) {
    return _then(LoginValidationError(
      null == errors
          ? _self._errors
          : errors // ignore: cast_nullable_to_non_nullable
              as Map<String, List<String>>,
    ));
  }
}

// dart format on
