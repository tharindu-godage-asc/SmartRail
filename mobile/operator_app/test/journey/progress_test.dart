import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/journey/domain/progress.dart';
import 'package:operator_app/features/reports/domain/report.dart';

import '../support/fakes.dart';

void main() {
  final journey = demoJourney();

  JourneyProgress progress({
    Set<int> departed = const {},
    Set<int> arrivedOnly = const {},
    List<QueuedReport> queued = const [],
    bool withStatus = true,
    String statusKey = '',
  }) => deriveProgress(
    journey: journey,
    reports: queued,
    status: withStatus
        ? statusWith(
            departed: departed,
            arrivedOnly: arrivedOnly,
            statusKey: statusKey,
          )
        : null,
  );

  /// "depart Gampaha", "arrive Gampaha" or "finished".
  String describe(JourneyProgress p) => switch (p.next) {
    DepartFrom(:final stop) => 'depart ${stop.stationName}',
    ArriveAt(:final stop) => 'arrive ${stop.stationName}',
    JourneyFinished() => 'finished',
  };

  QueuedReport arrival(
    int sequence, {
    ReportState state = ReportState.pending,
  }) => reportFor(
    sequence,
    kind: ReportKind.arrived,
    state: state,
    id: 'arr-$sequence',
  );

  group('departures (AC-E03-01, AC-E03-02)', () {
    test(
      'before any report the first stop is the one to depart (AC-E03-01 3)',
      () {
        final p = progress();
        expect(describe(p), 'depart Veyangoda');
        expect(p.marks[stopId(1)], StopMark.current);
        expect(p.marks[stopId(2)], StopMark.upcoming);
      },
    );

    test('after Veyangoda departed it is done and Gampaha comes next (AC-E03-01 2)', () {
      final p = progress(departed: {1});
      expect(p.marks[stopId(1)], StopMark.done);
      expect(p.marks[stopId(2)], StopMark.current);
      expect(describe(p), 'arrive Gampaha');
    });

    test('a report waiting on the phone already moves the screen on (AC-E03-02 11)', () {
      final p = progress(queued: [reportFor(1)]);
      expect(p.marks[stopId(1)], StopMark.done);
      expect(describe(p), 'arrive Gampaha');
    });

    test('a rejected report does not move the screen on', () {
      final p = progress(queued: [reportFor(1, state: ReportState.rejected)]);
      expect(describe(p), 'depart Veyangoda');
    });

    test('a departure from a later stop means the earlier ones were passed (AC-E03-02 6)', () {
      final p = progress(departed: {3});
      expect(p.marks[stopId(1)], StopMark.done);
      expect(p.marks[stopId(2)], StopMark.done);
      expect(describe(p), 'depart Maradana');
    });

    test('works from the queue alone when the status read is not available (offline)', () {
      expect(
        describe(progress(withStatus: false, queued: [reportFor(1)])),
        'arrive Gampaha',
      );
    });
  });

  group('arrivals (AC-E03-03)', () {
    test('at a designated stop the arrival comes first, then the departure (Nimal-Journey)', () {
      expect(describe(progress(departed: {1})), 'arrive Gampaha');
      expect(
        describe(progress(departed: {1}, arrivedOnly: {2})),
        'depart Gampaha',
      );
    });

    test(
      'a stop with only an arrival is not done: the train is still there',
      () {
        final p = progress(departed: {1}, arrivedOnly: {2});
        expect(p.marks[stopId(2)], StopMark.current);
      },
    );

    test('an arrival waiting on the phone offers the departure at once, offline too', () {
      final p = progress(withStatus: false, queued: [reportFor(1), arrival(2)]);
      expect(describe(p), 'depart Gampaha');
    });

    test(
      'no arrival is offered at a stop that is not designated (AC-E03-03 2)',
      () {
        // Gampaha is done, so Ragama (not designated) is next: only its departure.
        expect(describe(progress(departed: {1, 2})), 'depart Ragama');
        expect(describe(progress(departed: {1, 2, 3})), 'depart Maradana');
      },
    );

    test('the first stop never offers an arrival', () {
      expect(describe(progress()), 'depart Veyangoda');
    });

    test('a rejected arrival is offered again', () {
      final p = progress(
        departed: {1},
        queued: [arrival(2, state: ReportState.rejected)],
      );
      expect(describe(p), 'arrive Gampaha');
    });

    test(
      'the departure and the arrival for one stop are told apart by kind',
      () {
        // A queued departure from Gampaha does not count as an arrival there, but it does mean the train has left.
        final p = progress(departed: {1}, queued: [reportFor(2)]);
        expect(describe(p), 'depart Ragama');
        expect(p.marks[stopId(2)], StopMark.done);
      },
    );
  });

  group('the end of the journey (AC-E03-03 4)', () {
    test('after the last stop before the final one, the arrival at the final stop is the next action', () {
      final p = progress(departed: {1, 2, 3, 4});
      expect(describe(p), 'arrive Colombo Fort');
      expect(p.marks[stopId(5)], StopMark.current);
    });

    test('an arrival at the final stop on this phone finishes the journey, with its time', () {
      final at = DateTime.utc(2026, 10, 6, 3, 25);
      final p = progress(
        departed: {1, 2, 3, 4},
        queued: [reportFor(5, kind: ReportKind.arrived, at: at, id: 'final')],
      );

      expect(describe(p), 'finished');
      expect((p.next as JourneyFinished).arrivedAt, at);
      expect(p.marks.values, everyElement(StopMark.done));
    });

    test('the server saying Completed finishes it, even when this phone knows nothing', () {
      final p = progress(departed: {1, 2, 3, 4}, statusKey: 'Completed');
      expect(describe(p), 'finished');
    });

    test(
      'a rejected arrival at the final stop does not finish the journey',
      () {
        final p = progress(
          departed: {1, 2, 3, 4},
          queued: [
            reportFor(5, kind: ReportKind.arrived, state: ReportState.rejected),
          ],
        );
        expect(describe(p), 'arrive Colombo Fort');
      },
    );
  });

  test(
    'a delay report moves nothing on: it belongs to the journey, not to a stop',
    () {
      final p = progress(queued: [delayReport()]);
      expect(describe(p), 'depart Veyangoda');
    },
  );
}
