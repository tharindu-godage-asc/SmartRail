/// What `GET /api/v1/operator/journey` returns.
class OperatorStop {
  const OperatorStop({
    required this.serviceStopId,
    required this.sequence,
    required this.stationCode,
    required this.stationName,
    required this.isFinalStop,
    this.scheduledArrival,
    this.scheduledDeparture,
  });

  factory OperatorStop.fromJson(Map<String, dynamic> json) => OperatorStop(
    serviceStopId: json['serviceStopId'] as String,
    sequence: json['sequence'] as int,
    stationCode: json['stationCode'] as String,
    stationName: json['stationName'] as String,
    isFinalStop: json['isFinalStop'] as bool,
    scheduledArrival: _instant(json['scheduledArrival']),
    scheduledDeparture: _instant(json['scheduledDeparture']),
  );

  final String serviceStopId;
  final int sequence;
  final String stationCode;
  final String stationName;
  final bool isFinalStop;
  final DateTime? scheduledArrival;
  final DateTime? scheduledDeparture;

  Map<String, dynamic> toJson() => {
    'serviceStopId': serviceStopId,
    'sequence': sequence,
    'stationCode': stationCode,
    'stationName': stationName,
    'isFinalStop': isFinalStop,
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
    this.actualDeparture,
  });

  factory StopProgress.fromJson(Map<String, dynamic> json) => StopProgress(
    serviceStopId: json['serviceStopId'] as String,
    sequence: json['sequence'] as int,
    confirmation: json['confirmation'] as String,
    actualDeparture: _instant(json['actualDeparture']),
  );

  final String serviceStopId;
  final int sequence;

  /// Pending, Reported or Inferred (a later stop was reported, so this one was passed).
  final String confirmation;
  final DateTime? actualDeparture;

  bool get hasDeparted =>
      confirmation == 'Reported' || confirmation == 'Inferred';
}

/// What `GET /api/v1/trains/{trainNumber}/status` returns, reduced to what the operator app uses.
class TrainStatus {
  const TrainStatus({
    required this.journeyId,
    required this.version,
    required this.serverTime,
    required this.delayMinutes,
    required this.stops,
  });

  factory TrainStatus.fromJson(Map<String, dynamic> json) => TrainStatus(
    journeyId: json['journeyId'] as String,
    version: json['version'] as int,
    serverTime: DateTime.parse(json['serverTime'] as String).toUtc(),
    delayMinutes: json['delayMinutes'] as int,
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
}

DateTime? _instant(Object? value) =>
    value == null ? null : DateTime.parse(value as String).toUtc();
