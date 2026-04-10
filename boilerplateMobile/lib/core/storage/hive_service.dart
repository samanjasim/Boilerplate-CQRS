import 'dart:convert';

import 'package:hive_flutter/hive_flutter.dart';

/// Hive-based cache for non-secret data (user profile, feature flags,
/// cached notification list, reference data).
///
/// All values are stored as JSON strings via `json_serializable`. This
/// avoids the need for `hive_generator` / `@HiveType` annotations and
/// keeps the cache format human-inspectable.
class HiveService {
  static const _boxName = 'cache';

  late Box<String> _box;

  /// Must be called once during bootstrap, before any reads/writes.
  Future<void> init() async {
    await Hive.initFlutter();
    _box = await Hive.openBox<String>(_boxName);
  }

  /// Store a JSON-serialisable object under [key].
  Future<void> put(String key, Map<String, dynamic> value) =>
      _box.put(key, jsonEncode(value));

  /// Retrieve and deserialise an object, or `null` if absent.
  Map<String, dynamic>? get(String key) {
    final raw = _box.get(key);
    if (raw == null) return null;
    return jsonDecode(raw) as Map<String, dynamic>;
  }

  /// Store a list of JSON-serialisable objects.
  Future<void> putList(String key, List<Map<String, dynamic>> value) =>
      _box.put(key, jsonEncode(value));

  /// Retrieve a list, or empty list if absent.
  List<Map<String, dynamic>> getList(String key) {
    final raw = _box.get(key);
    if (raw == null) return [];
    final decoded = jsonDecode(raw) as List<dynamic>;
    return decoded.cast<Map<String, dynamic>>();
  }

  /// Remove a single key.
  Future<void> delete(String key) => _box.delete(key);

  /// Clear all cached data (e.g. on logout).
  Future<void> clear() => _box.clear();
}
