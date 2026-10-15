import 'dart:async';

import 'package:clock/clock.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../app/providers.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../journey/domain/models.dart';
import '../../journey/presentation/journey_controller.dart';
import '../domain/report.dart';
import '../domain/report_queue.dart';
import '../domain/report_sender.dart';

/// Runs the [ReportSender] whenever there may be something to send: after a tap, when the network comes back, after
/// sign-in, and when a retry is due. The app keeps this provider alive from the root widget.
class ReportDispatcher extends Notifier<void> {
  Timer? _timer;
  bool _running = false;
  bool _again = false;

  @override
  void build() {
    ref.onDispose(() => _timer?.cancel());
    ref.listen(authControllerProvider, (_, next) {
      if (next.value?.isSignedIn ?? false) {
        unawaited(kick());
      }
    });
    ref.listen(isOnlineProvider, (_, next) {
      if (next.value ?? false) {
        unawaited(kick());
      }
    });
    Future.microtask(kick);
  }

  /// Sends what is due now. Safe to call at any time: a call during a pass makes the pass run once more.
  Future<void> kick() async {
    if (_running) {
      _again = true;
      return;
    }
    _running = true;
    try {
      do {
        _again = false;
        if (!(ref.read(authControllerProvider).value?.isSignedIn ?? false)) {
          break;
        }
        await _pass();
      } while (_again && ref.mounted);
    } finally {
      _running = false;
    }
  }

  Future<void> _pass() async {
    final queue = ref.read(reportQueueProvider);
    final before = await queue.pendingCount();
    final sender = ReportSender(queue: queue, api: ref.read(reportApiProvider));
    final outcome = await sender.pump(clock.now().toUtc());
    if (!ref.mounted) {
      return;
    }

    switch (outcome) {
      case QueueEmpty():
        _timer?.cancel();
      case WaitUntil(:final at):
        _timer?.cancel();
        final wait = at.difference(clock.now().toUtc());
        _timer = Timer(
          wait.isNegative ? Duration.zero : wait,
          () => unawaited(kick()),
        );
      case NeedsSignIn():
        _timer?.cancel();
        ref.read(authControllerProvider.notifier).markSessionExpired();
    }

    // Something was delivered or refused: show the new state of the journey straight away.
    if (await queue.pendingCount() < before && ref.mounted) {
      unawaited(ref.read(journeyControllerProvider.notifier).refresh());
    }
  }
}

final reportDispatcherProvider = NotifierProvider<ReportDispatcher, void>(
  ReportDispatcher.new,
);

/// What the screens can do with reports.
class ReportActions {
  const ReportActions(this._ref);

  final Ref _ref;

  /// `Departed <stop>`: save the report with the moment of the tap (spec rule 1), then send it when possible.
  Future<QueuedReport> departed(OperatorJourney journey, OperatorStop stop) =>
      _save(journey, ReportKind.departed, stop: stop);

  /// `Arrived <stop>` at a designated stop or the final stop (AC-E03-03).
  Future<QueuedReport> arrived(OperatorJourney journey, OperatorStop stop) =>
      _save(journey, ReportKind.arrived, stop: stop);

  /// A delay: why, and the TOTAL delay in minutes against the timetable, or null for "unknown" (AC-E03-04).
  Future<QueuedReport> delayed(
    OperatorJourney journey, {
    required DelayReason reason,
    int? totalMinutes,
  }) => _save(journey, ReportKind.delay, reason: reason, total: totalMinutes);

  Future<QueuedReport> _save(
    OperatorJourney journey,
    ReportKind kind, {
    OperatorStop? stop,
    DelayReason? reason,
    int? total,
  }) async {
    final queue = _ref.read(reportQueueProvider);
    final report = await queue.enqueue(
      QueuedReport(
        clientEventId: const Uuid().v4(),
        journeyId: journey.journeyId,
        kind: kind,
        serviceStopId: stop?.serviceStopId,
        stationName: stop?.stationName ?? '',
        occurredAt: await _stamp(queue, journey.journeyId),
        delayReason: reason,
        totalDelayMinutes: total,
      ),
    );
    unawaited(_ref.read(reportDispatcherProvider.notifier).kick());
    return report;
  }

  /// The moment of the tap (spec rule 1), in whole seconds so a retry sends exactly what is stored. The server refuses two
  /// reports of one journey at the same moment, so it is never earlier than one second after the previous report.
  Future<DateTime> _stamp(ReportQueue queue, String journeyId) async {
    final now = _ref.read(clockOffsetProvider).now();
    final tap = DateTime.utc(
      now.year,
      now.month,
      now.day,
      now.hour,
      now.minute,
      now.second,
    );
    final previous = await queue.latestOccurredAt(journeyId);
    return previous != null && !tap.isAfter(previous)
        ? previous.add(const Duration(seconds: 1))
        : tap;
  }
}

final reportActionsProvider = Provider<ReportActions>(ReportActions.new);
