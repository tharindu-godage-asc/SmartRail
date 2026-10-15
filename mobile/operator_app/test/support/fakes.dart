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
      ),
  ],
);

TrainStatus statusWith({
  Set<int> departed = const {},
  int version = 0,
  int delay = 0,
  DateTime? serverTime,
}) => TrainStatus(
  journeyId: demoJourney().journeyId,
  version: version,
  serverTime: serverTime ?? DateTime.utc(2026, 10, 6, 3),
  delayMinutes: delay,
  stops: [
    for (var i = 1; i <= stationNames.length; i++)
      StopProgress(
        serviceStopId: stopId(i),
        sequence: i,
        confirmation: departed.contains(i) ? 'Reported' : 'Pending',
      ),
  ],
);

QueuedReport reportFor(
  int sequence, {
  ReportState state = ReportState.pending,
  DateTime? at,
  String? id,
  int attempts = 0,
  DateTime? nextTryAt,
}) => QueuedReport(
  clientEventId: id ?? 'evt-$sequence',
  journeyId: demoJourney().journeyId,
  serviceStopId: stopId(sequence),
  stationName: stationNames[sequence - 1],
  occurredAt:
      at ?? DateTime.utc(2026, 10, 6, 2, 50).add(Duration(minutes: sequence)),
  state: state,
  attempts: attempts,
  nextTryAt: nextTryAt,
);

class InMemoryReportQueue implements ReportQueue {
  final List<QueuedReport> items = [];
  final _changes = StreamController<void>.broadcast();

  @override
  Future<QueuedReport> enqueue(QueuedReport report) async {
    final existing = items.where(
      (r) =>
          r.journeyId == report.journeyId &&
          r.serviceStopId == report.serviceStopId &&
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
