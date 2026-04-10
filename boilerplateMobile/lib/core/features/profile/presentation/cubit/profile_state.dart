import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:freezed_annotation/freezed_annotation.dart';

part 'profile_state.freezed.dart';

@freezed
sealed class ProfileState with _$ProfileState {
  const factory ProfileState.initial() = ProfileInitial;
  const factory ProfileState.loading() = ProfileLoading;
  const factory ProfileState.loaded(User user) = ProfileLoaded;
  const factory ProfileState.saving() = ProfileSaving;
  const factory ProfileState.saved() = ProfileSaved;
  const factory ProfileState.passwordChanged() = ProfilePasswordChanged;
  const factory ProfileState.error(String message) = ProfileError;
  const factory ProfileState.validationError(
    Map<String, List<String>> errors,
  ) = ProfileValidationError;
}
