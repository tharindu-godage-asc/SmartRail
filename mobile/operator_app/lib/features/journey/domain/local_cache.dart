import 'models.dart';

class CachedJourney {
  const CachedJourney(this.journey, this.fetchedAt);

  final OperatorJourney journey;

  /// When the phone last got this from the server, for "Offline, last updated 08:35" (AC-E03-01 5).
  final DateTime fetchedAt;
}

/// What the Reports tab needs to head a group of reports: which train, between which stations, on which operating day. Kept for
/// the journeys the operator has worked on, so older reports still have a heading when the journey is gone from the phone.
class JourneySummary {
  const JourneySummary({
    required this.journeyId,
    required this.trainNumber,
    required this.operatingDate,
    required this.from,
    required this.to,
  });

  factory JourneySummary.of(OperatorJourney journey) => JourneySummary(
    journeyId: journey.journeyId,
    trainNumber: journey.trainNumber,
    operatingDate: journey.operatingDate,
    from: journey.firstStop.stationName,
    to: journey.finalStop.stationName,
  );

  factory JourneySummary.fromJson(Map<String, dynamic> json) => JourneySummary(
    journeyId: json['journeyId'] as String,
    trainNumber: json['trainNumber'] as String,
    operatingDate: json['operatingDate'] as String,
    from: json['from'] as String,
    to: json['to'] as String,
  );

  final String journeyId;
  final String trainNumber;

  /// `yyyy-MM-dd`.
  final String operatingDate;
  final String from;
  final String to;

  Map<String, dynamic> toJson() => {
    'journeyId': journeyId,
    'trainNumber': trainNumber,
    'operatingDate': operatingDate,
    'from': from,
    'to': to,
  };
}

/// What the phone remembers between runs so the journey still shows with no network.
abstract interface class LocalCache {
  Future<CachedJourney?> readJourney();
  Future<void> writeJourney(OperatorJourney journey, DateTime fetchedAt);
  Future<void> clearJourney();

  /// The summaries of the journeys worked on, by journey id (the latest 30 are kept).
  Future<Map<String, JourneySummary>> readJourneySummaries();
  Future<void> writeJourneySummary(OperatorJourney journey);

  /// The first page of the previous journeys as the server sent it (JSON), kept for offline use.
  Future<String?> readHistoryJson();
  Future<void> writeHistoryJson(String json);

  Future<Duration?> readClockOffset();
  Future<void> writeClockOffset(Duration offset);
}
