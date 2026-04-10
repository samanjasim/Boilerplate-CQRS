import 'package:flutter/widgets.dart';

/// A navigation item contributed by a core feature or optional module.
///
/// The `ModuleRegistry` collects these from all active modules and
/// filters them by the current user's permissions before rendering.
class ModuleNavItem {
  const ModuleNavItem({
    required this.label,
    required this.icon,
    required this.routePath,
    this.activeIcon,
    this.requiredPermissions = const [],
    this.order = 100,
    this.pageBuilder,
  });

  /// Display label shown under the nav icon.
  final String label;

  /// Default icon.
  final IconData icon;

  /// Icon shown when this nav item is active/selected.
  final IconData? activeIcon;

  /// The route path to navigate to when tapped (e.g. '/billing').
  final String routePath;

  /// Permissions the user must have to see this nav item.
  /// Empty list means always visible.
  final List<String> requiredPermissions;

  /// Sort order — lower numbers appear first. Core features use
  /// 0–49, modules use 100+.
  final int order;

  /// Optional builder for the page widget. When provided, the shell
  /// uses this to render the tab instead of a placeholder. This lets
  /// modules self-describe their pages without the shell importing
  /// module code directly.
  final Widget Function()? pageBuilder;
}
