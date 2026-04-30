// AUTO-GENERATED — DO NOT EDIT.
// Regenerate with `npm run generate:modules` from the repo root.
// CI fails on drift; modules.catalog.json is the single source of truth.
//
// Source: modules.catalog.json

import 'package:boilerplate_mobile/core/modularity/app_module.dart';
import 'package:boilerplate_mobile/modules/billing/billing_module.dart';

/// Optional modules active in this build.
///
/// In generated apps, `rename.ps1` regenerates this file from
/// `modules.catalog.json` based on the `-Modules` flag. When the flag
/// excludes every optional module, the list is empty and the app runs
/// with core features only.
List<AppModule> activeModules() => <AppModule>[
      BillingModule(),
    ];
