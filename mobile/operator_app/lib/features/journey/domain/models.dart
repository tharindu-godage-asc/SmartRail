/// What `GET /api/v1/operator/journey` returns.
class OperatorStop {
  const OperatorStop({
    required this.serviceStopId,
    required this.sequence,
    required this.stationCode,
    required this.stationName,
    required this.isFinalStop,
    this.isArrivalStation = false,
    this.scheduledArrival,
    this.scheduledDeparture,
  });

  factory OperatorStop.fromJson(Map<String, dynamic> json) => OperatorStop(
    serviceStopId: json['serviceStopId'] as String,
    sequence: json['sequence'] as int,
    stationCode: json['stationCode'] as String,
    stationName: json['stationName'] as String,
    isFinalStop: json['isFinalStop'] as bool,
    isArrivalStation: json['isArrivalStation'] as bool? ?? false,
    scheduledArrival: _instant(json['scheduledArrival']),
    scheduledDeparture: _instant(json['scheduledDeparture']),
  );

  final String serviceStopId;
  final int sequence;
  final String stationCode;
  final String stationName;
  final bool isFinalStop;

  /// An arrival can be reported here: a designated stop, or the final stop (D-08, spec 06).
  final bool isArrivalStation;
  final DateTime? scheduledArrival;
  final DateTime? scheduledDeparture;

  Map<String, dynamic> toJson() => {
    'serviceStopId': serviceStopId,
    'sequence': sequence,
    'stationCode': stationCode,
    'stationName': stationName,
    'isFinalStop': isFinalStop,
    'isArrivalStation': isArrivalStation,
    'scheduledArrival': scheduledArrival?.toUtc().toIso8601String(),
    'scheduledDeparture': scheduledDeparture?.toUtc().toIso8601String(),
  };
}

class OperatorJourney {
  const OperatorJourney({
    required this.journeyId,
    required this.trainNumber,
    required this.operatingDate,
    required this.status,
    required this.version,
    required this.stops,
  });

  factory OperatorJourney.fromJson(Map<String, dynamic> json) =>
      OperatorJourney(
        journeyId: json['journeyId'] as String,
        trainNumber: json['trainNumber'] as String,
        operatingDate: json['operatingDate'] as String,
        status: json['status'] as String,
        version: json['version'] as int,
        stops:
            (json['stops'] as List<dynamic>)
                .map((s) => OperatorStop.fromJson(s as Map<String, dynamic>))
                .toList()
              ..sort((a, b) => a.sequence.compareTo(b.sequence)),
      );

  final String journeyId;
  final String trainNumber;

  /// `yyyy-MM-dd`, the operating date of the journey (not today's date).
  final String operatingDate;
  final String status;
  final int version;
  final List<OperatorStop> stops;

  OperatorStop get firstStop => stops.first;
  OperatorStop get finalStop => stops.last;

  Map<String, dynamic> toJson() => {
    'journeyId': journeyId,
    'trainNumber': trainNumber,
    'operatingDate': operatingDate,
    'status': status,
    'version': version,
    'stops': stops.map((s) => s.toJson()).toList(),
  };
}

/// One stop in the public status read, which is how the app learns what has already departed (also from other phones).
class StopProgress {
  const StopProgress({
    required this.serviceStopId,
    required this.sequence,
    required this.confirmation,
    this.actualArrival,
    this.actualDeparture,
  });

  factory StopProgress.fromJson(Map<String, dynamic> json) => StopProgress(
    serviceStopId: json['serviceStopId'] as String,
    sequence: json['sequence'] as int,
    confirmation: json['confirmation'] as String,
    actualArrival: _instant(json['actualArrival']),
    actualDeparture: _instant(json['actualDeparture']),
  );

  final String serviceStopId;
  final int sequence;

  /// Pending, Reported or Inferred (a later stop was reported, so this one was passed).
  final String confirmation;
  final DateTime? actualArrival;
  final DateTime? actualDeparture;

  /// The train has left this stop: its departure was reported, or a later stop was reported so it was passed.
  /// A stop with only an arrival is `Reported` but not left, so the confirmation alone is not enough.
  bool get hasDeparted => actualDeparture != null || confirmation == 'Inferred';
}

/// What `GET /api/v1/trains/{trainNumber}/status` returns, reduced to what the operator app uses.
class TrainStatus {
  const TrainStatus({
    required this.journeyId,
    required this.version,
    required this.serverTime,
    required this.delayMinutes,
    required this.stops,
    this.statusKey = '',
    this.delayKnown = true,
  });

  factory TrainStatus.fromJson(Map<String, dynamic> json) => TrainStatus(
    journeyId: json['journeyId'] as String,
    version: json['version'] as int,
    serverTime: DateTime.parse(json['serverTime'] as String).toUtc(),
    delayMinutes: json['delayMinutes'] as int,
    statusKey: json['statusKey'] as String? ?? '',
    delayKnown: json['delayKnown'] as bool? ?? true,
    stops: (json['stops'] as List<dynamic>)
        .map((s) => StopProgress.fromJson(s as Map<String, dynamic>))
        .toList(),
  );

  /// The status read answers for "the train in progress", which may not be the journey assigned here: compare before using it.
  final String journeyId;
  final int version;
  final DateTime serverTime;
  final int delayMinutes;
  final List<StopProgress> stops;

  /// `Completed` once the arrival at the final stop is accepted (AC-E03-03 4); other values are not used by the app.
  final String statusKey;

  /// False after an operator reported "unknown": the number is the last known one, not a promise.
  final bool delayKnown;
}

DateTime? _instant(Object? value) =>
    value == null ? null : DateTime.parse(value as String).toUtc();
