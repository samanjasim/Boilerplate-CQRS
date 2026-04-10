// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'tenant_subscription_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$TenantSubscriptionDto {
  String get id;
  String get planName;
  String get planSlug;
  String get status;
  String get billingInterval;
  String get currentPeriodStart;
  String get currentPeriodEnd;
  double get lockedMonthlyPrice;
  double get lockedAnnualPrice;
  String get currency;
  bool get autoRenew;
  String get createdAt;
  String? get canceledAt;

  /// Create a copy of TenantSubscriptionDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $TenantSubscriptionDtoCopyWith<TenantSubscriptionDto> get copyWith =>
      _$TenantSubscriptionDtoCopyWithImpl<TenantSubscriptionDto>(
          this as TenantSubscriptionDto, _$identity);

  /// Serializes this TenantSubscriptionDto to a JSON map.
  Map<String, dynamic> toJson();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is TenantSubscriptionDto &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.planName, planName) ||
                other.planName == planName) &&
            (identical(other.planSlug, planSlug) ||
                other.planSlug == planSlug) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.billingInterval, billingInterval) ||
                other.billingInterval == billingInterval) &&
            (identical(other.currentPeriodStart, currentPeriodStart) ||
                other.currentPeriodStart == currentPeriodStart) &&
            (identical(other.currentPeriodEnd, currentPeriodEnd) ||
                other.currentPeriodEnd == currentPeriodEnd) &&
            (identical(other.lockedMonthlyPrice, lockedMonthlyPrice) ||
                other.lockedMonthlyPrice == lockedMonthlyPrice) &&
            (identical(other.lockedAnnualPrice, lockedAnnualPrice) ||
                other.lockedAnnualPrice == lockedAnnualPrice) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.autoRenew, autoRenew) ||
                other.autoRenew == autoRenew) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            (identical(other.canceledAt, canceledAt) ||
                other.canceledAt == canceledAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      id,
      planName,
      planSlug,
      status,
      billingInterval,
      currentPeriodStart,
      currentPeriodEnd,
      lockedMonthlyPrice,
      lockedAnnualPrice,
      currency,
      autoRenew,
      createdAt,
      canceledAt);

  @override
  String toString() {
    return 'TenantSubscriptionDto(id: $id, planName: $planName, planSlug: $planSlug, status: $status, billingInterval: $billingInterval, currentPeriodStart: $currentPeriodStart, currentPeriodEnd: $currentPeriodEnd, lockedMonthlyPrice: $lockedMonthlyPrice, lockedAnnualPrice: $lockedAnnualPrice, currency: $currency, autoRenew: $autoRenew, createdAt: $createdAt, canceledAt: $canceledAt)';
  }
}

/// @nodoc
abstract mixin class $TenantSubscriptionDtoCopyWith<$Res> {
  factory $TenantSubscriptionDtoCopyWith(TenantSubscriptionDto value,
          $Res Function(TenantSubscriptionDto) _then) =
      _$TenantSubscriptionDtoCopyWithImpl;
  @useResult
  $Res call(
      {String id,
      String planName,
      String planSlug,
      String status,
      String billingInterval,
      String currentPeriodStart,
      String currentPeriodEnd,
      double lockedMonthlyPrice,
      double lockedAnnualPrice,
      String currency,
      bool autoRenew,
      String createdAt,
      String? canceledAt});
}

/// @nodoc
class _$TenantSubscriptionDtoCopyWithImpl<$Res>
    implements $TenantSubscriptionDtoCopyWith<$Res> {
  _$TenantSubscriptionDtoCopyWithImpl(this._self, this._then);

  final TenantSubscriptionDto _self;
  final $Res Function(TenantSubscriptionDto) _then;

  /// Create a copy of TenantSubscriptionDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? planName = null,
    Object? planSlug = null,
    Object? status = null,
    Object? billingInterval = null,
    Object? currentPeriodStart = null,
    Object? currentPeriodEnd = null,
    Object? lockedMonthlyPrice = null,
    Object? lockedAnnualPrice = null,
    Object? currency = null,
    Object? autoRenew = null,
    Object? createdAt = null,
    Object? canceledAt = freezed,
  }) {
    return _then(_self.copyWith(
      id: null == id
          ? _self.id
          : id // ignore: cast_nullable_to_non_nullable
              as String,
      planName: null == planName
          ? _self.planName
          : planName // ignore: cast_nullable_to_non_nullable
              as String,
      planSlug: null == planSlug
          ? _self.planSlug
          : planSlug // ignore: cast_nullable_to_non_nullable
              as String,
      status: null == status
          ? _self.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      billingInterval: null == billingInterval
          ? _self.billingInterval
          : billingInterval // ignore: cast_nullable_to_non_nullable
              as String,
      currentPeriodStart: null == currentPeriodStart
          ? _self.currentPeriodStart
          : currentPeriodStart // ignore: cast_nullable_to_non_nullable
              as String,
      currentPeriodEnd: null == currentPeriodEnd
          ? _self.currentPeriodEnd
          : currentPeriodEnd // ignore: cast_nullable_to_non_nullable
              as String,
      lockedMonthlyPrice: null == lockedMonthlyPrice
          ? _self.lockedMonthlyPrice
          : lockedMonthlyPrice // ignore: cast_nullable_to_non_nullable
              as double,
      lockedAnnualPrice: null == lockedAnnualPrice
          ? _self.lockedAnnualPrice
          : lockedAnnualPrice // ignore: cast_nullable_to_non_nullable
              as double,
      currency: null == currency
          ? _self.currency
          : currency // ignore: cast_nullable_to_non_nullable
              as String,
      autoRenew: null == autoRenew
          ? _self.autoRenew
          : autoRenew // ignore: cast_nullable_to_non_nullable
              as bool,
      createdAt: null == createdAt
          ? _self.createdAt
          : createdAt // ignore: cast_nullable_to_non_nullable
              as String,
      canceledAt: freezed == canceledAt
          ? _self.canceledAt
          : canceledAt // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

/// Adds pattern-matching-related methods to [TenantSubscriptionDto].
extension TenantSubscriptionDtoPatterns on TenantSubscriptionDto {
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
    TResult Function(_TenantSubscriptionDto value)? $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _TenantSubscriptionDto() when $default != null:
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
    TResult Function(_TenantSubscriptionDto value) $default,
  ) {
    final _that = this;
    switch (_that) {
      case _TenantSubscriptionDto():
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
    TResult? Function(_TenantSubscriptionDto value)? $default,
  ) {
    final _that = this;
    switch (_that) {
      case _TenantSubscriptionDto() when $default != null:
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
            String planName,
            String planSlug,
            String status,
            String billingInterval,
            String currentPeriodStart,
            String currentPeriodEnd,
            double lockedMonthlyPrice,
            double lockedAnnualPrice,
            String currency,
            bool autoRenew,
            String createdAt,
            String? canceledAt)?
        $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _TenantSubscriptionDto() when $default != null:
        return $default(
            _that.id,
            _that.planName,
            _that.planSlug,
            _that.status,
            _that.billingInterval,
            _that.currentPeriodStart,
            _that.currentPeriodEnd,
            _that.lockedMonthlyPrice,
            _that.lockedAnnualPrice,
            _that.currency,
            _that.autoRenew,
            _that.createdAt,
            _that.canceledAt);
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
            String planName,
            String planSlug,
            String status,
            String billingInterval,
            String currentPeriodStart,
            String currentPeriodEnd,
            double lockedMonthlyPrice,
            double lockedAnnualPrice,
            String currency,
            bool autoRenew,
            String createdAt,
            String? canceledAt)
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _TenantSubscriptionDto():
        return $default(
            _that.id,
            _that.planName,
            _that.planSlug,
            _that.status,
            _that.billingInterval,
            _that.currentPeriodStart,
            _that.currentPeriodEnd,
            _that.lockedMonthlyPrice,
            _that.lockedAnnualPrice,
            _that.currency,
            _that.autoRenew,
            _that.createdAt,
            _that.canceledAt);
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
            String planName,
            String planSlug,
            String status,
            String billingInterval,
            String currentPeriodStart,
            String currentPeriodEnd,
            double lockedMonthlyPrice,
            double lockedAnnualPrice,
            String currency,
            bool autoRenew,
            String createdAt,
            String? canceledAt)?
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _TenantSubscriptionDto() when $default != null:
        return $default(
            _that.id,
            _that.planName,
            _that.planSlug,
            _that.status,
            _that.billingInterval,
            _that.currentPeriodStart,
            _that.currentPeriodEnd,
            _that.lockedMonthlyPrice,
            _that.lockedAnnualPrice,
            _that.currency,
            _that.autoRenew,
            _that.createdAt,
            _that.canceledAt);
      case _:
        return null;
    }
  }
}

/// @nodoc
@JsonSerializable()
class _TenantSubscriptionDto implements TenantSubscriptionDto {
  const _TenantSubscriptionDto(
      {required this.id,
      required this.planName,
      required this.planSlug,
      required this.status,
      required this.billingInterval,
      required this.currentPeriodStart,
      required this.currentPeriodEnd,
      required this.lockedMonthlyPrice,
      required this.lockedAnnualPrice,
      required this.currency,
      required this.autoRenew,
      required this.createdAt,
      this.canceledAt});
  factory _TenantSubscriptionDto.fromJson(Map<String, dynamic> json) =>
      _$TenantSubscriptionDtoFromJson(json);

  @override
  final String id;
  @override
  final String planName;
  @override
  final String planSlug;
  @override
  final String status;
  @override
  final String billingInterval;
  @override
  final String currentPeriodStart;
  @override
  final String currentPeriodEnd;
  @override
  final double lockedMonthlyPrice;
  @override
  final double lockedAnnualPrice;
  @override
  final String currency;
  @override
  final bool autoRenew;
  @override
  final String createdAt;
  @override
  final String? canceledAt;

  /// Create a copy of TenantSubscriptionDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  _$TenantSubscriptionDtoCopyWith<_TenantSubscriptionDto> get copyWith =>
      __$TenantSubscriptionDtoCopyWithImpl<_TenantSubscriptionDto>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$TenantSubscriptionDtoToJson(
      this,
    );
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _TenantSubscriptionDto &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.planName, planName) ||
                other.planName == planName) &&
            (identical(other.planSlug, planSlug) ||
                other.planSlug == planSlug) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.billingInterval, billingInterval) ||
                other.billingInterval == billingInterval) &&
            (identical(other.currentPeriodStart, currentPeriodStart) ||
                other.currentPeriodStart == currentPeriodStart) &&
            (identical(other.currentPeriodEnd, currentPeriodEnd) ||
                other.currentPeriodEnd == currentPeriodEnd) &&
            (identical(other.lockedMonthlyPrice, lockedMonthlyPrice) ||
                other.lockedMonthlyPrice == lockedMonthlyPrice) &&
            (identical(other.lockedAnnualPrice, lockedAnnualPrice) ||
                other.lockedAnnualPrice == lockedAnnualPrice) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.autoRenew, autoRenew) ||
                other.autoRenew == autoRenew) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            (identical(other.canceledAt, canceledAt) ||
                other.canceledAt == canceledAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      id,
      planName,
      planSlug,
      status,
      billingInterval,
      currentPeriodStart,
      currentPeriodEnd,
      lockedMonthlyPrice,
      lockedAnnualPrice,
      currency,
      autoRenew,
      createdAt,
      canceledAt);

  @override
  String toString() {
    return 'TenantSubscriptionDto(id: $id, planName: $planName, planSlug: $planSlug, status: $status, billingInterval: $billingInterval, currentPeriodStart: $currentPeriodStart, currentPeriodEnd: $currentPeriodEnd, lockedMonthlyPrice: $lockedMonthlyPrice, lockedAnnualPrice: $lockedAnnualPrice, currency: $currency, autoRenew: $autoRenew, createdAt: $createdAt, canceledAt: $canceledAt)';
  }
}

/// @nodoc
abstract mixin class _$TenantSubscriptionDtoCopyWith<$Res>
    implements $TenantSubscriptionDtoCopyWith<$Res> {
  factory _$TenantSubscriptionDtoCopyWith(_TenantSubscriptionDto value,
          $Res Function(_TenantSubscriptionDto) _then) =
      __$TenantSubscriptionDtoCopyWithImpl;
  @override
  @useResult
  $Res call(
      {String id,
      String planName,
      String planSlug,
      String status,
      String billingInterval,
      String currentPeriodStart,
      String currentPeriodEnd,
      double lockedMonthlyPrice,
      double lockedAnnualPrice,
      String currency,
      bool autoRenew,
      String createdAt,
      String? canceledAt});
}

/// @nodoc
class __$TenantSubscriptionDtoCopyWithImpl<$Res>
    implements _$TenantSubscriptionDtoCopyWith<$Res> {
  __$TenantSubscriptionDtoCopyWithImpl(this._self, this._then);

  final _TenantSubscriptionDto _self;
  final $Res Function(_TenantSubscriptionDto) _then;

  /// Create a copy of TenantSubscriptionDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $Res call({
    Object? id = null,
    Object? planName = null,
    Object? planSlug = null,
    Object? status = null,
    Object? billingInterval = null,
    Object? currentPeriodStart = null,
    Object? currentPeriodEnd = null,
    Object? lockedMonthlyPrice = null,
    Object? lockedAnnualPrice = null,
    Object? currency = null,
    Object? autoRenew = null,
    Object? createdAt = null,
    Object? canceledAt = freezed,
  }) {
    return _then(_TenantSubscriptionDto(
      id: null == id
          ? _self.id
          : id // ignore: cast_nullable_to_non_nullable
              as String,
      planName: null == planName
          ? _self.planName
          : planName // ignore: cast_nullable_to_non_nullable
              as String,
      planSlug: null == planSlug
          ? _self.planSlug
          : planSlug // ignore: cast_nullable_to_non_nullable
              as String,
      status: null == status
          ? _self.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      billingInterval: null == billingInterval
          ? _self.billingInterval
          : billingInterval // ignore: cast_nullable_to_non_nullable
              as String,
      currentPeriodStart: null == currentPeriodStart
          ? _self.currentPeriodStart
          : currentPeriodStart // ignore: cast_nullable_to_non_nullable
              as String,
      currentPeriodEnd: null == currentPeriodEnd
          ? _self.currentPeriodEnd
          : currentPeriodEnd // ignore: cast_nullable_to_non_nullable
              as String,
      lockedMonthlyPrice: null == lockedMonthlyPrice
          ? _self.lockedMonthlyPrice
          : lockedMonthlyPrice // ignore: cast_nullable_to_non_nullable
              as double,
      lockedAnnualPrice: null == lockedAnnualPrice
          ? _self.lockedAnnualPrice
          : lockedAnnualPrice // ignore: cast_nullable_to_non_nullable
              as double,
      currency: null == currency
          ? _self.currency
          : currency // ignore: cast_nullable_to_non_nullable
              as String,
      autoRenew: null == autoRenew
          ? _self.autoRenew
          : autoRenew // ignore: cast_nullable_to_non_nullable
              as bool,
      createdAt: null == createdAt
          ? _self.createdAt
          : createdAt // ignore: cast_nullable_to_non_nullable
              as String,
      canceledAt: freezed == canceledAt
          ? _self.canceledAt
          : canceledAt // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

// dart format on
