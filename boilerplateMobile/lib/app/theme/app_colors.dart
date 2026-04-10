import 'package:flutter/material.dart';

/// Brand palette mirroring the FE's active theme preset.
///
/// The current preset is "copper" (from
/// `boilerplateFE/src/config/theme.config.ts`). When the FE preset
/// changes, update these values to match. No runtime sharing exists
/// between web and mobile — this is a manual sync point documented
/// in the mobile README.
abstract final class AppColors {
  // --- Primary (copper) ---
  static const Color primary = Color(0xFFB87333);
  static const Color primaryLight = Color(0xFFD4956A);
  static const Color primaryDark = Color(0xFF8B5A2B);

  // --- Neutral ---
  static const Color background = Color(0xFFFAFAFA);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color surfaceDark = Color(0xFF1C1C1E);
  static const Color backgroundDark = Color(0xFF121212);

  // --- Text ---
  static const Color textPrimary = Color(0xFF1A1A1A);
  static const Color textSecondary = Color(0xFF6B7280);
  static const Color textPrimaryDark = Color(0xFFF5F5F5);
  static const Color textSecondaryDark = Color(0xFF9CA3AF);

  // --- Semantic ---
  static const Color success = Color(0xFF22C55E);
  static const Color warning = Color(0xFFF59E0B);
  static const Color error = Color(0xFFEF4444);
  static const Color info = Color(0xFF3B82F6);
}
