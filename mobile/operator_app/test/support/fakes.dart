import 'package:operator_app/features/history/domain/history.dart';

import 'dart:async';

import 'package:operator_app/features/auth/domain/auth.dart';
import 'package:operator_app/features/auth/domain/auth_ports.dart';
import 'package:operator_app/features/journey/domain/journey_api.dart';
import 'package:operator_app/features/journey/domain/local_cache.dart';
import 'package:operator_app/features/journey/domain/models.dart';
import 'package:operator_app/features/reports/domain/report.dart';
import 'package:operator_app/features/reports/domain/report_queue.dart';
import 'package:operator_app/features/reports/domain/send_result.dart';

/// The demo journey: train 8058 as the seed has it, five stops, Colombo Fort last.
const stationNames = [
  'Veyangoda',
  'Gampaha',
  'Ragama',
  'Maradana',
  'Colombo Fort',
];

String stopId(int sequence) => '00000004-0000-4000-8000-00000000000$sequence';

OperatorJourney demoJourney({int version = 0}) => OperatorJourney(
  journeyId: '00000005-0000-4000-8000-000000000001',
  trainNumber: '8058',
  operatingDate: '2026-10-06',
  status: 'Scheduled',
  version: version,
  stops: [
    for (var i = 0; i < stationNames.length; i++)
      OperatorStop(
        serviceStopId: stopId(i + 1),
        sequence: i + 1,
        stationCode: 'S${i + 1}',
        stationName: stationNames[i],
        isFinalStop: i == stationNames.length - 1,
        // Gampaha is designated in the seed; the final stop is an arrival station by rule (spec 06, Q2).
        isArrivalStation: i == 1 || i == stationNames.length - 1,
      ),
  ],
);

/// The public status as the server would send it. [departed] stops have a departure (and an arrival where the stop has one),
/// [arrivedOnly] stops have only an arrival: `Reported`, but the train has not left.
TrainStatus statusWith({
  Set<int> departed = const {},
  Set<int> arrivedOnly = const {},
  int version = 0,
  int delay = 0,
  bool delayKnown = true,
  String statusKey = '',
  DateTime? serverTime,
}) => TrainStatus(
  journeyId: demoJourney().journeyId,
  version: version,
  serverTime: serverTime ?? DateTime.utc(2026, 10, 6, 3),
  delayMinutes: delay,
  delayKnown: delayKnown,
  statusKey: statusKey,
  stops: [
    for (var i = 1; i <= stationNames.length; i++)
      StopProgress(
        serviceStopId: stopId(i),
        sequence: i,
        confirmation: departed.contains(i) || arrivedOnly.contains(i)
            ? 'Reported'
            : 'Pending',
        actualArrival: arrivedOnly.contains(i)
            ? DateTime.utc(2026, 10, 6, 3)
            : null,
        actualDeparture: departed.contains(i)
            ? DateTime.utc(2026, 10, 6, 3)
            : null,
      ),
  ],
);

QueuedReport reportFor(
  int sequence, {
  ReportKind kind = ReportKind.departed,
  ReportState state = ReportState.pending,
  DateTime? at,
  String? id,
  int attempts = 0,
  DateTime? nextTryAt,
}) => QueuedReport(
  clientEventId: id ?? 'evt-$sequence',
  journeyId: demoJourney().journeyId,
  kind: kind,
  serviceStopId: stopId(sequence),
  stationName: stationNames[sequence - 1],
  occurredAt:
      at ?? DateTime.utc(2026, 10, 6, 2, 50).add(Duration(minutes: sequence)),
  state: state,
  attempts: attempts,
  nextTryAt: nextTryAt,
);

/// A delay report as the app stores it. A null [total] is "unknown".
QueuedReport delayReport({
  String id = 'delay-1',
  DelayReason reason = DelayReason.signalIssue,
  int? total = 10,
  DateTime? at,
  ReportState state = ReportState.pending,
}) => QueuedReport(
  clientEventId: id,
  journeyId: demoJourney().journeyId,
  kind: ReportKind.delay,
  occurredAt: at ?? DateTime.utc(2026, 10, 6, 3),
  delayReason: reason,
  totalDelayMinutes: total,
  state: state,
);

class InMemoryReportQueue implements ReportQueue {
  final List<QueuedReport> items = [];
  final _changes = StreamController<void>.broadcast();

  @override
  Future<QueuedReport> enqueue(QueuedReport report) async {
    // Same rule as the real queue: one departure or arrival per journey, stop and kind; a delay is always saved.
    final existing = report.isDelay
        ? const <QueuedReport>[]
        : items.where(
            (r) =>
                r.journeyId == report.journeyId &&
                r.serviceStopId == report.serviceStopId &&
                r.kind == report.kind &&
                r.state != ReportState.rejected,
          );
    if (existing.isNotEmpty) {
      return existing.first;
    }
    items.add(report);
    _changes.add(null);
    return report;
  }

  @override
  Future<DateTime?> latestOccurredAt(String journeyId) async {
    final times = items
        .where((r) => r.journeyId == journeyId)
        .map((r) => r.occurredAt);
    return times.isEmpty ? null : times.reduce((a, b) => a.isAfter(b) ? a : b);
  }

  @override
  Future<List<QueuedReport>> pending() async =>
      (items.where((r) => r.state == ReportState.pending).toList()
        ..sort((a, b) => a.occurredAt.compareTo(b.occurredAt)));

  @override
  Future<void> update(QueuedReport report) async {
    final index = items.indexWhere(
      (r) => r.clientEventId == report.clientEventId,
    );
    items[index] = report;
    _changes.add(null);
  }

  @override
  Stream<List<QueuedReport>> watch(String journeyId) async* {
    List<QueuedReport> snapshot() =>
        (items.where((r) => r.journeyId == journeyId).toList()
          ..sort((a, b) => b.occurredAt.compareTo(a.occurredAt)));
    yield snapshot();
    await for (final _ in _changes.stream) {
      yield snapshot();
    }
  }

  @override
  Stream<List<QueuedReport>> watchAll() async* {
    List<QueuedReport> snapshot() =>
        (List.of(items)..sort((a, b) => b.occurredAt.compareTo(a.occurredAt)));
    yield snapshot();
    await for (final _ in _changes.stream) {
      yield snapshot();
    }
  }

  @override
  Future<int> deleteDeliveredBefore(DateTime cutoff) async {
    final before = items.length;
    items.removeWhere(
      (r) => r.state == ReportState.delivered && r.occurredAt.isBefore(cutoff),
    );
    _changes.add(null);
    return before - items.length;
  }

  @override
  Future<int> pendingCount() async =>
      items.where((r) => r.state == ReportState.pending).length;
}

/// Answers each send from a script, and remembers what it was asked to send.
class ScriptedReportApi implements ReportApi {
  ScriptedReportApi(this._results);

  final List<SendResult> _results;
  final sent = <String>[];

  @override
  Future<SendResult> send(QueuedReport report) async {
    sent.add(report.clientEventId);
    return _results.isEmpty ? const Delivered() : _results.removeAt(0);
  }
}

class FakeAuthGateway implements AuthGateway {
  FakeAuthGateway({this.onAuthorize, this.onRefresh});

  Future<TokenSet> Function()? onAuthorize;
  Future<TokenSet> Function(String refreshToken)? onRefresh;
  int refreshCalls = 0;
  int endSessionCalls = 0;
  String? endedWith;
  Future<void> Function(String refreshToken)? onEndSession;

  @override
  Future<TokenSet> authorize() => onAuthorize!();

  @override
  Future<TokenSet> refresh(String refreshToken) {
    refreshCalls++;
    return onRefresh!(refreshToken);
  }

  @override
  Future<void> endSession(String refreshToken) async {
    endSessionCalls++;
    endedWith = refreshToken;
    await onEndSession?.call(refreshToken);
  }
}

class FakeTokenStore implements TokenStore {
  TokenSet? tokens;
  bool signInStarted = false;

  @override
  Future<TokenSet?> read() async => tokens;

  @override
  Future<void> write(TokenSet value) async => tokens = value;

  @override
  Future<void> clear() async => tokens = null;

  @override
  Future<bool> readSignInStarted() async => signInStarted;

  @override
  Future<void> writeSignInStarted(bool started) async =>
      signInStarted = started;
}

class InMemoryLocalCache implements LocalCache {
  CachedJourney? journey;
  Duration? offset;

  @override
  Future<CachedJourney?> readJourney() async => journey;

  @override
  Future<void> writeJourney(OperatorJourney value, DateTime fetchedAt) async =>
      journey = CachedJourney(value, fetchedAt);

  @override
  Future<void> clearJourney() async => journey = null;

  String? history;

  @override
  Future<String?> readHistoryJson() async => history;

  @override
  Future<void> writeHistoryJson(String json) async => history = json;

  final summaries = <String, JourneySummary>{};

  @override
  Future<Map<String, JourneySummary>> readJourneySummaries() async =>
      Map.of(summaries);

  @override
  Future<void> writeJourneySummary(OperatorJourney journey) async =>
      summaries[journey.journeyId] = JourneySummary.of(journey);

  @override
  Future<Duration?> readClockOffset() async => offset;

  @override
  Future<void> writeClockOffset(Duration value) async => offset = value;
}

/// Answers the two reads from fields the test sets. Set [offline] to make both fail like a dead network.
class FakeJourneyApi implements JourneyApi {
  FakeJourneyApi({this.journey, TrainStatus? status})
    : status = status ?? statusWith();

  OperatorJourney? journey;
  TrainStatus? status;
  bool offline = false;
  bool sessionExpired = false;

  @override
  Future<OperatorJourney?> fetchAssignedJourney() async {
    if (sessionExpired) {
      throw const SessionExpiredException();
    }
    if (offline) {
      throw const NetworkException();
    }
    return journey;
  }

  @override
  Future<TrainStatus?> fetchTrainStatus(String trainNumber) async {
    if (offline) {
      throw const NetworkException();
    }
    return status;
  }
}

/// Never reaches the server: every report stays waiting on the phone.
class OfflineReportApi implements ReportApi {
  @override
  Future<SendResult> send(QueuedReport report) async =>
      const RetryLater('network');
}

/// Answers the history reads from what a test sets. Set [offline] to make both fail like a dead network.
class FakeHistoryApi implements HistoryApi {
  FakeHistoryApi({this.pages = const []});

  /// Pages in the order the app asks: the first page, then each "Show earlier".
  List<HistoryPage> pages;
  Map<String, JourneyRecord?> records = {};
  bool offline = false;
  int calls = 0;

  @override
  Future<HistoryPage> fetchHistory({DateTime? before}) async {
    calls++;
    if (offline) {
      throw const NetworkException();
    }
    final index = before == null ? 0 : 1;
    return pages[index.clamp(0, pages.length - 1)];
  }

  @override
  Future<JourneyRecord?> fetchRecord(String journeyId) async {
    if (offline) {
      throw const NetworkException();
    }
    return records[journeyId];
  }
}

HistoryPage historyPage(
  List<PastJourney> journeys, {
  DateTime? next,
}) => HistoryPage.fromJson({
  'serverTime': '2026-10-19T03:12:00Z',
  'next': next?.toIso8601String(),
  'journeys': [
    for (final j in journeys)
      {
        'journeyId': j.journeyId,
        'trainNumber': j.trainNumber,
        'fromName': j.fromName,
        'toName': j.toName,
        'operatingDate':
            '${j.operatingDate.year}-${j.operatingDate.month.toString().padLeft(2, '0')}-${j.operatingDate.day.toString().padLeft(2, '0')}',
        'status': j.status,
        'myReportCount': j.myReportCount,
        'arrivalDelayMinutes': j.arrivalDelayMinutes,
      },
  ],
});

PastJourney pastJourney(
  String id, {
  String train = '8058',
  int reports = 7,
  int? late = 10,
  DateTime? day,
  String status = 'Completed',
}) => PastJourney(
  journeyId: id,
  trainNumber: train,
  fromName: 'Veyangoda',
  toName: 'Colombo Fort',
  operatingDate: day ?? DateTime(2026, 10, 15),
  status: status,
  myReportCount: reports,
  arrivalDelayMinutes: late,
);
