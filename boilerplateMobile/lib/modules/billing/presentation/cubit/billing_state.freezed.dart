// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'billing_state.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$BillingState {
  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is BillingState);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'BillingState()';
  }
}

/// @nodoc
class $BillingStateCopyWith<$Res> {
  $BillingStateCopyWith(BillingState _, $Res Function(BillingState) __);
}

/// Adds pattern-matching-related methods to [BillingState].
extension BillingStatePatterns on BillingState {
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
    TResult Function(BillingInitial value)? initial,
    TResult Function(BillingLoading value)? loading,
    TResult Function(BillingLoaded value)? loaded,
    TResult Function(BillingError value)? error,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case BillingInitial() when initial != null:
        return initial(_that);
      case BillingLoading() when loading != null:
        return loading(_that);
      case BillingLoaded() when loaded != null:
        return loaded(_that);
      case BillingError() when error != null:
        return error(_that);
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
    required TResult Function(BillingInitial value) initial,
    required TResult Function(BillingLoading value) loading,
    required TResult Function(BillingLoaded value) loaded,
    required TResult Function(BillingError value) error,
  }) {
    final _that = this;
    switch (_that) {
      case BillingInitial():
        return initial(_that);
      case BillingLoading():
        return loading(_that);
      case BillingLoaded():
        return loaded(_that);
      case BillingError():
        return error(_that);
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
    TResult? Function(BillingInitial value)? initial,
    TResult? Function(BillingLoading value)? loading,
    TResult? Function(BillingLoaded value)? loaded,
    TResult? Function(BillingError value)? error,
  }) {
    final _that = this;
    switch (_that) {
      case BillingInitial() when initial != null:
        return initial(_that);
      case BillingLoading() when loading != null:
        return loading(_that);
      case BillingLoaded() when loaded != null:
        return loaded(_that);
      case BillingError() when error != null:
        return error(_that);
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
    TResult Function(
            List<SubscriptionPlan> plans, TenantSubscription? subscription)?
        loaded,
    TResult Function(String message)? error,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case BillingInitial() when initial != null:
        return initial();
      case BillingLoading() when loading != null:
        return loading();
      case BillingLoaded() when loaded != null:
        return loaded(_that.plans, _that.subscription);
      case BillingError() when error != null:
        return error(_that.message);
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
    required TResult Function(
            List<SubscriptionPlan> plans, TenantSubscription? subscription)
        loaded,
    required TResult Function(String message) error,
  }) {
    final _that = this;
    switch (_that) {
      case BillingInitial():
        return initial();
      case BillingLoading():
        return loading();
      case BillingLoaded():
        return loaded(_that.plans, _that.subscription);
      case BillingError():
        return error(_that.message);
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
    TResult? Function(
            List<SubscriptionPlan> plans, TenantSubscription? subscription)?
        loaded,
    TResult? Function(String message)? error,
  }) {
    final _that = this;
    switch (_that) {
      case BillingInitial() when initial != null:
        return initial();
      case BillingLoading() when loading != null:
        return loading();
      case BillingLoaded() when loaded != null:
        return loaded(_that.plans, _that.subscription);
      case BillingError() when error != null:
        return error(_that.message);
      case _:
        return null;
    }
  }
}

/// @nodoc

class BillingInitial implements BillingState {
  const BillingInitial();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is BillingInitial);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'BillingState.initial()';
  }
}

/// @nodoc

class BillingLoading implements BillingState {
  const BillingLoading();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is BillingLoading);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'BillingState.loading()';
  }
}

/// @nodoc

class BillingLoaded implements BillingState {
  const BillingLoaded(
      {required final List<SubscriptionPlan> plans, this.subscription})
      : _plans = plans;

  final List<SubscriptionPlan> _plans;
  List<SubscriptionPlan> get plans {
    if (_plans is EqualUnmodifiableListView) return _plans;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_plans);
  }

  final TenantSubscription? subscription;

  /// Create a copy of BillingState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $BillingLoadedCopyWith<BillingLoaded> get copyWith =>
      _$BillingLoadedCopyWithImpl<BillingLoaded>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is BillingLoaded &&
            const DeepCollectionEquality().equals(other._plans, _plans) &&
            (identical(other.subscription, subscription) ||
                other.subscription == subscription));
  }

  @override
  int get hashCode => Object.hash(
      runtimeType, const DeepCollectionEquality().hash(_plans), subscription);

  @override
  String toString() {
    return 'BillingState.loaded(plans: $plans, subscription: $subscription)';
  }
}

/// @nodoc
abstract mixin class $BillingLoadedCopyWith<$Res>
    implements $BillingStateCopyWith<$Res> {
  factory $BillingLoadedCopyWith(
          BillingLoaded value, $Res Function(BillingLoaded) _then) =
      _$BillingLoadedCopyWithImpl;
  @useResult
  $Res call({List<SubscriptionPlan> plans, TenantSubscription? subscription});
}

/// @nodoc
class _$BillingLoadedCopyWithImpl<$Res>
    implements $BillingLoadedCopyWith<$Res> {
  _$BillingLoadedCopyWithImpl(this._self, this._then);

  final BillingLoaded _self;
  final $Res Function(BillingLoaded) _then;

  /// Create a copy of BillingState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? plans = null,
    Object? subscription = freezed,
  }) {
    return _then(BillingLoaded(
      plans: null == plans
          ? _self._plans
          : plans // ignore: cast_nullable_to_non_nullable
              as List<SubscriptionPlan>,
      subscription: freezed == subscription
          ? _self.subscription
          : subscription // ignore: cast_nullable_to_non_nullable
              as TenantSubscription?,
    ));
  }
}

/// @nodoc

class BillingError implements BillingState {
  const BillingError(this.message);

  final String message;

  /// Create a copy of BillingState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $BillingErrorCopyWith<BillingError> get copyWith =>
      _$BillingErrorCopyWithImpl<BillingError>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is BillingError &&
            (identical(other.message, message) || other.message == message));
  }

  @override
  int get hashCode => Object.hash(runtimeType, message);

  @override
  String toString() {
    return 'BillingState.error(message: $message)';
  }
}

/// @nodoc
abstract mixin class $BillingErrorCopyWith<$Res>
    implements $BillingStateCopyWith<$Res> {
  factory $BillingErrorCopyWith(
          BillingError value, $Res Function(BillingError) _then) =
      _$BillingErrorCopyWithImpl;
  @useResult
  $Res call({String message});
}

/// @nodoc
class _$BillingErrorCopyWithImpl<$Res> implements $BillingErrorCopyWith<$Res> {
  _$BillingErrorCopyWithImpl(this._self, this._then);

  final BillingError _self;
  final $Res Function(BillingError) _then;

  /// Create a copy of BillingState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? message = null,
  }) {
    return _then(BillingError(
      null == message
          ? _self.message
          : message // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

// dart format on
