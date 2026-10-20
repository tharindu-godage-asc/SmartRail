import '../../reports/domain/report.dart';
import 'models.dart';

enum StopMark { done, current, upcoming }

sealed class NextAction {
  const NextAction();
}

/// The train is at (or coming into) a stop that has an arrival: report `Arrived <stop>` (AC-E03-03 1). At an in-between stop
/// [canDepartInstead] is true: reporting the arrival is the operator's choice, so `Departed <stop>` is offered beside it and a missed
/// arrival never holds the departure up (spec 19). At the final stop there is no departure, so it is the only button.
final class ArriveAt extends NextAction {
  const ArriveAt(this.stop, {this.canDepartInstead = false});
  final OperatorStop stop;
  final bool canDepartInstead;
}

/// The one report the operator can make now when the train is leaving: `Departed <stop>` (AC-E03-01 3, AC-E03-02 9).
final class DepartFrom extends NextAction {
  const DepartFrom(this.stop);
  final OperatorStop stop;
}

/// The arrival at the final stop is reported: the journey is complete and there is nothing more to send (AC-E03-03 4).
final class JourneyFinished extends NextAction {
  const JourneyFinished(this.finalStop, {this.arrivedAt});
  final OperatorStop finalStop;

  /// When the train reached the final stop, if known (the report on this phone, or the server's record).
  final DateTime? arrivedAt;
}

class JourneyProgress {
  const JourneyProgress({required this.marks, required this.next});

  final Map<String, StopMark> marks;
  final NextAction next;
}

/// Works out which stops are done and what to offer next, from three sources: the journey, the public status
/// (what the server knows, including reports from other phones) and the reports waiting on this phone, so a
/// tap made offline moves the screen on at once (AC-E03-02 11).
///
/// At a stop with an arrival (every stop but the first, spec 19) the operator can report the arrival and then the departure, or the
/// departure alone (`Next: Gampaha (arrival or departure)`, then `Next: Gampaha (departure)`); at the first stop only the departure is
/// offered. At the final stop only the arrival is offered.
JourneyProgress deriveProgress({
  required OperatorJourney journey,
  required Iterable<QueuedReport> reports,
  TrainStatus? status,
}) {
  final arrived = <String>{};
  final departed = <String>{};

  if (status != null) {
    for (final stop in status.stops) {
      if (stop.actualArrival != null) {
        arrived.add(stop.serviceStopId);
      }
      if (stop.hasDeparted) {
        departed.add(stop.serviceStopId);
      }
    }
  }

  // A rejected report did not happen as far as the server is concerned, so it does not move the screen on.
  for (final report in reports.where(
    (r) => r.state != ReportState.rejected && r.serviceStopId != null,
  )) {
    (report.kind == ReportKind.arrived ? arrived : departed).add(
      report.serviceStopId!,
    );
  }

  final sequenceOf = {
    for (final s in journey.stops) s.serviceStopId: s.sequence,
  };
  DateTime? finalArrivedAt;
  var lastActive = 0;
  for (final id in {...arrived, ...departed}) {
    final sequence = sequenceOf[id];
    if (sequence != null && sequence > lastActive) {
      lastActive = sequence;
    }
  }

  bool isDone(OperatorStop stop) =>
      stop.sequence < lastActive || departed.contains(stop.serviceStopId);

  final finalStop = journey.finalStop;
  for (final report in reports.where(
    (r) =>
        r.state != ReportState.rejected &&
        r.kind == ReportKind.arrived &&
        r.serviceStopId == finalStop.serviceStopId,
  )) {
    finalArrivedAt = report.occurredAt;
  }
  for (final stop in status?.stops ?? const <StopProgress>[]) {
    if (stop.serviceStopId == finalStop.serviceStopId &&
        stop.actualArrival != null) {
      finalArrivedAt ??= stop.actualArrival;
    }
  }
  final finished =
      status?.statusKey == 'Completed' ||
      arrived.contains(finalStop.serviceStopId);

  final current = journey.stops.firstWhere(
    (s) => !isDone(s),
    orElse: () => finalStop,
  );
  final marks = {
    for (final stop in journey.stops)
      stop.serviceStopId: finished || isDone(stop)
          ? StopMark.done
          : stop.serviceStopId == current.serviceStopId
          ? StopMark.current
          : StopMark.upcoming,
  };

  final NextAction next;
  if (finished) {
    next = JourneyFinished(finalStop, arrivedAt: finalArrivedAt);
  } else if (current.isArrivalStation &&
      !arrived.contains(current.serviceStopId)) {
    next = ArriveAt(current, canDepartInstead: !current.isFinalStop);
  } else {
    next = DepartFrom(current);
  }

  return JourneyProgress(marks: marks, next: next);
}
