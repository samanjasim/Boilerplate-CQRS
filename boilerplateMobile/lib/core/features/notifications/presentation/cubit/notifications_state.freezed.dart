// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'notifications_state.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// dart format off
T _$identity<T>(T value) => value;

/// @nodoc
mixin _$NotificationsState {
  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is NotificationsState);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'NotificationsState()';
  }
}

/// @nodoc
class $NotificationsStateCopyWith<$Res> {
  $NotificationsStateCopyWith(
      NotificationsState _, $Res Function(NotificationsState) __);
}

/// Adds pattern-matching-related methods to [NotificationsState].
extension NotificationsStatePatterns on NotificationsState {
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
    TResult Function(NotificationsInitial value)? initial,
    TResult Function(NotificationsLoading value)? loading,
    TResult Function(NotificationsLoaded value)? loaded,
    TResult Function(NotificationsError value)? error,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case NotificationsInitial() when initial != null:
        return initial(_that);
      case NotificationsLoading() when loading != null:
        return loading(_that);
      case NotificationsLoaded() when loaded != null:
        return loaded(_that);
      case NotificationsError() when error != null:
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
    required TResult Function(NotificationsInitial value) initial,
    required TResult Function(NotificationsLoading value) loading,
    required TResult Function(NotificationsLoaded value) loaded,
    required TResult Function(NotificationsError value) error,
  }) {
    final _that = this;
    switch (_that) {
      case NotificationsInitial():
        return initial(_that);
      case NotificationsLoading():
        return loading(_that);
      case NotificationsLoaded():
        return loaded(_that);
      case NotificationsError():
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
    TResult? Function(NotificationsInitial value)? initial,
    TResult? Function(NotificationsLoading value)? loading,
    TResult? Function(NotificationsLoaded value)? loaded,
    TResult? Function(NotificationsError value)? error,
  }) {
    final _that = this;
    switch (_that) {
      case NotificationsInitial() when initial != null:
        return initial(_that);
      case NotificationsLoading() when loading != null:
        return loading(_that);
      case NotificationsLoaded() when loaded != null:
        return loaded(_that);
      case NotificationsError() when error != null:
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
    TResult Function(List<NotificationItem> notifications,
            PaginationMeta pagination, int unreadCount, bool isLoadingMore)?
        loaded,
    TResult Function(String message)? error,
    required TResult orElse(),
  }) {
    final _that = this;
    switch (_that) {
      case NotificationsInitial() when initial != null:
        return initial();
      case NotificationsLoading() when loading != null:
        return loading();
      case NotificationsLoaded() when loaded != null:
        return loaded(_that.notifications, _that.pagination, _that.unreadCount,
            _that.isLoadingMore);
      case NotificationsError() when error != null:
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
    required TResult Function(List<NotificationItem> notifications,
            PaginationMeta pagination, int unreadCount, bool isLoadingMore)
        loaded,
    required TResult Function(String message) error,
  }) {
    final _that = this;
    switch (_that) {
      case NotificationsInitial():
        return initial();
      case NotificationsLoading():
        return loading();
      case NotificationsLoaded():
        return loaded(_that.notifications, _that.pagination, _that.unreadCount,
            _that.isLoadingMore);
      case NotificationsError():
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
    TResult? Function(List<NotificationItem> notifications,
            PaginationMeta pagination, int unreadCount, bool isLoadingMore)?
        loaded,
    TResult? Function(String message)? error,
  }) {
    final _that = this;
    switch (_that) {
      case NotificationsInitial() when initial != null:
        return initial();
      case NotificationsLoading() when loading != null:
        return loading();
      case NotificationsLoaded() when loaded != null:
        return loaded(_that.notifications, _that.pagination, _that.unreadCount,
            _that.isLoadingMore);
      case NotificationsError() when error != null:
        return error(_that.message);
      case _:
        return null;
    }
  }
}

/// @nodoc

class NotificationsInitial implements NotificationsState {
  const NotificationsInitial();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is NotificationsInitial);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'NotificationsState.initial()';
  }
}

/// @nodoc

class NotificationsLoading implements NotificationsState {
  const NotificationsLoading();

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType && other is NotificationsLoading);
  }

  @override
  int get hashCode => runtimeType.hashCode;

  @override
  String toString() {
    return 'NotificationsState.loading()';
  }
}

/// @nodoc

class NotificationsLoaded implements NotificationsState {
  const NotificationsLoaded(
      {required final List<NotificationItem> notifications,
      required this.pagination,
      required this.unreadCount,
      this.isLoadingMore = false})
      : _notifications = notifications;

  final List<NotificationItem> _notifications;
  List<NotificationItem> get notifications {
    if (_notifications is EqualUnmodifiableListView) return _notifications;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_notifications);
  }

  final PaginationMeta pagination;
  final int unreadCount;
  @JsonKey()
  final bool isLoadingMore;

  /// Create a copy of NotificationsState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $NotificationsLoadedCopyWith<NotificationsLoaded> get copyWith =>
      _$NotificationsLoadedCopyWithImpl<NotificationsLoaded>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is NotificationsLoaded &&
            const DeepCollectionEquality()
                .equals(other._notifications, _notifications) &&
            (identical(other.pagination, pagination) ||
                other.pagination == pagination) &&
            (identical(other.unreadCount, unreadCount) ||
                other.unreadCount == unreadCount) &&
            (identical(other.isLoadingMore, isLoadingMore) ||
                other.isLoadingMore == isLoadingMore));
  }

  @override
  int get hashCode => Object.hash(
      runtimeType,
      const DeepCollectionEquality().hash(_notifications),
      pagination,
      unreadCount,
      isLoadingMore);

  @override
  String toString() {
    return 'NotificationsState.loaded(notifications: $notifications, pagination: $pagination, unreadCount: $unreadCount, isLoadingMore: $isLoadingMore)';
  }
}

/// @nodoc
abstract mixin class $NotificationsLoadedCopyWith<$Res>
    implements $NotificationsStateCopyWith<$Res> {
  factory $NotificationsLoadedCopyWith(
          NotificationsLoaded value, $Res Function(NotificationsLoaded) _then) =
      _$NotificationsLoadedCopyWithImpl;
  @useResult
  $Res call(
      {List<NotificationItem> notifications,
      PaginationMeta pagination,
      int unreadCount,
      bool isLoadingMore});

  $PaginationMetaCopyWith<$Res> get pagination;
}

/// @nodoc
class _$NotificationsLoadedCopyWithImpl<$Res>
    implements $NotificationsLoadedCopyWith<$Res> {
  _$NotificationsLoadedCopyWithImpl(this._self, this._then);

  final NotificationsLoaded _self;
  final $Res Function(NotificationsLoaded) _then;

  /// Create a copy of NotificationsState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? notifications = null,
    Object? pagination = null,
    Object? unreadCount = null,
    Object? isLoadingMore = null,
  }) {
    return _then(NotificationsLoaded(
      notifications: null == notifications
          ? _self._notifications
          : notifications // ignore: cast_nullable_to_non_nullable
              as List<NotificationItem>,
      pagination: null == pagination
          ? _self.pagination
          : pagination // ignore: cast_nullable_to_non_nullable
              as PaginationMeta,
      unreadCount: null == unreadCount
          ? _self.unreadCount
          : unreadCount // ignore: cast_nullable_to_non_nullable
              as int,
      isLoadingMore: null == isLoadingMore
          ? _self.isLoadingMore
          : isLoadingMore // ignore: cast_nullable_to_non_nullable
              as bool,
    ));
  }

  /// Create a copy of NotificationsState
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $PaginationMetaCopyWith<$Res> get pagination {
    return $PaginationMetaCopyWith<$Res>(_self.pagination, (value) {
      return _then(_self.copyWith(pagination: value));
    });
  }
}

/// @nodoc

class NotificationsError implements NotificationsState {
  const NotificationsError(this.message);

  final String message;

  /// Create a copy of NotificationsState
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @pragma('vm:prefer-inline')
  $NotificationsErrorCopyWith<NotificationsError> get copyWith =>
      _$NotificationsErrorCopyWithImpl<NotificationsError>(this, _$identity);

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is NotificationsError &&
            (identical(other.message, message) || other.message == message));
  }

  @override
  int get hashCode => Object.hash(runtimeType, message);

  @override
  String toString() {
    return 'NotificationsState.error(message: $message)';
  }
}

/// @nodoc
abstract mixin class $NotificationsErrorCopyWith<$Res>
    implements $NotificationsStateCopyWith<$Res> {
  factory $NotificationsErrorCopyWith(
          NotificationsError value, $Res Function(NotificationsError) _then) =
      _$NotificationsErrorCopyWithImpl;
  @useResult
  $Res call({String message});
}

/// @nodoc
class _$NotificationsErrorCopyWithImpl<$Res>
    implements $NotificationsErrorCopyWith<$Res> {
  _$NotificationsErrorCopyWithImpl(this._self, this._then);

  final NotificationsError _self;
  final $Res Function(NotificationsError) _then;

  /// Create a copy of NotificationsState
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  $Res call({
    Object? message = null,
  }) {
    return _then(NotificationsError(
      null == message
          ? _self.message
          : message // ignore: cast_nullable_to_non_nullable
              as String,
    ));
  }
}

// dart format on
