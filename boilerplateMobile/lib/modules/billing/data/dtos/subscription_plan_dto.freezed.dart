// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'subscription_plan_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$PlanFeatureEntryDto {
  String get key;
  String get value;

  /// Create a copy of PlanFeatureEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $PlanFeatureEntryDtoCopyWith<PlanFeatureEntryDto> get copyWith =>
      _$PlanFeatureEntryDtoCopyWithImpl<PlanFeatureEntryDto>(
          this as PlanFeatureEntryDto, _$identity);

  /// Serializes this PlanFeatureEntryDto to a JSON map.
  Map<String, dynamic> toJson();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is PlanFeatureEntryDto &&
            (identical(other.key, key) || other.key == key) &&
            (identical(other.value, value) || other.value == value));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, key, value);

  @override
  String toString() {
    return 'PlanFeatureEntryDto(key: $key, value: $value)';
  }
}

/// @nodoc
abstract mixin class $PlanFeatureEntryDtoCopyWith<$Res> {
  factory $PlanFeatureEntryDtoCopyWith(
          PlanFeatureEntryDto value, $Res Function(PlanFeatureEntryDto) _then) =
      _$PlanFeatureEntryDtoCopyWithImpl;
  @useResult
  $Res call({String key, String value});
}

/// @nodoc
class _$PlanFeatureEntryDtoCopyWithImpl<$Res>
    implements $PlanFeatureEntryDtoCopyWith<$Res> {
  _$PlanFeatureEntryDtoCopyWithImpl(this._self, this._then);

  final PlanFeatureEntryDto _self;
  final $Res Function(PlanFeatureEntryDto) _then;

  /// Create a copy of PlanFeatureEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? key = null,
    Object? value = null,
  }) {
    return _then(_self.copyWith(
      key: null == key
          ? _self.key
          : key // ignore: cast_nullable_to_non_nullable
              as String,
      value: null == value
          ? _self.value
          : value // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

/// Adds pattern-matching-related methods to [PlanFeatureEntryDto].
extension PlanFeatureEntryDtoPatterns on PlanFeatureEntryDto {
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
    TResult Function(_PlanFeatureEntryDto value)? $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _PlanFeatureEntryDto() when $default != null:
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
    TResult Function(_PlanFeatureEntryDto value) $default,
  ) {
    final _that = this;
    switch (_that) {
      case _PlanFeatureEntryDto():
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
    TResult? Function(_PlanFeatureEntryDto value)? $default,
  ) {
    final _that = this;
    switch (_that) {
      case _PlanFeatureEntryDto() when $default != null:
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
    TResult Function(String key, String value)? $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _PlanFeatureEntryDto() when $default != null:
        return $default(_that.key, _that.value);
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
    TResult Function(String key, String value) $default,
  ) {
    final _that = this;
    switch (_that) {
      case _PlanFeatureEntryDto():
        return $default(_that.key, _that.value);
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
    TResult? Function(String key, String value)? $default,
  ) {
    final _that = this;
    switch (_that) {
      case _PlanFeatureEntryDto() when $default != null:
        return $default(_that.key, _that.value);
      case _:
        return null;
    }
  }
}

/// @nodoc
@JsonSerializable()
class _PlanFeatureEntryDto implements PlanFeatureEntryDto {
  const _PlanFeatureEntryDto({required this.key, required this.value});
  factory _PlanFeatureEntryDto.fromJson(Map<String, dynamic> json) =>
      _$PlanFeatureEntryDtoFromJson(json);

  @override
  final String key;
  @override
  final String value;

  /// Create a copy of PlanFeatureEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  _$PlanFeatureEntryDtoCopyWith<_PlanFeatureEntryDto> get copyWith =>
      __$PlanFeatureEntryDtoCopyWithImpl<_PlanFeatureEntryDto>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$PlanFeatureEntryDtoToJson(
      this,
    );
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _PlanFeatureEntryDto &&
            (identical(other.key, key) || other.key == key) &&
            (identical(other.value, value) || other.value == value));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, key, value);

  @override
  String toString() {
    return 'PlanFeatureEntryDto(key: $key, value: $value)';
  }
}

/// @nodoc
abstract mixin class _$PlanFeatureEntryDtoCopyWith<$Res>
    implements $PlanFeatureEntryDtoCopyWith<$Res> {
  factory _$PlanFeatureEntryDtoCopyWith(_PlanFeatureEntryDto value,
          $Res Function(_PlanFeatureEntryDto) _then) =
      __$PlanFeatureEntryDtoCopyWithImpl;
  @override
  @useResult
  $Res call({String key, String value});
}

/// @nodoc
class __$PlanFeatureEntryDtoCopyWithImpl<$Res>
    implements _$PlanFeatureEntryDtoCopyWith<$Res> {
  __$PlanFeatureEntryDtoCopyWithImpl(this._self, this._then);

  final _PlanFeatureEntryDto _self;
  final $Res Function(_PlanFeatureEntryDto) _then;

  /// Create a copy of PlanFeatureEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $Res call({
    Object? key = null,
    Object? value = null,
  }) {
    return _then(_PlanFeatureEntryDto(
      key: null == key
          ? _self.key
          : key // ignore: cast_nullable_to_non_nullable
              as String,
      value: null == value
          ? _self.value
          : value // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

/// @nodoc
mixin _$SubscriptionPlanDto {
  String get id;
  String get name;
  String get slug;
  double get monthlyPrice;
  double get annualPrice;
  String get currency;
  bool get isFree;
  bool get isActive;
  int get displayOrder;
  String? get description;
  List<PlanFeatureEntryDto> get features;
  int get trialDays;

  /// Create a copy of SubscriptionPlanDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $SubscriptionPlanDtoCopyWith<SubscriptionPlanDto> get copyWith =>
      _$SubscriptionPlanDtoCopyWithImpl<SubscriptionPlanDto>(
          this as SubscriptionPlanDto, _$identity);

  /// Serializes this SubscriptionPlanDto to a JSON map.
  Map<String, dynamic> toJson();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is SubscriptionPlanDto &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.monthlyPrice, monthlyPrice) ||
                other.monthlyPrice == monthlyPrice) &&
            (identical(other.annualPrice, annualPrice) ||
                other.annualPrice == annualPrice) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.isFree, isFree) || other.isFree == isFree) &&
            (identical(other.isActive, isActive) ||
                other.isActive == isActive) &&
            (identical(other.displayOrder, displayOrder) ||
                other.displayOrder == displayOrder) &&
            (identical(other.description, description) ||
                other.description == description) &&
            const DeepCollectionEquality().equals(other.features, features) &&
            (identical(other.trialDays, trialDays) ||
                other.trialDays == trialDays));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      id,
      name,
      slug,
      monthlyPrice,
      annualPrice,
      currency,
      isFree,
      isActive,
      displayOrder,
      description,
      const DeepCollectionEquality().hash(features),
      trialDays);

  @override
  String toString() {
    return 'SubscriptionPlanDto(id: $id, name: $name, slug: $slug, monthlyPrice: $monthlyPrice, annualPrice: $annualPrice, currency: $currency, isFree: $isFree, isActive: $isActive, displayOrder: $displayOrder, description: $description, features: $features, trialDays: $trialDays)';
  }
}

/// @nodoc
abstract mixin class $SubscriptionPlanDtoCopyWith<$Res> {
  factory $SubscriptionPlanDtoCopyWith(
          SubscriptionPlanDto value, $Res Function(SubscriptionPlanDto) _then) =
      _$SubscriptionPlanDtoCopyWithImpl;
  @useResult
  $Res call(
      {String id,
      String name,
      String slug,
      double monthlyPrice,
      double annualPrice,
      String currency,
      bool isFree,
      bool isActive,
      int displayOrder,
      String? description,
      List<PlanFeatureEntryDto> features,
      int trialDays});
}

/// @nodoc
class _$SubscriptionPlanDtoCopyWithImpl<$Res>
    implements $SubscriptionPlanDtoCopyWith<$Res> {
  _$SubscriptionPlanDtoCopyWithImpl(this._self, this._then);

  final SubscriptionPlanDto _self;
  final $Res Function(SubscriptionPlanDto) _then;

  /// Create a copy of SubscriptionPlanDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? slug = null,
    Object? monthlyPrice = null,
    Object? annualPrice = null,
    Object? currency = null,
    Object? isFree = null,
    Object? isActive = null,
    Object? displayOrder = null,
    Object? description = freezed,
    Object? features = null,
    Object? trialDays = null,
  }) {
    return _then(_self.copyWith(
      id: null == id
          ? _self.id
          : id // ignore: cast_nullable_to_non_nullable
              as String,
      name: null == name
          ? _self.name
          : name // ignore: cast_nullable_to_non_nullable
              as String,
      slug: null == slug
          ? _self.slug
          : slug // ignore: cast_nullable_to_non_nullable
              as String,
      monthlyPrice: null == monthlyPrice
          ? _self.monthlyPrice
          : monthlyPrice // ignore: cast_nullable_to_non_nullable
              as double,
      annualPrice: null == annualPrice
          ? _self.annualPrice
          : annualPrice // ignore: cast_nullable_to_non_nullable
              as double,
      currency: null == currency
          ? _self.currency
          : currency // ignore: cast_nullable_to_non_nullable
              as String,
      isFree: null == isFree
          ? _self.isFree
          : isFree // ignore: cast_nullable_to_non_nullable
              as bool,
      isActive: null == isActive
          ? _self.isActive
          : isActive // ignore: cast_nullable_to_non_nullable
              as bool,
      displayOrder: null == displayOrder
          ? _self.displayOrder
          : displayOrder // ignore: cast_nullable_to_non_nullable
              as int,
      description: freezed == description
          ? _self.description
          : description // ignore: cast_nullable_to_non_nullable
              as String?,
      features: null == features
          ? _self.features
          : features // ignore: cast_nullable_to_non_nullable
              as List<PlanFeatureEntryDto>,
      trialDays: null == trialDays
          ? _self.trialDays
          : trialDays // ignore: cast_nullable_to_non_nullable
              as int,
    ));
  }
}

/// Adds pattern-matching-related methods to [SubscriptionPlanDto].
extension SubscriptionPlanDtoPatterns on SubscriptionPlanDto {
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
    TResult Function(_SubscriptionPlanDto value)? $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _SubscriptionPlanDto() when $default != null:
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
    TResult Function(_SubscriptionPlanDto value) $default,
  ) {
    final _that = this;
    switch (_that) {
      case _SubscriptionPlanDto():
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
    TResult? Function(_SubscriptionPlanDto value)? $default,
  ) {
    final _that = this;
    switch (_that) {
      case _SubscriptionPlanDto() when $default != null:
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
            String name,
            String slug,
            double monthlyPrice,
            double annualPrice,
            String currency,
            bool isFree,
            bool isActive,
            int displayOrder,
            String? description,
            List<PlanFeatureEntryDto> features,
            int trialDays)?
        $default, {
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case _SubscriptionPlanDto() when $default != null:
        return $default(
            _that.id,
            _that.name,
            _that.slug,
            _that.monthlyPrice,
            _that.annualPrice,
            _that.currency,
            _that.isFree,
            _that.isActive,
            _that.displayOrder,
            _that.description,
            _that.features,
            _that.trialDays);
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
            String name,
            String slug,
            double monthlyPrice,
            double annualPrice,
            String currency,
            bool isFree,
            bool isActive,
            int displayOrder,
            String? description,
            List<PlanFeatureEntryDto> features,
            int trialDays)
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _SubscriptionPlanDto():
        return $default(
            _that.id,
            _that.name,
            _that.slug,
            _that.monthlyPrice,
            _that.annualPrice,
            _that.currency,
            _that.isFree,
            _that.isActive,
            _that.displayOrder,
            _that.description,
            _that.features,
            _that.trialDays);
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
            String name,
            String slug,
            double monthlyPrice,
            double annualPrice,
            String currency,
            bool isFree,
            bool isActive,
            int displayOrder,
            String? description,
            List<PlanFeatureEntryDto> features,
            int trialDays)?
        $default,
  ) {
    final _that = this;
    switch (_that) {
      case _SubscriptionPlanDto() when $default != null:
        return $default(
            _that.id,
            _that.name,
            _that.slug,
            _that.monthlyPrice,
            _that.annualPrice,
            _that.currency,
            _that.isFree,
            _that.isActive,
            _that.displayOrder,
            _that.description,
            _that.features,
            _that.trialDays);
      case _:
        return null;
    }
  }
}

/// @nodoc
@JsonSerializable()
class _SubscriptionPlanDto implements SubscriptionPlanDto {
  const _SubscriptionPlanDto(
      {required this.id,
      required this.name,
      required this.slug,
      required this.monthlyPrice,
      required this.annualPrice,
      required this.currency,
      required this.isFree,
      required this.isActive,
      required this.displayOrder,
      this.description,
      final List<PlanFeatureEntryDto> features = const [],
      this.trialDays = 0})
      : _features = features;
  factory _SubscriptionPlanDto.fromJson(Map<String, dynamic> json) =>
      _$SubscriptionPlanDtoFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  final String slug;
  @override
  final double monthlyPrice;
  @override
  final double annualPrice;
  @override
  final String currency;
  @override
  final bool isFree;
  @override
  final bool isActive;
  @override
  final int displayOrder;
  @override
  final String? description;
  final List<PlanFeatureEntryDto> _features;
  @override
  @JsonKey()
  List<PlanFeatureEntryDto> get features {
    if (_features is EqualUnmodifiableListView) return _features;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_features);
  }

  @override
  @JsonKey()
  final int trialDays;

  /// Create a copy of SubscriptionPlanDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  _$SubscriptionPlanDtoCopyWith<_SubscriptionPlanDto> get copyWith =>
      __$SubscriptionPlanDtoCopyWithImpl<_SubscriptionPlanDto>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$SubscriptionPlanDtoToJson(
      this,
    );
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _SubscriptionPlanDto &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.slug, slug) || other.slug == slug) &&
            (identical(other.monthlyPrice, monthlyPrice) ||
                other.monthlyPrice == monthlyPrice) &&
            (identical(other.annualPrice, annualPrice) ||
                other.annualPrice == annualPrice) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.isFree, isFree) || other.isFree == isFree) &&
            (identical(other.isActive, isActive) ||
                other.isActive == isActive) &&
            (identical(other.displayOrder, displayOrder) ||
                other.displayOrder == displayOrder) &&
            (identical(other.description, description) ||
                other.description == description) &&
            const DeepCollectionEquality().equals(other._features, _features) &&
            (identical(other.trialDays, trialDays) ||
                other.trialDays == trialDays));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      id,
      name,
      slug,
      monthlyPrice,
      annualPrice,
      currency,
      isFree,
      isActive,
      displayOrder,
      description,
      const DeepCollectionEquality().hash(_features),
      trialDays);

  @override
  String toString() {
    return 'SubscriptionPlanDto(id: $id, name: $name, slug: $slug, monthlyPrice: $monthlyPrice, annualPrice: $annualPrice, currency: $currency, isFree: $isFree, isActive: $isActive, displayOrder: $displayOrder, description: $description, features: $features, trialDays: $trialDays)';
  }
}

/// @nodoc
abstract mixin class _$SubscriptionPlanDtoCopyWith<$Res>
    implements $SubscriptionPlanDtoCopyWith<$Res> {
  factory _$SubscriptionPlanDtoCopyWith(_SubscriptionPlanDto value,
          $Res Function(_SubscriptionPlanDto) _then) =
      __$SubscriptionPlanDtoCopyWithImpl;
  @override
  @useResult
  $Res call(
      {String id,
      String name,
      String slug,
      double monthlyPrice,
      double annualPrice,
      String currency,
      bool isFree,
      bool isActive,
      int displayOrder,
      String? description,
      List<PlanFeatureEntryDto> features,
      int trialDays});
}

/// @nodoc
class __$SubscriptionPlanDtoCopyWithImpl<$Res>
    implements _$SubscriptionPlanDtoCopyWith<$Res> {
  __$SubscriptionPlanDtoCopyWithImpl(this._self, this._then);

  final _SubscriptionPlanDto _self;
  final $Res Function(_SubscriptionPlanDto) _then;

  /// Create a copy of SubscriptionPlanDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? slug = null,
    Object? monthlyPrice = null,
    Object? annualPrice = null,
    Object? currency = null,
    Object? isFree = null,
    Object? isActive = null,
    Object? displayOrder = null,
    Object? description = freezed,
    Object? features = null,
    Object? trialDays = null,
  }) {
    return _then(_SubscriptionPlanDto(
      id: null == id
          ? _self.id
          : id // ignore: cast_nullable_to_non_nullable
              as String,
      name: null == name
          ? _self.name
          : name // ignore: cast_nullable_to_non_nullable
              as String,
      slug: null == slug
          ? _self.slug
          : slug // ignore: cast_nullable_to_non_nullable
              as String,
      monthlyPrice: null == monthlyPrice
          ? _self.monthlyPrice
          : monthlyPrice // ignore: cast_nullable_to_non_nullable
              as double,
      annualPrice: null == annualPrice
          ? _self.annualPrice
          : annualPrice // ignore: cast_nullable_to_non_nullable
              as double,
      currency: null == currency
          ? _self.currency
          : currency // ignore: cast_nullable_to_non_nullable
              as String,
      isFree: null == isFree
          ? _self.isFree
          : isFree // ignore: cast_nullable_to_non_nullable
              as bool,
      isActive: null == isActive
          ? _self.isActive
          : isActive // ignore: cast_nullable_to_non_nullable
              as bool,
      displayOrder: null == displayOrder
          ? _self.displayOrder
          : displayOrder // ignore: cast_nullable_to_non_nullable
              as int,
      description: freezed == description
          ? _self.description
          : description // ignore: cast_nullable_to_non_nullable
              as String?,
      features: null == features
          ? _self._features
          : features // ignore: cast_nullable_to_non_nullable
              as List<PlanFeatureEntryDto>,
      trialDays: null == trialDays
          ? _self.trialDays
          : trialDays // ignore: cast_nullable_to_non_nullable
              as int,
    ));
  }
}

// dart format on
