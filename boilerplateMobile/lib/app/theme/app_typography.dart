import 'package:flutter/material.dart';

/// Typography configuration using IBM Plex Sans (matching the FE).
///
/// The FE uses IBM Plex Sans for LTR and IBM Plex Sans Arabic for RTL.
/// Mobile uses the system font stack by default and will switch to
/// bundled IBM Plex fonts once assets are added. For now, `fontFamily`
/// is left unset so the platform default applies cleanly.
abstract final class AppTypography {
  static const String fontFamily = 'IBM Plex Sans';
  static const String fontFamilyArabic = 'IBM Plex Sans Arabic';

  static TextTheme textTheme(Brightness brightness) {
    final color = brightness == Brightness.light
        ? const Color(0xFF1A1A1A)
        : const Color(0xFFF5F5F5);

    return TextTheme(
      displayLarge: TextStyle(
        fontSize: 32,
        fontWeight: FontWeight.w700,
        color: color,
        height: 1.2,
      ),
      headlineMedium: TextStyle(
        fontSize: 24,
        fontWeight: FontWeight.w600,
        color: color,
        height: 1.3,
      ),
      titleLarge: TextStyle(
        fontSize: 20,
        fontWeight: FontWeight.w600,
        color: color,
        height: 1.3,
      ),
      titleMedium: TextStyle(
        fontSize: 16,
        fontWeight: FontWeight.w600,
        color: color,
        height: 1.4,
      ),
      bodyLarge: TextStyle(
        fontSize: 16,
        fontWeight: FontWeight.w400,
        color: color,
        height: 1.5,
      ),
      bodyMedium: TextStyle(
        fontSize: 14,
        fontWeight: FontWeight.w400,
        color: color,
        height: 1.5,
      ),
      bodySmall: TextStyle(
        fontSize: 12,
        fontWeight: FontWeight.w400,
        color: color.withValues(alpha: 0.7),
        height: 1.4,
      ),
      labelLarge: TextStyle(
        fontSize: 14,
        fontWeight: FontWeight.w600,
        color: color,
        letterSpacing: 0.5,
      ),
    );
  }
}
