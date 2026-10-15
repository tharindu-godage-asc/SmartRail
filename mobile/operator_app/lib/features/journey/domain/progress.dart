import '../../reports/domain/report.dart';
import 'models.dart';

enum StopMark { done, current, upcoming }

sealed class NextAction {
  const NextAction();
}

/// The one report the operator can make now: `Departed <stop>` (AC-E03-01 3, AC-E03-02 9).
final class DepartFrom extends NextAction {
  const DepartFrom(this.stop);
  final OperatorStop stop;
}

/// Every departure that can be reported has been (slice 1 has no departure from the final stop and no arrival report).
final class NoMoreReports extends NextAction {
  const NoMoreReports(this.finalStop);
  final OperatorStop finalStop;
}

class JourneyProgress {
  const JourneyProgress({required this.marks, required this.next});

  final Map<String, StopMark> marks;
  final NextAction next;
}

/// Works out which stops are done and what to offer next, from three sources: the journey, the public status
/// (what the server knows, including reports from other phones) and the reports waiting on this phone, so a
/// tap made offline moves the screen on at once (AC-E03-02 11).
JourneyProgress deriveProgress({
  required OperatorJourney journey,
  required Iterable<QueuedReport> reports,
  TrainStatus? status,
}) {
  final departed = <String>{
    if (status != null)
      ...status.stops.where((s) => s.hasDeparted).map((s) => s.serviceStopId),
    // A rejected report did not happen as far as the server is concerned, so it does not move the screen on.
    ...reports
        .where((r) => r.state != ReportState.rejected)
        .map((r) => r.serviceStopId),
  };

  var lastDone = 0;
  for (final stop in journey.stops) {
    if (departed.contains(stop.serviceStopId) && stop.sequence > lastDone) {
      lastDone = stop.sequence;
    }
  }

  final next = journey.stops.firstWhere(
    (s) => s.sequence > lastDone,
    orElse: () => journey.finalStop,
  );
  final marks = {
    for (final stop in journey.stops)
      stop.serviceStopId: stop.sequence <= lastDone
          ? StopMark.done
          : stop.serviceStopId == next.serviceStopId
          ? StopMark.current
          : StopMark.upcoming,
  };

  return JourneyProgress(
    marks: marks,
    next: next.isFinalStop ? NoMoreReports(next) : DepartFrom(next),
  );
}
