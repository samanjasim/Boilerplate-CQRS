import 'package:boilerplate_mobile/l10n/app_localizations.dart';
import 'package:flutter/widgets.dart';

/// Convenience extensions on [BuildContext].
extension ContextExtensions on BuildContext {
  /// Shorthand for `AppLocalizations.of(this)`.
  AppLocalizations get l10n => AppLocalizations.of(this);

  /// Whether the current locale is RTL.
  bool get isRtl => Directionality.of(this) == TextDirection.rtl;
}
