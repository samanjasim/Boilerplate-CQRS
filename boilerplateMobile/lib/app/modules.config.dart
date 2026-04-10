import 'package:boilerplate_mobile/core/modularity/app_module.dart';

// MODULE IMPORTS — rename.ps1 strips lines between these markers
import 'package:boilerplate_mobile/modules/billing/billing_module.dart';
// (end MODULE IMPORTS)

/// The list of optional modules active in this build.
///
/// `rename.ps1` rewrites this list based on the `-Modules` flag.
/// When all modules are stripped, the list is empty and the app
/// runs with core features only.
///
/// To add a new module:
/// 1. Create `lib/modules/{name}/{name}_module.dart` implementing `AppModule`
/// 2. Import it between the MODULE IMPORTS markers above
/// 3. Add `{Name}Module()` between the MODULE INSTANCES markers below
/// 4. Add a `mobileModule` / `mobileFolder` entry to `scripts/modules.json`
List<AppModule> activeModules() => <AppModule>[
      // MODULE INSTANCES — rename.ps1 strips lines between these markers
      BillingModule(),
      // (end MODULE INSTANCES)
    ];
