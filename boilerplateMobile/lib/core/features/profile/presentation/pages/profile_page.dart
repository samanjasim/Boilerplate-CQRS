import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/core/features/auth/domain/entities/user.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_state.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/cubit/profile_cubit.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/cubit/profile_state.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/pages/change_password_page.dart';
import 'package:boilerplate_mobile/core/features/profile/presentation/pages/edit_profile_page.dart';
import 'package:boilerplate_mobile/core/modularity/slot_builder.dart';
import 'package:boilerplate_mobile/core/widgets/error_view.dart';
import 'package:boilerplate_mobile/core/widgets/loading_view.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  @override
  void initState() {
    super.initState();
    context.read<ProfileCubit>().loadProfile();
  }

  @override
  Widget build(BuildContext context) {
    final config = AppConfig.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            onPressed: () => context.read<AuthCubit>().logout(),
          ),
        ],
      ),
      body: BlocConsumer<ProfileCubit, ProfileState>(
        listener: (context, state) {
          if (state is ProfileSaved) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Profile updated')),
            );
          } else if (state is ProfilePasswordChanged) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Password changed')),
            );
          } else if (state is ProfileError) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.message),
                backgroundColor: theme.colorScheme.error,
              ),
            );
          }
        },
        builder: (context, state) => switch (state) {
          ProfileLoading() || ProfileInitial() =>
            const LoadingView(message: 'Loading profile...'),
          ProfileError(:final message) => ErrorView(
              message: message,
              onRetry: () => context.read<ProfileCubit>().loadProfile(),
            ),
          ProfileLoaded(:final user) => _ProfileContent(
              user: user,
              multiTenancyEnabled: config.multiTenancyEnabled,
            ),
          // While saving/after save, keep showing the last loaded data.
          _ => _buildFromAuthState(context, config),
        },
      ),
    );
  }

  Widget _buildFromAuthState(BuildContext context, AppConfig config) {
    final authState = context.read<AuthCubit>().state;
    if (authState is AuthAuthenticated) {
      return _ProfileContent(
        user: authState.user,
        multiTenancyEnabled: config.multiTenancyEnabled,
      );
    }
    return const LoadingView();
  }
}

class _ProfileContent extends StatelessWidget {
  const _ProfileContent({
    required this.user,
    required this.multiTenancyEnabled,
  });

  final User user;
  final bool multiTenancyEnabled;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // User avatar + name
        Center(
          child: Column(
            children: [
              CircleAvatar(
                radius: 48,
                backgroundColor: theme.colorScheme.primaryContainer,
                child: Text(
                  _initials(user),
                  style: theme.textTheme.headlineMedium?.copyWith(
                    color: theme.colorScheme.onPrimaryContainer,
                  ),
                ),
              ),
              const SizedBox(height: 12),
              Text(user.fullName, style: theme.textTheme.titleLarge),
              const SizedBox(height: 4),
              Text(
                user.email,
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              if (user.roles.isNotEmpty) ...[
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  children: user.roles
                      .map(
                        (role) => Chip(
                          label: Text(
                            role,
                            style: theme.textTheme.bodySmall,
                          ),
                          materialTapTargetSize:
                              MaterialTapTargetSize.shrinkWrap,
                          visualDensity: VisualDensity.compact,
                        ),
                      )
                      .toList(),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 24),

        // Info card
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _InfoTile(
                  icon: Icons.person_outline,
                  label: 'Name',
                  value: user.fullName,
                ),
                _InfoTile(
                  icon: Icons.email_outlined,
                  label: 'Email',
                  value: user.email,
                ),
                if (user.phoneNumber != null && user.phoneNumber!.isNotEmpty)
                  _InfoTile(
                    icon: Icons.phone_outlined,
                    label: 'Phone',
                    value: user.phoneNumber!,
                  ),
                _InfoTile(
                  icon: Icons.verified_user_outlined,
                  label: 'Two-Factor Auth',
                  value: user.twoFactorEnabled ? 'Enabled' : 'Disabled',
                ),
                // Tenant info — conditionally hidden when multi-tenancy is off
                if (multiTenancyEnabled && user.tenantName != null)
                  _InfoTile(
                    icon: Icons.business_outlined,
                    label: 'Tenant',
                    value: user.tenantName!,
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),

        // Module slot — modules can inject widgets here
        const SlotBuilder(id: 'profile-info'),
        const SizedBox(height: 16),

        // Actions
        OutlinedButton.icon(
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (_) => BlocProvider.value(
                value: context.read<ProfileCubit>(),
                child: EditProfilePage(user: user),
              ),
            ),
          ),
          icon: const Icon(Icons.edit_outlined),
          label: const Text('Edit Profile'),
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (_) => BlocProvider.value(
                value: context.read<ProfileCubit>(),
                child: const ChangePasswordPage(),
              ),
            ),
          ),
          icon: const Icon(Icons.lock_outline),
          label: const Text('Change Password'),
        ),
      ],
    );
  }

  String _initials(User user) {
    final first = user.firstName.isNotEmpty ? user.firstName[0] : '';
    final last = user.lastName.isNotEmpty ? user.lastName[0] : '';
    return '$first$last'.toUpperCase();
  }
}

class _InfoTile extends StatelessWidget {
  const _InfoTile({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Icon(icon, size: 20, color: theme.colorScheme.onSurfaceVariant),
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
                Text(value, style: theme.textTheme.bodyLarge),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
