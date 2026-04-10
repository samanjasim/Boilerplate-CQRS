/// A permission declared by a module.
///
/// Used for documentation and test assertions — actual authorization
/// happens server-side. Mirrors `IModule.GetPermissions()` on the BE.
class ModulePermission {
  const ModulePermission({
    required this.key,
    required this.displayName,
    required this.module,
  });

  /// The permission string, e.g. `Billing.View`.
  final String key;

  /// Human-readable label, e.g. `View billing information`.
  final String displayName;

  /// The module that declares this permission.
  final String module;
}
