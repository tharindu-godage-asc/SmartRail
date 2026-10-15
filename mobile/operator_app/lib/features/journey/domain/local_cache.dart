import 'models.dart';

class CachedJourney {
  const CachedJourney(this.journey, this.fetchedAt);

  final OperatorJourney journey;

  /// When the phone last got this from the server, for "Offline, last updated 08:35" (AC-E03-01 5).
  final DateTime fetchedAt;
}

/// What the phone remembers between runs so the journey still shows with no network.
abstract interface class LocalCache {
  Future<CachedJourney?> readJourney();
  Future<void> writeJourney(OperatorJourney journey, DateTime fetchedAt);
  Future<void> clearJourney();

  Future<Duration?> readClockOffset();
  Future<void> writeClockOffset(Duration offset);
}
