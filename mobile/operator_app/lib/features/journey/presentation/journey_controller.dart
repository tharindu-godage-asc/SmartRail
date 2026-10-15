import 'dart:async';

import 'package:clock/clock.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/providers.dart';
import '../../../core/time.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../reports/domain/report.dart';
import '../domain/journey_api.dart';
import '../domain/models.dart';
import '../domain/progress.dart';

/// What the journey screen shows. [journey] is null when nothing is assigned (or nothing could be loaded: see [failed]).
class JourneyViewState {
  const JourneyViewState({
    this.journey,
    this.status,
    this.fetchedAt,
    this.offline = false,
    this.failed = false,
  });

  final OperatorJourney? journey;
  final TrainStatus? status;

  /// When the phone last got the journey from the server (shown as "Last updated" while offline, AC-E03-01 5).
  final DateTime? fetchedAt;

  /// The last attempt could not reach the server; what is shown is from [fetchedAt].
  final bool offline;

  /// There is nothing to show and the server could not be reached.
  final bool failed;
}

/// The server's clock minus the phone's, kept between runs so a report made offline is still stamped with corrected time.
class ClockOffsetController extends Notifier<ClockOffset> {
  @override
  ClockOffset build() => ClockOffset.none;

  Future<void> restore() async {
    final saved = await ref.read(localCacheProvider).readClockOffset();
    if (saved != null) {
      state = ClockOffset(saved);
    }
  }

  Future<void> update(ClockOffset offset) async {
    state = offset;
    await ref.read(localCacheProvider).writeClockOffset(offset.value);
  }
}

final clockOffsetProvider =
    NotifierProvider<ClockOffsetController, ClockOffset>(
      ClockOffsetController.new,
    );

/// Loads the assigned journey and the public status of its train, keeps them fresh (every 30 s and after each send),
/// and falls back to what is saved on the phone when there is no network.
class JourneyController extends AsyncNotifier<JourneyViewState> {
  static const pollEvery = Duration(seconds: 30);

  @override
  Future<JourneyViewState> build() async {
    final timer = Timer.periodic(pollEvery, (_) => refresh());
    ref.onDispose(timer.cancel);

    final auth = await ref.watch(authControllerProvider.future);
    if (!auth.isSignedIn) {
      return const JourneyViewState();
    }

    await ref.read(clockOffsetProvider.notifier).restore();
    final cached = await ref.read(localCacheProvider).readJourney();
    if (cached != null) {
      // Show what we have at once, even with no network, and bring it up to date behind it.
      Future.microtask(refresh);
      return JourneyViewState(
        journey: cached.journey,
        fetchedAt: cached.fetchedAt,
        offline: true,
      );
    }
    return _load(null);
  }

  Future<void> refresh() async {
    final current = state.value;
    if (current == null ||
        ref.read(authControllerProvider).value?.isSignedIn != true) {
      return;
    }
    final next = await _load(current);
    if (ref.mounted) {
      state = AsyncData(next);
    }
  }

  Future<JourneyViewState> _load(JourneyViewState? previous) async {
    final api = ref.read(journeyApiProvider);
    final cache = ref.read(localCacheProvider);
    try {
      final journey = await api.fetchAssignedJourney();
      if (journey == null) {
        await cache.clearJourney();
        return const JourneyViewState();
      }

      final status = await _loadStatus(api, journey, previous);
      final now = clock.now().toUtc();
      await cache.writeJourney(journey, now);
      return JourneyViewState(journey: journey, status: status, fetchedAt: now);
    } on NetworkException {
      return _unreachable(previous);
    } on ServerException {
      return _unreachable(previous);
    } on SessionExpiredException {
      ref.read(authControllerProvider.notifier).markSessionExpired();
      return previous ?? const JourneyViewState();
    }
  }

  JourneyViewState _unreachable(JourneyViewState? previous) {
    if (previous?.journey == null) {
      return const JourneyViewState(failed: true);
    }
    return JourneyViewState(
      journey: previous!.journey,
      status: previous.status,
      fetchedAt: previous.fetchedAt,
      offline: true,
    );
  }

  Future<TrainStatus?> _loadStatus(
    JourneyApi api,
    OperatorJourney journey,
    JourneyViewState? previous,
  ) async {
    try {
      final sentAt = clock.now().toUtc();
      final status = await api.fetchTrainStatus(journey.trainNumber);
      final receivedAt = clock.now().toUtc();
      if (status == null || status.journeyId != journey.journeyId) {
        return null;
      }

      // The server's clock was read somewhere in the middle of the round trip (spec rule 1).
      final middle = sentAt.add(receivedAt.difference(sentAt) ~/ 2);
      await ref
          .read(clockOffsetProvider.notifier)
          .update(
            ClockOffset.measure(
              serverTime: status.serverTime,
              receivedAt: middle,
            ),
          );

      // An older answer than the one on screen is ignored (ADR-09: the version only goes up).
      final shown = previous?.status;
      return shown != null &&
              shown.journeyId == status.journeyId &&
              status.version < shown.version
          ? shown
          : status;
    } on NetworkException {
      return previous?.status;
    } on ServerException {
      return previous?.status;
    }
  }
}

final journeyControllerProvider =
    AsyncNotifierProvider<JourneyController, JourneyViewState>(
      JourneyController.new,
    );

/// Every report of the shown journey on this phone, newest first.
final reportsProvider = StreamProvider.family<List<QueuedReport>, String>(
  (ref, journeyId) => ref.watch(reportQueueProvider).watch(journeyId),
);

/// Which stops are done and what to offer next (null while there is no journey).
final journeyProgressProvider = Provider<JourneyProgress?>((ref) {
  final view = ref.watch(journeyControllerProvider).value;
  final journey = view?.journey;
  if (journey == null) {
    return null;
  }
  final reports =
      ref.watch(reportsProvider(journey.journeyId)).value ??
      const <QueuedReport>[];
  return deriveProgress(
    journey: journey,
    reports: reports,
    status: view!.status,
  );
});
