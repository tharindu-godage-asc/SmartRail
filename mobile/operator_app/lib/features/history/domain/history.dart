/// A journey the operator worked on earlier (spec 12 section 4.1). The server sends only his own journeys.
class PastJourney {
  const PastJourney({
    required this.journeyId,
    required this.trainNumber,
    required this.fromName,
    required this.toName,
    required this.operatingDate,
    required this.status,
    required this.myReportCount,
    this.arrivalDelayMinutes,
  });

  factory PastJourney.fromJson(Map<String, dynamic> json) => PastJourney(
    journeyId: json['journeyId'] as String,
    trainNumber: json['trainNumber'] as String,
    fromName: json['fromName'] as String,
    toName: json['toName'] as String,
    operatingDate: DateTime.parse(json['operatingDate'] as String),
    status: json['status'] as String,
    myReportCount: json['myReportCount'] as int,
    arrivalDelayMinutes: json['arrivalDelayMinutes'] as int?,
  );

  final String journeyId;
  final String trainNumber;
  final String fromName;
  final String toName;

  /// The operating day (a date, not an instant).
  final DateTime operatingDate;
  final String status;
  final int myReportCount;

  /// Minutes late at the final stop; null when the journey did not reach it.
  final int? arrivalDelayMinutes;

  bool get completed => status == 'Completed';
}

class HistoryPage {
  const HistoryPage({
    required this.serverTime,
    required this.journeys,
    required this.next,
    required this.raw,
  });

  factory HistoryPage.fromJson(Map<String, dynamic> json) => HistoryPage(
    serverTime: DateTime.parse(json['serverTime'] as String).toUtc(),
    journeys: [
      for (final j in json['journeys'] as List<dynamic>)
        PastJourney.fromJson(j as Map<String, dynamic>),
    ],
    next: json['next'] == null
        ? null
        : DateTime.parse(json['next'] as String).toUtc(),
    raw: json,
  );

  /// The server's own time of this answer, shown as "Last updated" (not the phone clock).
  final DateTime serverTime;
  final List<PastJourney> journeys;

  /// Pass to the next request as `before` to get earlier journeys; null on the last page.
  final DateTime? next;

  /// Kept so the first page can be saved for offline use.
  final Map<String, dynamic> raw;
}

class RecordStop {
  const RecordStop({
    required this.stationName,
    required this.confirmation,
    this.scheduledArrival,
    this.scheduledDeparture,
    this.actualArrival,
    this.actualDeparture,
  });

  factory RecordStop.fromJson(Map<String, dynamic> json) => RecordStop(
    stationName: json['stationName'] as String,
    confirmation: json['confirmation'] as String,
    scheduledArrival: _instant(json['scheduledArrival']),
    scheduledDeparture: _instant(json['scheduledDeparture']),
    actualArrival: _instant(json['actualArrival']),
    actualDeparture: _instant(json['actualDeparture']),
  );

  final String stationName;

  /// `Reported`, `Inferred` or `Pending`.
  final String confirmation;
  final DateTime? scheduledArrival;
  final DateTime? scheduledDeparture;
  final DateTime? actualArrival;
  final DateTime? actualDeparture;
}

/// One report this operator made, as the server recorded it.
class RecordReport {
  const RecordReport({
    required this.type,
    required this.occurredAt,
    this.stationName,
    this.delayReason,
    this.totalDelayMinutes,
  });

  factory RecordReport.fromJson(Map<String, dynamic> json) => RecordReport(
    type: json['type'] as String,
    stationName: json['stationName'] as String?,
    occurredAt: DateTime.parse(json['occurredAt'] as String).toUtc(),
    delayReason: json['delayReason'] as String?,
    totalDelayMinutes: json['totalDelayMinutes'] as int?,
  );

  /// `Departed`, `Arrived` or `DelayReported`.
  final String type;
  final String? stationName;
  final DateTime occurredAt;
  final String? delayReason;

  /// Null on a delay report means the length was unknown.
  final int? totalDelayMinutes;
}

class JourneyRecord {
  const JourneyRecord({
    required this.trainNumber,
    required this.operatingDate,
    required this.status,
    required this.stops,
    required this.myReports,
  });

  factory JourneyRecord.fromJson(Map<String, dynamic> json) => JourneyRecord(
    trainNumber: json['trainNumber'] as String,
    operatingDate: DateTime.parse(json['operatingDate'] as String),
    status: json['status'] as String,
    stops: [
      for (final s in json['stops'] as List<dynamic>)
        RecordStop.fromJson(s as Map<String, dynamic>),
    ],
    myReports: [
      for (final r in json['myReports'] as List<dynamic>)
        RecordReport.fromJson(r as Map<String, dynamic>),
    ],
  );

  final String trainNumber;
  final DateTime operatingDate;
  final String status;
  final List<RecordStop> stops;
  final List<RecordReport> myReports;
}

DateTime? _instant(Object? value) =>
    value == null ? null : DateTime.parse(value as String).toUtc();

/// The two history reads (spec 12). Failures are the exceptions of the journey API.
abstract interface class HistoryApi {
  /// Past journeys, newest first. [before] pages to earlier ones.
  Future<HistoryPage> fetchHistory({DateTime? before});

  /// The record of one journey; null when it is not his or does not exist (404).
  Future<JourneyRecord?> fetchRecord(String journeyId);
}
