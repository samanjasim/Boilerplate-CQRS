import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_state.dart';
import 'package:boilerplate_mobile/core/modularity/slot_builder.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Home / dashboard page — the default landing screen after login.
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final config = AppConfig.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(config.appName)),
      body: BlocBuilder<AuthCubit, AuthState>(
        builder: (context, state) {
          final user = switch (state) {
            AuthAuthenticated(:final user) => user,
            _ => null,
          };

          if (user == null) return const SizedBox.shrink();

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              // Welcome card
              Card(
                color: theme.colorScheme.primaryContainer,
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Welcome back, ${user.firstName}!',
                        style: theme.textTheme.titleLarge?.copyWith(
                          color: theme.colorScheme.onPrimaryContainer,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        user.email,
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: theme.colorScheme.onPrimaryContainer
                              .withValues(alpha: 0.7),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),

              // Quick info cards
              Row(
                children: [
                  Expanded(
                    child: _InfoCard(
                      icon: Icons.shield_outlined,
                      label: 'Role',
                      value: user.roles.isNotEmpty
                          ? user.roles.first
                          : 'User',
                      theme: theme,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _InfoCard(
                      icon: Icons.security_outlined,
                      label: '2FA',
                      value: user.twoFactorEnabled
                          ? 'Enabled'
                          : 'Disabled',
                      theme: theme,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),

              if (config.multiTenancyEnabled && user.tenantName != null)
                _InfoCard(
                  icon: Icons.business_outlined,
                  label: 'Tenant',
                  value: user.tenantName!,
                  theme: theme,
                ),

              const SizedBox(height: 16),

              // Module slots for home
              const SlotBuilder(id: 'home-cards'),
            ],
          );
        },
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({
    required this.icon,
    required this.label,
    required this.value,
    required this.theme,
  });

  final IconData icon;
  final String label;
  final String value;
  final ThemeData theme;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(icon, color: theme.colorScheme.primary, size: 24),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                  Text(value, style: theme.textTheme.titleMedium),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
