import 'package:boilerplate_mobile/core/modularity/app_module.dart';
import 'package:boilerplate_mobile/modules/billing/billing_module.dart';

/// Optional modules active in this build.
///
/// In generated apps, `rename.ps1` overwrites this file from
/// `modules.catalog.json` based on the `-Modules` flag. When the flag
/// excludes every optional module, the list is empty and the app runs
/// with core features only.
///
/// To add a new module to the boilerplate template:
/// 1. Create `lib/modules/{name}/{name}_module.dart` implementing `AppModule`.
/// 2. Add an entry under `mobileModule` / `mobileFolder` in `modules.catalog.json`.
/// 3. Import the module class above and instantiate it in the list below.
List<AppModule> activeModules() => <AppModule>[
      BillingModule(),
    ];
