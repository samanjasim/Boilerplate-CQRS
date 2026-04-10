import 'package:flutter/widgets.dart';

/// Widget that conditionally renders [child] based on the user's
/// permissions.
///
/// Usage:
/// ```dart
/// PermissionGate(
///   permission: Permissions.filesUpload,
///   userPermissions: authState.permissions,
///   child: UploadButton(),
///   fallback: SizedBox.shrink(),  // optional, defaults to nothing
/// )
/// ```
///
/// The [userPermissions] set comes from the decoded JWT `permission`
/// claims, cached in AuthCubit state. Server-side enforcement is the
/// real gate — this is a UX convenience.
class PermissionGate extends StatelessWidget {
  const PermissionGate({
    required this.permission,
    required this.userPermissions,
    required this.child,
    this.fallback,
    super.key,
  });

  /// The permission string to check (e.g. `Permissions.filesUpload`).
  final String permission;

  /// The current user's permission set.
  final Set<String> userPermissions;

  /// Widget shown when the user has the permission.
  final Widget child;

  /// Widget shown when the user lacks the permission.
  /// Defaults to [SizedBox.shrink].
  final Widget? fallback;

  @override
  Widget build(BuildContext context) {
    if (userPermissions.contains(permission)) {
      return child;
    }
    return fallback ?? const SizedBox.shrink();
  }
}

/// Variant that checks multiple permissions (ALL required).
class MultiPermissionGate extends StatelessWidget {
  const MultiPermissionGate({
    required this.permissions,
    required this.userPermissions,
    required this.child,
    this.fallback,
    super.key,
  });

  final List<String> permissions;
  final Set<String> userPermissions;
  final Widget child;
  final Widget? fallback;

  @override
  Widget build(BuildContext context) {
    if (permissions.every(userPermissions.contains)) {
      return child;
    }
    return fallback ?? const SizedBox.shrink();
  }
}
