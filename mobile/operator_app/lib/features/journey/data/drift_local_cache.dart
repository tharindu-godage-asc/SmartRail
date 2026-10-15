import 'dart:convert';

import '../../reports/data/app_database.dart';
import '../domain/local_cache.dart';
import '../domain/models.dart';

class DriftLocalCache implements LocalCache {
  DriftLocalCache(this._db);

  final AppDatabase _db;

  static const _journeyKey = 'journey';
  static const _fetchedKey = 'journey_fetched_at';
  static const _offsetKey = 'clock_offset_ms';

  @override
  Future<CachedJourney?> readJourney() async {
    final json = await _read(_journeyKey);
    final fetched = await _read(_fetchedKey);
    if (json == null || fetched == null) {
      return null;
    }
    try {
      return CachedJourney(
        OperatorJourney.fromJson(jsonDecode(json) as Map<String, dynamic>),
        DateTime.parse(fetched).toUtc(),
      );
    } on Object {
      // A cache from an older version of the app is not worth a crash: drop it and ask the server.
      await clearJourney();
      return null;
    }
  }

  @override
  Future<void> writeJourney(OperatorJourney journey, DateTime fetchedAt) async {
    await _write(_journeyKey, jsonEncode(journey.toJson()));
    await _write(_fetchedKey, fetchedAt.toUtc().toIso8601String());
  }

  @override
  Future<void> clearJourney() async {
    await (_db.delete(
      _db.keyValues,
    )..where((r) => r.key.isIn([_journeyKey, _fetchedKey]))).go();
  }

  @override
  Future<Duration?> readClockOffset() async {
    final value = int.tryParse(await _read(_offsetKey) ?? '');
    return value == null ? null : Duration(milliseconds: value);
  }

  @override
  Future<void> writeClockOffset(Duration offset) =>
      _write(_offsetKey, '${offset.inMilliseconds}');

  Future<String?> _read(String key) async => (await (_db.select(
    _db.keyValues,
  )..where((r) => r.key.equals(key))).getSingleOrNull())?.value;

  Future<void> _write(String key, String value) => _db
      .into(_db.keyValues)
      .insertOnConflictUpdate(
        KeyValuesCompanion.insert(key: key, value: value),
      );
}
