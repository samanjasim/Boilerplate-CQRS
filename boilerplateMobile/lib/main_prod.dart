import 'package:boilerplate_mobile/app/app_config.dart';
import 'package:boilerplate_mobile/app/flavors/flavor.dart';
import 'package:boilerplate_mobile/bootstrap.dart';

/// Entry point for the **prod** flavor.
///
/// Run:
/// ```bash
/// flutter run --flavor prod -t lib/main_prod.dart
/// flutter build apk --flavor prod -t lib/main_prod.dart --release
/// ```
///
/// The prod API URL is a placeholder — `rename.ps1` rewrites it to the
/// solution-specific production endpoint when scaffolding a new client.
void main() => bootstrap(
      const AppConfig(
        flavor: Flavor.prod,
        apiBaseUrl: 'https://api.example.com/api/v1',
        appName: 'Starter',
        multiTenancyEnabled: true,
      ),
    );
