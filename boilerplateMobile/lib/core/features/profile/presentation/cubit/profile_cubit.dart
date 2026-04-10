import 'package:boilerplate_mobile/core/error/failure.dart';
import 'package:boilerplate_mobile/core/error/result.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/usecases/change_password_usecase.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/usecases/get_profile_usecase.dart';
import 'package:boilerplate_mobile/core/features/profile/domain/usecases/update_profile_usecase.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/cubit/profile_state.dart';
import 'package:boilerplate_mobile/core/usecase/usecase.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class ProfileCubit extends Cubit<ProfileState> {
  ProfileCubit({
    required GetProfileUseCase getProfileUseCase,
    required UpdateProfileUseCase updateProfileUseCase,
    required ChangePasswordUseCase changePasswordUseCase,
    required AuthCubit authCubit,
  })  : _getProfile = getProfileUseCase,
        _updateProfile = updateProfileUseCase,
        _changePassword = changePasswordUseCase,
        _authCubit = authCubit,
        super(const ProfileState.initial());

  final GetProfileUseCase _getProfile;
  final UpdateProfileUseCase _updateProfile;
  final ChangePasswordUseCase _changePassword;
  final AuthCubit _authCubit;

  Future<void> loadProfile() async {
    emit(const ProfileState.loading());
    final result = await _getProfile(const NoParams());
    switch (result) {
      case Success(value: final user):
        emit(ProfileState.loaded(user));
      case Err(failure: final f):
        emit(ProfileState.error(f.message));
    }
  }

  Future<void> updateProfile({
    required String userId,
    required String firstName,
    required String lastName,
    required String email,
    String? phoneNumber,
  }) async {
    emit(const ProfileState.saving());
    final result = await _updateProfile(
      UpdateProfileParams(
        userId: userId,
        firstName: firstName,
        lastName: lastName,
        email: email,
        phoneNumber: phoneNumber,
      ),
    );
    switch (result) {
      case Success():
        // Refresh the global auth state so other screens see updated user.
        await _authCubit.refreshUser();
        emit(const ProfileState.saved());
        // Re-load to show updated data.
        await loadProfile();
      case Err(failure: ValidationFailure(:final errors)):
        emit(ProfileState.validationError(errors));
      case Err(failure: final f):
        emit(ProfileState.error(f.message));
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    emit(const ProfileState.saving());
    final result = await _changePassword(
      ChangePasswordParams(
        currentPassword: currentPassword,
        newPassword: newPassword,
        confirmNewPassword: confirmNewPassword,
      ),
    );
    switch (result) {
      case Success():
        emit(const ProfileState.passwordChanged());
      case Err(failure: ValidationFailure(:final errors)):
        emit(ProfileState.validationError(errors));
      case Err(failure: final f):
        emit(ProfileState.error(f.message));
    }
  }
}
