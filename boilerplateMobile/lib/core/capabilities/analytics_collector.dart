/// Capability contract for analytics tracking.
///
/// Modules that need analytics call this via DI. If no analytics
/// module is installed, the [NullAnalyticsCollector] silently no-ops.
///
/// This is a template demonstrating the Null Object capability pattern
/// used by the BE (`IBillingProvider`/`NullBillingProvider`). Real
/// capability contracts follow the same shape.
// ignore: one_member_abstracts
abstract class AnalyticsCollector {
  Future<void> track(String event, {Map<String, Object?> props});
}

/// Fallback registered by core DI. Does nothing.
class NullAnalyticsCollector implements AnalyticsCollector {
  @override
  Future<void> track(
    String event, {
    Map<String, Object?> props = const {},
  }) async {}
}
