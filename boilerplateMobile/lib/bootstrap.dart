import 'dart:async';

import 'package:boilerplate_mobile/app/app.dart';
import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/core/di/injection.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';

/// Shared bootstrap entry point. Both `main_staging.dart` and
/// `main_prod.dart` call [bootstrap] with a flavor-specific [AppConfig].
///
/// Initialisation order:
/// 1. Flutter binding
/// 2. DI container (registers SecureStorage, Hive, Dio, etc.)
/// 3. Error handlers
/// 4. `runApp`
Future<void> bootstrap(AppConfig config) async {
  WidgetsFlutterBinding.ensureInitialized();

  // Wire up dependency injection (Hive init happens inside).
  await configureDependencies(config);

  FlutterError.onError = (FlutterErrorDetails details) {
    FlutterError.presentError(details);
    if (kReleaseMode) {
      // Forward to crash reporter capability when one is installed.
    }
  };

  // Check for stored session — AuthCubit emits authenticated/unauthenticated.
  unawaited(sl<AuthCubit>().checkSession());

  await runZonedGuarded<Future<void>>(
    () async {
      runApp(App(config: config));
    },
    (Object error, StackTrace stack) {
      debugPrint('Uncaught error: $error\n$stack');
    },
  );
}
