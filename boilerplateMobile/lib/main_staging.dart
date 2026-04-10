import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/app/flavors/flavor.dart';
import 'package:boilerplate_mobile/bootstrap.dart';

/// Entry point for the **staging** flavor.
///
/// Run:
/// ```bash
/// flutter run --flavor staging -t lib/main_staging.dart
/// ```
///
/// `rename.ps1` may rewrite the `multiTenancyEnabled` flag here when
/// scaffolding a single-tenant solution.
void main() => bootstrap(
      const AppConfig(
        flavor: Flavor.staging,
        // Android emulator → host machine is 10.0.2.2.
        // iOS simulator → host machine is localhost / 127.0.0.1.
        // Physical devices need the LAN IP of the dev machine.
        apiBaseUrl: 'http://10.0.2.2:5000/api/v1',
        appName: 'Starter Staging',
        multiTenancyEnabled: true,
      ),
    );
