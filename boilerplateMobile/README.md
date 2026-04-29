# Boilerplate Mobile (Flutter)

Flutter mobile client for the Boilerplate CQRS project. iOS + Android, with a modular architecture mirroring the backend and frontend.

## Quick Start

```bash
# Prerequisites: Flutter SDK >= 3.24, Android Studio / Xcode

# Install dependencies
flutter pub get

# Generate code (freezed DTOs, injectable DI, l10n)
dart run build_runner build --delete-conflicting-outputs

# Run (staging flavor, Android emulator)
flutter run --flavor staging -t lib/main_staging.dart

# Run (prod flavor)
flutter run --flavor prod -t lib/main_prod.dart

# Build release APK
flutter build apk --flavor staging -t lib/main_staging.dart --release

# Run tests
flutter test
```

## Architecture

```
Presentation (Cubit/Bloc + Pages)
      |  calls
Domain (Entities + UseCases + Repository interfaces)
      |  implemented by
Data (DTOs + Remote DataSources + Repository impls)
      |  talks to
Core (Dio client, Hive, SecureStorage, DI, Theme)
```

### Why this layering?

- **Domain is pure Dart** — no Flutter, no Dio, no Hive. You can test use cases with zero framework setup. If Flutter's HTTP library changed tomorrow, domain code wouldn't need a single edit.
- **Data layer** knows about HTTP and serialization (freezed DTOs, Dio calls), but the domain doesn't care how data arrives — it just defines the contract (abstract repository).
- **Presentation** calls use cases, not repositories directly. This forces a "one action per class" discipline that keeps cubits thin — they orchestrate UI state, not business logic.

### Why Cubit-first?

Most screens have simple state: loading → loaded / error. Cubit handles this without the boilerplate of events. Full Bloc (events + states) is reserved for screens where event-driven patterns genuinely help (e.g., debounced search, complex multi-step flows). Start with Cubit; upgrade to Bloc only when Cubit feels limiting.

### Why `Result<T>` instead of exceptions?

```dart
switch (await loginUseCase(params)) {
  case Success(value: final session): emit(LoginState.success(session));
  case Err(failure: NetworkFailure()): emit(LoginState.error('No internet'));
  case Err(failure: final f): emit(LoginState.error(f.message));
}
```

Dart 3's sealed classes + pattern matching make every failure path visible at compile time. No forgotten catch blocks, no "which exceptions can this throw?" guessing. The `Failure` hierarchy is exhaustive — add a new subtype and every `switch` breaks until you handle it.

## Module System

Optional features (billing, future e-commerce, school, HR modules) are self-contained and strippable:

```
lib/
  core/features/    # Always ships (auth, profile, notifications)
  modules/          # Optional, removable by rename.ps1
    billing/        # Implements AppModule — has its own domain/data/presentation
```

### How it works

1. Each module implements `AppModule` — registering its DI, nav items, slot contributions, and permissions.
2. `modules.config.dart` lists active modules between `// MODULE IMPORTS` / `// MODULE INSTANCES` markers.
3. `rename.ps1 -Modules "None"` strips the imports and instances — the app compiles and runs with core only.
4. Modules provide their own `pageBuilder` in nav items, so the shell never imports module code directly.
5. Cross-module communication uses **capability contracts** with Null Object fallbacks (same pattern as the BE).

### Adding a new module

1. Create `lib/modules/{name}/` with `domain/`, `data/`, `presentation/` subdirs
2. Create `{name}_module.dart` implementing `AppModule`
3. Add import + instance in `modules.config.dart` between the markers
4. Add `mobileModule` / `mobileFolder` to `modules.catalog.json`

## Key Files

| File | Purpose |
|------|---------|
| `lib/main_staging.dart` / `main_prod.dart` | Flavor entry points with `AppConfig` |
| `lib/app/app_config.dart` | Compile-time config (`apiBaseUrl`, `multiTenancyEnabled`) |
| `lib/app/modules.config.dart` | Active modules list (edited by rename.ps1) |
| `lib/core/error/result.dart` | Sealed `Result<T>` / `Failure` types |
| `lib/core/network/dio_client.dart` | Dio factory with 5 interceptors |
| `lib/core/network/interceptors/refresh_interceptor.dart` | Single-flight token refresh |
| `lib/core/modularity/app_module.dart` | Module interface |
| `lib/core/modularity/module_registry.dart` | Module discovery + topological sort |
| `lib/core/permissions/permissions.dart` | Permission constants mirroring BE |

## Multi-Tenancy Toggle

`AppConfig.multiTenancyEnabled` controls whether tenant-scoped UI and headers appear. Set per flavor in the entry points; `rename.ps1 -MobileMultiTenancy:$false` disables it for single-entity solutions.

When disabled:
- No `X-Tenant-Id` header sent on requests
- Tenant info row hidden on profile page
- `tenant_id` JWT claim ignored

## Syncing with BE/FE

| What | Where | Sync method |
|------|-------|-------------|
| Permission strings | `lib/core/permissions/permissions.dart` | Manual — mirror `Starter.Shared/Constants/Permissions.cs` |
| API endpoints | Feature datasources (`auth_remote_datasource.dart`, etc.) | Manual — match controller routes |
| API response envelope | `lib/core/network/api_response.dart` | Matches `ApiResponse<T>` / `PaginatedResponse<T>` from BE |
| Theme colors | `lib/app/theme/app_colors.dart` | Manual — mirror FE active preset |
| Module list | `lib/app/modules.config.dart` + `modules.catalog.json` | rename.ps1 syncs BE + FE + Mobile together |

## Dev URLs

| Platform | Staging API Base URL |
|----------|---------------------|
| Android emulator | `http://10.0.2.2:5000/api/v1` |
| iOS simulator | `http://localhost:5000/api/v1` |
| Physical device | `http://{LAN_IP}:5000/api/v1` |
