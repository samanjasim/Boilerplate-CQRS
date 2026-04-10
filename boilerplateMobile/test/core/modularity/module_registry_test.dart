import 'package:boilerplate_mobile/core/modularity/app_module.dart';
import 'package:boilerplate_mobile/core/modularity/module_nav_item.dart';
import 'package:boilerplate_mobile/core/modularity/module_registry.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:get_it/get_it.dart';

class _FakeModuleA extends AppModule {
  @override
  String get name => 'module_a';
  @override
  String get displayName => 'Module A';
  @override
  String get version => '1.0.0';
  @override
  List<String> get dependencies => [];

  bool registered = false;

  @override
  void registerDependencies(GetIt sl) => registered = true;

  @override
  List<ModuleNavItem> getNavItems() => [
        const ModuleNavItem(
          label: 'A',
          icon: Icons.abc,
          routePath: '/a',
          requiredPermissions: ['A.View'],
          order: 100,
        ),
      ];
}

class _FakeModuleB extends AppModule {
  @override
  String get name => 'module_b';
  @override
  String get displayName => 'Module B';
  @override
  String get version => '1.0.0';
  @override
  List<String> get dependencies => ['module_a'];

  bool registered = false;

  @override
  void registerDependencies(GetIt sl) => registered = true;

  @override
  List<ModuleNavItem> getNavItems() => [
        const ModuleNavItem(
          label: 'B',
          icon: Icons.abc,
          routePath: '/b',
          order: 200,
        ),
      ];
}

class _FakeModuleMissingDep extends AppModule {
  @override
  String get name => 'missing_dep';
  @override
  String get displayName => 'Missing Dep';
  @override
  String get version => '1.0.0';
  @override
  List<String> get dependencies => ['nonexistent'];

  @override
  void registerDependencies(GetIt sl) {}

  @override
  List<ModuleNavItem> getNavItems() => [];
}

void main() {
  late ModuleRegistry registry;
  late GetIt sl;

  setUp(() {
    registry = ModuleRegistry.instance;
    sl = GetIt.instance;
    sl.reset();
    // Reset the registry by re-initializing with empty list.
    registry.init([], sl);
  });

  group('ModuleRegistry', () {
    test('boots cleanly with empty module list', () {
      registry.init([], sl);
      expect(registry.activeModules, isEmpty);
      expect(registry.hasModules, isFalse);
    });

    test('registers modules and calls registerDependencies', () {
      final a = _FakeModuleA();
      final b = _FakeModuleB();

      registry.init([b, a], sl); // B depends on A — should sort correctly.

      expect(registry.activeModules, hasLength(2));
      expect(a.registered, isTrue);
      expect(b.registered, isTrue);
    });

    test('topologically sorts modules by dependency', () {
      final a = _FakeModuleA();
      final b = _FakeModuleB();

      // Pass B before A — registry should sort A first.
      registry.init([b, a], sl);

      expect(registry.activeModules[0].name, 'module_a');
      expect(registry.activeModules[1].name, 'module_b');
    });

    test('throws on missing dependency', () {
      expect(
        () => registry.init([_FakeModuleMissingDep()], sl),
        throwsA(isA<StateError>()),
      );
    });

    test('collectNavItems filters by permissions', () {
      final a = _FakeModuleA();
      final b = _FakeModuleB();
      registry.init([a, b], sl);

      // User has no permissions — A requires A.View, B has no requirements.
      final noPerms = registry.collectNavItems({});
      expect(noPerms, hasLength(1));
      expect(noPerms[0].label, 'B');

      // User has A.View — both visible.
      final withPerms = registry.collectNavItems({'A.View'});
      expect(withPerms, hasLength(2));
    });

    test('collectNavItems sorts by order', () {
      final a = _FakeModuleA();
      final b = _FakeModuleB();
      registry.init([a, b], sl);

      final items = registry.collectNavItems({'A.View'});
      expect(items[0].order, lessThan(items[1].order));
    });
  });
}
