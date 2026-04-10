import 'package:boilerplate_mobile/core/modularity/module_registry.dart';
import 'package:flutter/widgets.dart';

/// Widget that renders all module contributions for a named slot.
///
/// Place this in any core page where modules should be able to inject UI:
/// ```dart
/// SlotBuilder(id: 'profile-info', args: userId)
/// ```
///
/// If no module contributes to this slot, nothing is rendered.
class SlotBuilder extends StatelessWidget {
  const SlotBuilder({
    required this.id,
    this.args,
    this.separator,
    super.key,
  });

  /// The slot identifier. Must match what modules register in
  /// `getSlotContributions()`.
  final String id;

  /// Optional data passed to each slot contribution's builder.
  final Object? args;

  /// Optional widget rendered between contributions.
  final Widget? separator;

  @override
  Widget build(BuildContext context) {
    final widgets = ModuleRegistry.instance.buildSlot(id, context, args: args);
    if (widgets.isEmpty) return const SizedBox.shrink();

    if (separator != null) {
      final withSeparators = <Widget>[];
      for (var i = 0; i < widgets.length; i++) {
        withSeparators.add(widgets[i]);
        if (i < widgets.length - 1) {
          withSeparators.add(separator!);
        }
      }
      return Column(
        mainAxisSize: MainAxisSize.min,
        children: withSeparators,
      );
    }

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: widgets,
    );
  }
}
