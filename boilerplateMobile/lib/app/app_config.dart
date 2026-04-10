import 'package:boilerplate_mobile/app/flavors/flavor.dart';
import 'package:flutter/widgets.dart';

/// Compile-time configuration selected by the entry point.
///
/// The fields here are the ONLY place build-time variability lives.
/// Anything that differs between flavors, tenants, or deployment targets
/// should flow through here rather than reading environment variables
/// scattered across the codebase.
class AppConfig {
  const AppConfig({
    required this.flavor,
    required this.apiBaseUrl,
    required this.appName,
    required this.multiTenancyEnabled,
  });

  /// Which flavor this build represents.
  final Flavor flavor;

  /// Base URL of the backend REST API, e.g.
  /// `http://10.0.2.2:5000/api/v1` on Android emulator for local dev.
  final String apiBaseUrl;

  /// Human-readable app name shown in the UI header / splash / dialogs.
  final String appName;

  /// When `false`, tenant-scoped UI and headers are suppressed app-wide.
  /// Ships forward-compat: the backend does not yet support a fully
  /// tenancy-less mode, but the mobile flag is wired so switching later
  /// is a one-line change per flavor.
  final bool multiTenancyEnabled;

  /// Retrieve the config from a [BuildContext] via the [AppConfigScope]
  /// inherited widget set up by `bootstrap`.
  static AppConfig of(BuildContext context) {
    final scope =
        context.dependOnInheritedWidgetOfExactType<AppConfigScope>();
    assert(scope != null, 'AppConfigScope missing — did bootstrap run?');
    return scope!.config;
  }
}

/// Inherited widget exposing [AppConfig] to the widget tree.
class AppConfigScope extends InheritedWidget {
  const AppConfigScope({
    required this.config,
    required super.child,
    super.key,
  });

  final AppConfig config;

  @override
  bool updateShouldNotify(AppConfigScope oldWidget) =>
      oldWidget.config != config;
}
