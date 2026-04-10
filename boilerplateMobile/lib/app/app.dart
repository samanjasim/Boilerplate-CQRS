import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/app/shell/main_shell_page.dart';
import 'package:boilerplate_mobile/app/theme/app_theme.dart';
import 'package:boilerplate_mobile/core/di/injection.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_state.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/login_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/pages/login_page.dart';
import 'package:boilerplate_mobile/core/widgets/loading_view.dart';
import 'package:boilerplate_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

/// Root of the widget tree.
///
/// Provides the global `AuthCubit`, localization delegates (en + ar),
/// and routes between login and shell based on auth state.
class App extends StatelessWidget {
  const App({required this.config, super.key});

  final AppConfig config;

  @override
  Widget build(BuildContext context) {
    return AppConfigScope(
      config: config,
      child: BlocProvider<AuthCubit>.value(
        value: sl<AuthCubit>(),
        child: MaterialApp(
          title: config.appName,
          debugShowCheckedModeBanner: false,
          theme: AppTheme.light(),
          darkTheme: AppTheme.dark(),

          // --- Localization ---
          localizationsDelegates: const [
            AppLocalizations.delegate,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          supportedLocales: AppLocalizations.supportedLocales,

          home: const _AuthGate(),
        ),
      ),
    );
  }
}

/// Listens to `AuthCubit` and switches between splash / login / shell.
class _AuthGate extends StatelessWidget {
  const _AuthGate();

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<AuthCubit, AuthState>(
      builder: (context, state) => switch (state) {
        AuthInitial() => const Scaffold(
            body: LoadingView(message: 'Loading...'),
          ),
        AuthUnauthenticated() => BlocProvider(
            create: (_) => sl<LoginCubit>(),
            child: const LoginPage(),
          ),
        AuthAuthenticated(:final permissions) => MainShellPage(
            userPermissions: permissions,
          ),
      },
    );
  }
}
