import 'package:boilerplate_mobile/core/di/injection.dart';
import 'package:boilerplate_mobile/core/features/home/home_page.dart';
import 'package:boilerplate_mobile/core/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:boilerplate_mobile/core/features/notifications/presentation/pages/notifications_page.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/cubit/profile_cubit.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/pages/profile_page.dart';
import 'package:boilerplate_mobile/core/modularity/module_nav_item.dart';
import 'package:boilerplate_mobile/core/modularity/module_registry.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// The authenticated shell — renders a bottom navigation bar built
/// from core nav items + module contributions, filtered by the
/// current user's permissions.
///
/// Each nav item maps to a route path. Tapping switches the displayed
/// page. Phase 3 will wire this to auto_route's nested navigation;
/// for now it uses a simple IndexedStack placeholder.
class MainShellPage extends StatefulWidget {
  const MainShellPage({required this.userPermissions, super.key});

  /// The current user's permission set, decoded from the JWT.
  final Set<String> userPermissions;

  @override
  State<MainShellPage> createState() => _MainShellPageState();
}

class _MainShellPageState extends State<MainShellPage> {
  int _currentIndex = 0;

  /// Core nav items that always exist.
  static const _coreNavItems = [
    ModuleNavItem(
      label: 'Home',
      icon: Icons.home_outlined,
      activeIcon: Icons.home,
      routePath: '/home',
      order: 0,
    ),
    ModuleNavItem(
      label: 'Notifications',
      icon: Icons.notifications_outlined,
      activeIcon: Icons.notifications,
      routePath: '/notifications',
      order: 10,
    ),
    ModuleNavItem(
      label: 'Profile',
      icon: Icons.person_outlined,
      activeIcon: Icons.person,
      routePath: '/profile',
      order: 900,
    ),
  ];

  List<ModuleNavItem> get _allNavItems {
    final moduleItems =
        ModuleRegistry.instance.collectNavItems(widget.userPermissions);
    final all = [..._coreNavItems, ...moduleItems]
      ..sort((a, b) => a.order.compareTo(b.order));
    return all;
  }

  @override
  Widget build(BuildContext context) {
    final navItems = _allNavItems;

    return Scaffold(
      body: IndexedStack(
        index: _currentIndex.clamp(0, navItems.length - 1),
        children: navItems.map(_buildTab).toList(),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex.clamp(0, navItems.length - 1),
        onDestinationSelected: (index) {
          setState(() => _currentIndex = index);
        },
        destinations: navItems
            .map(
              (item) => NavigationDestination(
                icon: Icon(item.icon),
                selectedIcon: Icon(item.activeIcon ?? item.icon),
                label: item.label,
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _buildTab(ModuleNavItem item) {
    // Module-provided pages use the pageBuilder (self-contained, no
    // shell import needed). Core pages are wired directly.
    if (item.pageBuilder != null) {
      return item.pageBuilder!();
    }
    return switch (item.routePath) {
      '/home' => const HomePage(),
      '/notifications' => BlocProvider(
          create: (_) => sl<NotificationsCubit>(),
          child: const NotificationsPage(),
        ),
      '/profile' => BlocProvider(
          create: (_) => sl<ProfileCubit>(),
          child: const ProfilePage(),
        ),
      _ => _PlaceholderTab(label: item.label, route: item.routePath),
    };
  }
}

/// Placeholder page for nav tabs not yet implemented.
class _PlaceholderTab extends StatelessWidget {
  const _PlaceholderTab({required this.label, required this.route});

  final String label;
  final String route;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(label)),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              label,
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            const SizedBox(height: 8),
            Text(
              'Route: $route',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ),
      ),
    );
  }
}
