import 'package:boilerplate_mobile/core/modularity/app_module.dart';
import 'package:boilerplate_mobile/core/modularity/module_nav_item.dart';
import 'package:boilerplate_mobile/core/modularity/slot_contribution.dart';
import 'package:flutter/widgets.dart';
import 'package:get_it/get_it.dart';

/// Central registry for optional modules.
///
/// Populated once at bootstrap from `modules.config.dart`, it:
/// 1. Topologically sorts modules by declared dependencies.
/// 2. Calls each module's `registerDependencies` in order.
/// 3. Collects nav items, slot contributions, and permissions for
///    use by the shell and core pages.
///
/// If the `activeModules()` list in `modules.config.dart` is empty
/// (all modules stripped by `rename.ps1`), the registry is empty and
/// the app runs with core features only.
class ModuleRegistry {
  ModuleRegistry._();

  static final ModuleRegistry instance = ModuleRegistry._();

  final List<AppModule> _modules = [];

  /// All active modules after initialisation.
  List<AppModule> get activeModules => List.unmodifiable(_modules);

  /// Whether any modules are registered.
  bool get hasModules => _modules.isNotEmpty;

  /// Initialise the registry with the provided modules.
  ///
  /// Sorts by dependencies, validates that all declared dependencies
  /// are present, and calls `registerDependencies` on each module.
  void init(List<AppModule> modules, GetIt sl) {
    _modules
      ..clear()
      ..addAll(_topologicalSort(modules));

    for (final module in _modules) {
      module.registerDependencies(sl);
    }
  }

  /// Collect nav items from all modules, filtered by the user's
  /// permissions and sorted by [ModuleNavItem.order].
  List<ModuleNavItem> collectNavItems(Set<String> userPermissions) {
    final items = <ModuleNavItem>[];
    for (final module in _modules) {
      for (final item in module.getNavItems()) {
        if (item.requiredPermissions.isEmpty ||
            item.requiredPermissions.every(userPermissions.contains)) {
          items.add(item);
        }
      }
    }
    items.sort((a, b) => a.order.compareTo(b.order));
    return items;
  }

  /// Build all widgets contributed to a named [slotId], sorted by order.
  List<Widget> buildSlot(
    String slotId,
    BuildContext context, {
    Object? args,
  }) {
    final contributions = <SlotContribution>[];
    for (final module in _modules) {
      final contribution = module.getSlotContributions()[slotId];
      if (contribution != null) {
        contributions.add(contribution);
      }
    }
    contributions.sort((a, b) => a.order.compareTo(b.order));
    return contributions
        .map((c) => c.builder(context, args: args))
        .toList();
  }

  /// Topological sort with dependency validation.
  ///
  /// Throws [StateError] if a module declares a dependency that isn't
  /// in the active modules list.
  List<AppModule> _topologicalSort(List<AppModule> modules) {
    final byName = <String, AppModule>{};
    for (final m in modules) {
      byName[m.name] = m;
    }

    // Validate all dependencies exist.
    for (final m in modules) {
      for (final dep in m.dependencies) {
        if (!byName.containsKey(dep)) {
          throw StateError(
            'Module "${m.name}" depends on "$dep" which is not active. '
            'Either add "$dep" to activeModules() or remove the dependency.',
          );
        }
      }
    }

    // Kahn's algorithm.
    final inDegree = <String, int>{for (final m in modules) m.name: 0};
    final dependents = <String, List<String>>{
      for (final m in modules) m.name: [],
    };

    for (final m in modules) {
      for (final dep in m.dependencies) {
        inDegree[m.name] = (inDegree[m.name] ?? 0) + 1;
        dependents[dep]!.add(m.name);
      }
    }

    final queue = <String>[
      for (final entry in inDegree.entries)
        if (entry.value == 0) entry.key,
    ];
    final sorted = <AppModule>[];

    while (queue.isNotEmpty) {
      final name = queue.removeAt(0);
      sorted.add(byName[name]!);
      for (final dep in dependents[name]!) {
        inDegree[dep] = inDegree[dep]! - 1;
        if (inDegree[dep] == 0) {
          queue.add(dep);
        }
      }
    }

    if (sorted.length != modules.length) {
      throw StateError(
        'Circular dependency detected among modules: '
        '${modules.map((m) => m.name).toSet().difference(sorted.map((m) => m.name).toSet())}',
      );
    }

    return sorted;
  }
}
