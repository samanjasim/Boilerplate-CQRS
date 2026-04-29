# Tier 2.5 — Theme 6: Mobile Second Module + Capability Contracts

> **Status:** Designed, not yet executing. Pick this up after Theme 5 has been merged. Spec: [`2026-04-29-modularity-tier-2-5-hardening.md`](../specs/2026-04-29-modularity-tier-2-5-hardening.md) §2 Theme 6.

**Goal:** Add Communication as the second mobile module so the modularity invariants (capability contracts, slot contributions, NullObject fallbacks, dependency-order DI, removal cleanup) are exercised by something other than Billing. Theme 6 is the "stress test" for everything Themes 1–5 build.

**Why last.** Until a second mobile module exists, every claim about mobile modularity is theoretical. The mobile audit explicitly noted: "the scaffold is usable for a single module (Billing); adding a second would take 30–60 minutes of manual work and expose gaps." This theme runs that experiment intentionally and bakes the gaps' fixes into the boilerplate.

---

## Why Communication

The catalog has 7 optional modules with a `frontendFeature`; only Billing has `mobileModule`. Of the remaining six, Communication is the strongest candidate:

- **It's already a peer dependency** (workflow → communication via the catalog `dependencies` chain), so wiring it on mobile validates the dependency-resolution logic for mobile modules too.
- **It has a clear mobile use case:** notification preferences, push notification opt-in, communication history viewer, in-app channel toggles. Other admin-heavy modules (Webhooks, ImportExport) don't make sense as mobile UIs.
- **It naturally needs a capability contract:** `IPushNotificationCarrier` is a real abstraction that core (e.g. AuthCubit triggering a welcome push) would call. Communication implements it; null fallback is a silent no-op. Forces the "core depends on capability, module provides impl" pattern across mobile.

Alternative considered: Products. Rejected — Products is currently web-only in the catalog (`supportedPlatforms: ["backend", "web"]`), and mobile e-commerce UI is a much larger product question than a modularity proof.

---

## What "second mobile module" includes

The full module skeleton mirrors Billing's structure under `boilerplateMobile/lib/modules/billing/`:

```
lib/modules/communication/
├── communication_module.dart            # AppModule impl: nav items, permissions, slot contributions, DI
├── data/
│   ├── datasources/
│   │   └── communication_remote_datasource.dart   # Dio-backed
│   ├── dtos/
│   │   ├── notification_preference_dto.dart       # freezed
│   │   └── notification_preference_dto.freezed.dart
│   └── repositories/
│       └── communication_repository_impl.dart
├── domain/
│   ├── entities/
│   │   └── notification_preference.dart
│   ├── repositories/
│   │   └── communication_repository.dart
│   └── usecases/
│       ├── get_notification_preferences_usecase.dart
│       └── update_notification_preference_usecase.dart
└── presentation/
    ├── cubit/
    │   ├── notification_preferences_cubit.dart
    │   └── notification_preferences_state.dart
    └── pages/
        └── notification_preferences_page.dart
```

The module exposes:
1. **A nav item** — "Notifications" entry under Settings, gated by `Permissions.notificationsView` (already in core).
2. **A slot contribution** — on the existing profile page, render a "Notification preferences" panel teaser that links to the full page.
3. **A capability contract** — `IPushNotificationCarrier` (defined in core), implemented by the Communication module. Core's auth flow calls `IPushNotificationCarrier.registerDeviceTokenAsync(userId, token)`; null fallback silently returns.

---

## Capability contract design

**File:** `boilerplateMobile/lib/core/modularity/push_notification_carrier.dart`

```dart
/// A capability provided by the Communication module: register the device for
/// push notifications and dispatch local notifications. Null Object fallback
/// (NullPushNotificationCarrier in this file) silently no-ops when Communication
/// is not installed.
///
/// Mirrors the BE pattern: core depends on the contract, module provides the
/// impl, no-op fallback so removing the module never crashes core.
abstract class IPushNotificationCarrier {
  Future<void> registerDeviceToken(String userId, String token);
  Future<void> unregisterDeviceToken(String userId);
  Future<bool> isPermissionGranted();
  Future<bool> requestPermission();
}

/// Default registered in core DI; overridden by CommunicationModule.
class NullPushNotificationCarrier implements IPushNotificationCarrier {
  const NullPushNotificationCarrier();
  @override
  Future<void> registerDeviceToken(String userId, String token) async {}
  @override
  Future<void> unregisterDeviceToken(String userId) async {}
  @override
  Future<bool> isPermissionGranted() async => false;
  @override
  Future<bool> requestPermission() async => false;
}
```

**Core registers it (DI bootstrap):**

```dart
// In lib/core/di/injection.dart
// Default — overridden if Communication module is registered
sl.registerLazySingleton<IPushNotificationCarrier>(() => const NullPushNotificationCarrier());
```

**Module overrides:**

```dart
// In lib/modules/communication/communication_module.dart
@override
void registerDependencies(GetIt sl) {
  // Override the null fallback
  if (sl.isRegistered<IPushNotificationCarrier>()) {
    sl.unregister<IPushNotificationCarrier>();
  }
  sl.registerLazySingleton<IPushNotificationCarrier>(
    () => FirebasePushNotificationCarrier(/* …deps */),
  );
  // …rest of the module's DI
}
```

**Core consumes it without knowing if the module is present:**

```dart
// Anywhere in core (e.g. AuthCubit after successful login)
final carrier = sl<IPushNotificationCarrier>();
final granted = await carrier.requestPermission();
if (granted) {
  final token = await getFirebaseToken();
  await carrier.registerDeviceToken(user.id, token);
}
```

If Communication isn't installed: the call is a silent no-op, no permission prompt, no crash. If it is: full flow runs.

---

## File structure

**Create:**

- `boilerplateMobile/lib/core/modularity/push_notification_carrier.dart` — contract + null impl.
- `boilerplateMobile/lib/modules/communication/communication_module.dart`
- `boilerplateMobile/lib/modules/communication/data/...` (datasource, DTO, repo impl)
- `boilerplateMobile/lib/modules/communication/domain/...` (entity, repo iface, use cases)
- `boilerplateMobile/lib/modules/communication/presentation/...` (cubit, page)
- `boilerplateMobile/test/modules/communication/...` — unit tests for cubit and use cases.
- `boilerplateMobile/test/core/modularity/null_push_notification_carrier_test.dart` — verify the null fallback truly no-ops, doesn't throw, returns expected defaults.
- `boilerplateMobile/test/modules/communication/module_removal_test.dart` — boot the registry without the Communication module, assert no exceptions, assert the slot returns `SizedBox.shrink()`, assert `sl<IPushNotificationCarrier>()` returns `NullPushNotificationCarrier`.
- `docs/architecture/mobile-module-development.md` — author guide using both Billing and Communication as reference.

**Modify:**

- `modules.catalog.json` — `communication.mobileModule = "CommunicationModule"`, `communication.mobileFolder = "communication"`, push `"mobile"` into `communication.supportedPlatforms`.
- `boilerplateMobile/lib/core/di/injection.dart` — register `NullPushNotificationCarrier` as the default.
- `boilerplateMobile/lib/app/modules.config.dart` — if Theme 5 has shipped, the codegen handles this. Otherwise hand-add `CommunicationModule()` to `activeModules()`.
- `boilerplateMobile/test/core/modularity/module_registry_test.dart` — add a case that exercises the dependency-order logic with both modules.
- `.github/workflows/modularity.yml` — extend the `mobile-killer` matrix with a `workflow-chain` set so the new mobile chain (workflow→communication→commentsActivity, when communication ships mobile) is exercised. Today's matrix is `[None, All]`; after Theme 6, expand to `[None, All, workflow-chain]` if mobile WorkflowModule exists, otherwise add a new `["communication-only"]` set.

---

## Tasks

### Phase A — Core capability contract

1. Create `IPushNotificationCarrier` and `NullPushNotificationCarrier` in `lib/core/modularity/push_notification_carrier.dart`.
2. Register the null impl in `lib/core/di/injection.dart`.
3. Add a unit test that exercises every method on the null impl.
4. Wire core code (e.g. AuthCubit post-login) to call `carrier.requestPermission()` and `carrier.registerDeviceToken(...)`. Even though it no-ops by default, this is the consumption path that proves the abstraction.
5. Run `flutter analyze` + `flutter test` — green.
6. Commit.

### Phase B — Communication mobile module scaffold

1. Create the directory structure under `lib/modules/communication/`.
2. Implement domain layer (entity + use cases + repository interface).
3. Implement data layer (DTO + datasource + repository impl). Wire to existing BE endpoint `GET /api/v1/notifications/preferences`, `PATCH /api/v1/notifications/preferences/{id}` (verify endpoints exist; add to BE if not — but BE work would be a separate prerequisite PR).
4. Implement presentation layer (cubit + state + page).
5. Implement `CommunicationModule extends AppModule`:
   - `name: "communication"`.
   - `dependencies: []` (catalog will list `commentsActivity` + `communication` for Workflow's BE side, but mobile-side workflow isn't shipping yet, so communication is leaf-level on mobile).
   - `getNavItems()`: one entry under Settings group.
   - `getSlotContributions()`: profile-page-info-card slot → small "Notification preferences" tile.
   - `getDeclaredPermissions()`: returns the relevant `Permissions.notifications*` strings.
   - `registerDependencies(sl)`: re-binds `IPushNotificationCarrier` to a real impl (Firebase or noop-with-logging — pick one based on what's lighter for the boilerplate template).
6. Run `dart run build_runner build --delete-conflicting-outputs` to generate freezed files.
7. Run `flutter analyze` + `flutter test`.
8. Commit (large commit, but logically one unit).

### Phase C — Catalog + bootstrap wiring

1. Update `modules.catalog.json`:
   ```json
   "communication": {
     "mobileModule": "CommunicationModule",
     "mobileFolder": "communication",
     "supportedPlatforms": ["backend", "web", "mobile"],
     // …existing fields
   }
   ```
2. Update `boilerplateMobile/lib/app/modules.config.dart` to include `CommunicationModule()` in `activeModules()`. (If Theme 5 has merged, regenerate.)
3. Run the existing `CatalogConsistencyTests` in BE — `supportedPlatforms_matches_declared_path_fields` will pass because we added all three fields together.
4. Run mobile killer test locally:
   ```bash
   pwsh scripts/rename.ps1 -Name "tier65Smoke" -OutputDir /tmp -Modules All -IncludeMobile:$true
   cd /tmp/tier65Smoke/tier65Smoke-Mobile && flutter pub get && flutter analyze
   ```
5. Commit.

### Phase D — Removal & failure-mode tests

1. `null_push_notification_carrier_test.dart` — assert the null impl never throws and returns sane defaults.
2. `module_removal_test.dart`:
   - Boot a `ModuleRegistry` instance with only `BillingModule()` (no Communication).
   - Assert no exceptions during init.
   - Resolve `IPushNotificationCarrier` — must be `NullPushNotificationCarrier`.
   - Resolve a slot for `profile-page-info-card` — Billing should be present, Communication absent. Render the slot, assert no crash.
3. Add a third test: boot with Communication enabled, confirm `IPushNotificationCarrier` resolves to the Communication impl.
4. `flutter test` green.
5. Commit.

### Phase E — Author guide

`docs/architecture/mobile-module-development.md`:

- "How to add a mobile module" walkthrough using Billing and Communication as before/after examples.
- Capability contract pattern (when to use, how to wire null fallback).
- Slot contribution pattern (when a module owns a UI fragment vs. a full page).
- DI registration order (modules register after core, can replace bindings).
- Testing patterns: unit, removal, dependency-order.
- Cross-references to backend module dev guide (`docs/architecture/module-development.md`).

Length target: 400–600 lines. Should be the canonical reference for anyone adding a third+ mobile module.

### Phase F — CI matrix expansion

If by the time Theme 6 ships, no mobile workflow-chain exists, leave the mobile-killer matrix as `[None, All]`. The All case now exercises Billing + Communication, which is sufficient.

If a future theme adds mobile Workflow, expand the matrix to `[None, All, "workflow,commentsActivity,communication"]` to exercise the dependency chain on mobile.

---

## Verification

- [ ] `flutter analyze` + `flutter test` clean.
- [ ] `module_removal_test` passes — Communication can be omitted with no crashes.
- [ ] `module_registry_test` (existing) still green.
- [ ] Killer-test matrix mobile-killer (modules=All) job succeeds with both Billing and Communication scaffolded.
- [ ] Catalog tests catch a planted regression (e.g., dropping `"mobile"` from `communication.supportedPlatforms` while keeping `mobileModule` set).
- [ ] BE auth flow that calls `IPushNotificationCarrier` works end-to-end with Communication enabled (manual smoke).
- [ ] Removing Communication via `rename.ps1 -Modules billing` produces an app that builds and boots cleanly.

---

## Out of scope

- **Backend push notification infrastructure** — assumed to exist or be a separate prerequisite PR. Theme 6 is about the mobile contract surface; whether the BE actually delivers pushes is orthogonal.
- **Real Firebase integration** — the boilerplate template should ship with a configurable carrier (a logging stub for templates, a Firebase impl as an opt-in). Default to logging stub to avoid forcing every template user to configure FCM.
- **iOS/Android platform-specific notification permissions UI** — the `requestPermission()` method abstracts this; concrete impls handle the platform code.
- **Adding a third mobile module** — once Communication validates the patterns, future modules use the author guide.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Adding Communication mobile reveals a gap in the existing AppModule contract that requires breaking changes | Document the gap in the author guide; if breaking, fix as a separate small PR before Theme 6 lands. The Theme 2 killer matrix gates regressions either way. |
| BE notification preference endpoints don't exist or have different shape | Verify before Phase B. If gap exists, file a prerequisite PR adding the BE endpoints. Don't merge Theme 6 with mocked data. |
| Push notification permission request UI conflicts with system flow | Use `requestPermission()` only in response to an explicit user action (e.g. tap "Enable notifications" in preferences). Never auto-prompt at first launch. |
| Module-removal test boots a full Flutter app and is slow | Use `flutter_test`'s widget testing harness, not integration_test. Keeps execution fast. |

---

## After this ships

Tier 2.5 is complete. The boilerplate has:
- A locked-down catalog schema (Theme 1)
- A trustworthy architecture-test surface (Theme 3)
- CI-gated killer tests (Theme 2)
- Single-source-of-truth permissions (Theme 4)
- Generated module bootstrap on every platform (Theme 5)
- A second mobile module proving the patterns scale (Theme 6)

That's the foundation Tier 3 needs to layer package distribution on without inheriting weaknesses. The package work becomes "extend the generators' input format with package coordinates and the emitters with package-references" — additive, not invasive.
