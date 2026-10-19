import '../core/l10n/strings.dart';

DateTime? _time(Object? v) =>
    v == null ? null : DateTime.parse(v as String).toUtc();

Map<String, dynamic> _map(Object? v) => (v as Map).cast<String, dynamic>();

class Station {
  const Station(this.code, this.en, this.si, this.ta);

  factory Station.fromJson(Map<String, dynamic> j) {
    final n = _map(j['name']);
    return Station(
      j['code'] as String,
      n['en'] as String,
      n['si'] as String,
      n['ta'] as String,
    );
  }

  final String code;
  final String en;
  final String si;
  final String ta;

  /// A missing translation falls back to English (EC-78).
  String name(Lang lang) {
    final text = switch (lang) {
      Lang.en => en,
      Lang.si => si,
      Lang.ta => ta,
    };
    return text.isEmpty ? en : text;
  }

  Map<String, dynamic> toJson() => {
    'code': code,
    'name': {'en': en, 'si': si, 'ta': ta},
  };
}

class TimePair {
  const TimePair(this.scheduled, this.estimated);

  static TimePair? parse(Object? v) {
    if (v == null) return null;
    final m = _map(v);
    return TimePair(_time(m['scheduled'])!, _time(m['estimated']));
  }

  final DateTime scheduled;
  final DateTime? estimated;

  /// The time to show: the estimate, else the schedule.
  DateTime get shown => estimated ?? scheduled;
}

/// One train in a search result (`GET /api/v1/trains`).
class TrainResult {
  const TrainResult({
    required this.number,
    required this.destination,
    required this.live,
    required this.arrivalAtFrom,
    required this.departureAtFrom,
    required this.statusKey,
    required this.delayMinutes,
    this.platform,
    this.lastEventAt,
  });

  factory TrainResult.fromJson(Map<String, dynamic> j) {
    final from = _map(j['atFrom']);
    return TrainResult(
      number: j['trainNumber'] as String,
      destination: Station.fromJson(_map(j['destination'])),
      live: j['live'] as bool,
      arrivalAtFrom: TimePair.parse(from['arrival']),
      departureAtFrom: TimePair.parse(from['departure']),
      statusKey: j['statusKey'] as String,
      delayMinutes: (j['delayMinutes'] as num?)?.toInt() ?? 0,
      platform: j['platform'] as String?,
      lastEventAt: _time(j['lastEventAt']),
    );
  }

  final String number;
  final Station destination;
  final bool live;
  final TimePair? arrivalAtFrom;
  final TimePair? departureAtFrom;
  final String statusKey;
  final int delayMinutes;
  final String? platform;
  final DateTime? lastEventAt;

  /// The time at the passenger's station: arrival, or departure at a service's first stop.
  TimePair? get timeAtFrom => arrivalAtFrom ?? departureAtFrom;
}

class TrainsAnswer {
  const TrainsAnswer(this.trains, this.serverTime);

  factory TrainsAnswer.fromJson(Map<String, dynamic> j) => TrainsAnswer(
    (j['trains'] as List).map((t) => TrainResult.fromJson(_map(t))).toList(),
    _time(j['serverTime'])!,
  );

  final List<TrainResult> trains;
  final DateTime serverTime;
}

class StopView {
  const StopView({
    required this.sequence,
    required this.code,
    required this.name,
    this.scheduledArrival,
    this.scheduledDeparture,
    this.estimatedArrival,
    this.estimatedDeparture,
    this.actualArrival,
    this.actualDeparture,
    required this.confirmation,
  });

  factory StopView.fromJson(Map<String, dynamic> j) => StopView(
    sequence: (j['sequence'] as num).toInt(),
    code: j['stationCode'] as String,
    name: j['stationName'] as String,
    scheduledArrival: _time(j['scheduledArrival']),
    scheduledDeparture: _time(j['scheduledDeparture']),
    estimatedArrival: _time(j['estimatedArrival']),
    estimatedDeparture: _time(j['estimatedDeparture']),
    actualArrival: _time(j['actualArrival']),
    actualDeparture: _time(j['actualDeparture']),
    confirmation: j['confirmation'] as String,
  );

  final int sequence;
  final String code;
  final String name;
  final DateTime? scheduledArrival;
  final DateTime? scheduledDeparture;
  final DateTime? estimatedArrival;
  final DateTime? estimatedDeparture;
  final DateTime? actualArrival;
  final DateTime? actualDeparture;

  /// `Pending`, `Reported` or `Inferred` (a stop passed with no report, shown as passed: P-07).
  final String confirmation;
}

class LastSeen {
  const LastSeen(this.code, this.name, this.state, this.at);

  final String code;
  final String name;

  /// `Departed` or `Arrived`.
  final String state;
  final DateTime at;
}

/// The public status of one journey (`GET /api/v1/trains/{n}/status`).
class TrainStatus {
  const TrainStatus({
    required this.trainNumber,
    required this.status,
    required this.delayMinutes,
    required this.delayKnown,
    required this.stops,
    required this.serverTime,
    this.lastSeen,
    this.lastEventAt,
    required this.operatingDate,
    this.reasonCode,
    this.reasonVerifiedAt,
    this.raw = const {},
  });

  factory TrainStatus.fromJson(Map<String, dynamic> j) {
    final pos = j['lastKnownPosition'];
    LastSeen? seen;
    if (pos != null) {
      final p = _map(pos);
      final at = _time(p['departedAt']) ?? _time(p['arrivedAt']);
      if (at != null) {
        seen = LastSeen(
          p['stationCode'] as String,
          p['stationName'] as String,
          p['state'] as String,
          at,
        );
      }
    }
    return TrainStatus(
      trainNumber: j['trainNumber'] as String,
      status: j['statusKey'] as String,
      delayMinutes: (j['delayMinutes'] as num).toInt(),
      delayKnown: j['delayKnown'] as bool? ?? true,
      stops:
          (j['stops'] as List).map((s) => StopView.fromJson(_map(s))).toList()
            ..sort((a, b) => a.sequence.compareTo(b.sequence)),
      serverTime: _time(j['serverTime'])!,
      lastSeen: seen,
      lastEventAt: _time(j['lastEventAt']),
      operatingDate: j['operatingDate'] as String,
      reasonCode: j['reason'] == null
          ? null
          : _map(j['reason'])['code'] as String?,
      reasonVerifiedAt: j['reason'] == null
          ? null
          : _time(_map(j['reason'])['verifiedAt']),
      raw: j,
    );
  }

  final String trainNumber;

  /// `Scheduled`, `OnTime`, `Delayed` or `Completed`.
  final String status;
  final int delayMinutes;
  final bool delayKnown;
  final List<StopView> stops;
  final DateTime serverTime;
  final LastSeen? lastSeen;
  final DateTime? lastEventAt;
  final String operatingDate;

  /// The delay reason Operations verified (spec 16 F2), and when. Null until a controller verifies it: passengers never see an unverified reason.
  final String? reasonCode;
  final DateTime? reasonVerifiedAt;

  /// The answer as the server sent it, kept as the offline snapshot.
  final Map<String, dynamic> raw;

  bool get completed => status == 'Completed';
}

class LineStatus {
  const LineStatus(this.statusKey, this.inProgress, this.serverTime);

  factory LineStatus.fromJson(Map<String, dynamic> j) => LineStatus(
    j['statusKey'] as String,
    (j['journeysInProgress'] as num).toInt(),
    _time(j['serverTime'])!,
  );

  /// `Normal` or `SomeDelayed`.
  final String statusKey;
  final int inProgress;
  final DateTime serverTime;
}
