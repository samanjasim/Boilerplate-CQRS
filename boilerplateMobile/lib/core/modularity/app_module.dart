import 'package:boilerplate_mobile/core/modularity/module_nav_item.dart';
import 'package:boilerplate_mobile/core/modularity/module_permission.dart';
import 'package:boilerplate_mobile/core/modularity/slot_contribution.dart';
import 'package:get_it/get_it.dart';

/// Contract that every optional module implements.
///
/// Mirrors the BE `IModule` interface. Each module is a self-contained
/// feature that can be included or excluded at scaffold time via
/// `rename.ps1`. The app boots cleanly regardless of which modules
/// are active — removing a module simply removes its nav items,
/// routes, and DI registrations.
///
/// Core features (Auth, Profile, Notifications) do NOT implement this
/// — they are always present and register through the standard DI
/// pipeline. Only strippable optional modules use `AppModule`.
abstract class AppModule {
  /// Unique identifier, e.g. `billing`. Must match the key in
  /// `scripts/modules.json`.
  String get name;

  /// Human-readable label for debugging and logs.
  String get displayName;

  /// Semver version string.
  String get version;

  /// Names of other modules this module depends on. The registry
  /// topologically sorts modules and throws if a dependency is missing.
  List<String> get dependencies;

  /// Register datasources, repositories, use cases, capability
  /// implementations, and cubits with the DI container.
  ///
  /// Called once during bootstrap, after core services are registered
  /// and before `runApp`.
  void registerDependencies(GetIt sl);

  /// Navigation items this module contributes to the bottom nav / drawer.
  /// Filtered by the user's permissions at render time.
  List<ModuleNavItem> getNavItems();

  /// Slot contributions — widgets injected into named slots in core
  /// pages. Keyed by slot ID (e.g. `profile-info`, `home-cards`).
  Map<String, SlotContribution> getSlotContributions() => {};

  /// Permissions this module declares. Documentation/test use only;
  /// real enforcement is server-side.
  List<ModulePermission> getDeclaredPermissions() => [];
}
