import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/journey/domain/progress.dart';
import 'package:operator_app/features/reports/domain/report.dart';

import '../support/fakes.dart';

void main() {
  final journey = demoJourney();

  JourneyProgress progress({
    Set<int> departed = const {},
    List<QueuedReport> queued = const [],
    bool withStatus = true,
  }) => deriveProgress(
    journey: journey,
    reports: queued,
    status: withStatus ? statusWith(departed: departed) : null,
  );

  String? nextStation(JourneyProgress p) => switch (p.next) {
    DepartFrom(:final stop) => stop.stationName,
    NoMoreReports() => null,
  };

  test(
    'before any report the first stop is the one to depart (AC-E03-01 3)',
    () {
      final p = progress();
      expect(nextStation(p), 'Veyangoda');
      expect(p.marks[stopId(1)], StopMark.current);
      expect(p.marks[stopId(2)], StopMark.upcoming);
    },
  );

  test(
    'after Veyangoda departed it is done and Gampaha is next (AC-E03-01 2)',
    () {
      final p = progress(departed: {1});
      expect(p.marks[stopId(1)], StopMark.done);
      expect(p.marks[stopId(2)], StopMark.current);
      expect(nextStation(p), 'Gampaha');
    },
  );

  test('"Departed Veyangoda" is no longer offered once it is recorded (AC-E03-02 9)', () {
    expect(nextStation(progress(departed: {1})), isNot('Veyangoda'));
  });

  test(
    'a report waiting on the phone already moves the screen on (AC-E03-02 11)',
    () {
      final p = progress(queued: [reportFor(1)]);
      expect(p.marks[stopId(1)], StopMark.done);
      expect(nextStation(p), 'Gampaha');
    },
  );

  test('two queued reports offline: the next action is already Ragama', () {
    expect(
      nextStation(progress(queued: [reportFor(1), reportFor(2)])),
      'Ragama',
    );
  });

  test('a rejected report does not move the screen on', () {
    final p = progress(queued: [reportFor(1, state: ReportState.rejected)]);
    expect(nextStation(p), 'Veyangoda');
  });

  test('a departure from a later stop means the earlier ones were passed (AC-E03-02 6)', () {
    final p = deriveProgress(
      journey: journey,
      reports: const [],
      status: statusWith(departed: {3}),
    );
    expect(p.marks[stopId(1)], StopMark.done);
    expect(p.marks[stopId(2)], StopMark.done);
    expect(nextStation(p), 'Maradana');
  });

  test('works from the queue alone when the status read is not available (offline)', () {
    expect(
      nextStation(progress(withStatus: false, queued: [reportFor(1)])),
      'Gampaha',
    );
  });

  test('after the last allowed departure there is nothing left to report', () {
    final p = progress(departed: {1, 2, 3, 4});
    expect(
      p.next,
      isA<NoMoreReports>().having(
        (n) => n.finalStop.stationName,
        'final stop',
        'Colombo Fort',
      ),
    );
    expect(p.marks[stopId(5)], StopMark.current);
  });
}
