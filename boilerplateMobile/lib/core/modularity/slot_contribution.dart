import 'package:flutter/widgets.dart';

/// A widget contribution that a module injects into a named slot.
///
/// Mirrors the FE slot pattern (e.g. `tenant-detail-tabs`,
/// `users-list-toolbar`). Core pages render `SlotBuilder(id: 'profile-info')`
/// and any active module that registered a contribution for that slot
/// gets rendered there.
class SlotContribution {
  const SlotContribution({
    required this.builder,
    this.order = 100,
  });

  /// Builds the widget to render in the slot.
  /// `context` is the slot's build context.
  /// `args` is optional data the slot host passes (e.g. a tenant ID).
  final Widget Function(BuildContext context, {Object? args}) builder;

  /// Sort order when multiple modules contribute to the same slot.
  final int order;
}
