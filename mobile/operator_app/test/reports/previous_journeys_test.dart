import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/history/domain/history.dart';

import '../support/fakes.dart';
import '../support/harness.dart';

void main() {
  Future<void> openPrevious(WidgetTester tester, TestApp app) async {
    await app.pump(tester);
    await tester.tap(find.text('Reports').last);
    await settle(tester);
    await tester.tap(find.text('Previous journeys'));
    await settle(tester);
  }

  group('Previous journeys (spec 12 part 2)', () {
    testWidgets(
      'lists the journeys with train, route, day, state and report count',
      (tester) async {
        final app = TestApp(
          history: FakeHistoryApi(
            pages: [
              historyPage([
                pastJourney('j1'),
                pastJourney(
                  'j2',
                  train: '8075',
                  reports: 1,
                  late: null,
                  status: 'Scheduled',
                  day: DateTime(2026, 10, 14),
                ),
              ]),
            ],
          ),
        );
        await openPrevious(tester, app);

        expect(
          find.text('Train 8058 Veyangoda → Colombo Fort'),
          findsOneWidget,
        );
        expect(
          find.text(
            'Thu 15 Oct · Completed · 7 reports · arrived +10 min late',
          ),
          findsOneWidget,
        );
        expect(
          find.text('Wed 14 Oct · Not finished · 1 report'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets('with no journeys it says so', (tester) async {
      final app = TestApp();
      await openPrevious(tester, app);

      expect(find.text('No previous journeys yet.'), findsOneWidget);
      await app.close(tester);
    });

    testWidgets(
      'with no connection and no copy it says it needs a connection; with a copy it shows it with Last updated',
      (tester) async {
        final offline = FakeHistoryApi(pages: [historyPage([])])
          ..offline = true;
        final app = TestApp(history: offline);
        await openPrevious(tester, app);
        expect(
          find.text('Connect to see your previous journeys.'),
          findsOneWidget,
        );
        await app.close(tester);

        final cache = InMemoryLocalCache()
          ..history =
              '{"serverTime":"2026-10-19T03:12:00Z","next":null,"journeys":[{"journeyId":"j1","trainNumber":"8058","fromName":"Veyangoda","toName":"Colombo Fort","operatingDate":"2026-10-15","status":"Completed","myReportCount":7,"arrivalDelayMinutes":10}]}';
        final app2 = TestApp(
          cache: cache,
          history: FakeHistoryApi(pages: [historyPage([])])..offline = true,
        );
        await openPrevious(tester, app2);
        expect(find.text('Last updated 08:42.'), findsOneWidget);
        expect(
          find.text('Train 8058 Veyangoda → Colombo Fort'),
          findsOneWidget,
        );
        await app2.close(tester);
      },
    );

    testWidgets(
      'a good answer is saved for next time and Show earlier adds the next page',
      (tester) async {
        final cache = InMemoryLocalCache();
        final app = TestApp(
          cache: cache,
          history: FakeHistoryApi(
            pages: [
              historyPage([
                pastJourney('j1'),
              ], next: DateTime.utc(2026, 10, 15)),
              historyPage([
                pastJourney('j2', train: '8075', day: DateTime(2026, 10, 14)),
              ]),
            ],
          ),
        );
        await openPrevious(tester, app);
        expect(cache.history, isNotNull);
        expect(find.textContaining('Train 8075'), findsNothing);

        await tester.tap(find.text('Show earlier'));
        await settle(tester);

        expect(find.textContaining('Train 8075'), findsOneWidget);
        expect(find.text('Show earlier'), findsNothing);
        await app.close(tester);
      },
    );

    testWidgets(
      'a journey opens its read-only record: stops with actual and scheduled times, and his reports',
      (tester) async {
        final history =
            FakeHistoryApi(
                pages: [
                  historyPage([pastJourney('j1')]),
                ],
              )
              ..records = {
                'j1': JourneyRecord.fromJson({
                  'trainNumber': '8058',
                  'operatingDate': '2026-10-15',
                  'status': 'Completed',
                  'stops': [
                    {
                      'stationName': 'Veyangoda',
                      'confirmation': 'Reported',
                      'scheduledDeparture': '2026-10-15T02:50:00Z',
                      'actualDeparture': '2026-10-15T02:58:00Z',
                    },
                    {
                      'stationName': 'Gampaha',
                      'confirmation': 'Inferred',
                      'scheduledArrival': '2026-10-15T03:00:00Z',
                    },
                  ],
                  'myReports': [
                    {
                      'type': 'Departed',
                      'stationName': 'Veyangoda',
                      'occurredAt': '2026-10-15T02:58:00Z',
                    },
                    {
                      'type': 'DelayReported',
                      'occurredAt': '2026-10-15T03:11:00Z',
                      'delayReason': 'SignalIssue',
                      'totalDelayMinutes': 10,
                    },
                  ],
                }),
              };
        final app = TestApp(history: history);
        await openPrevious(tester, app);

        await tester.tap(find.text('Train 8058 Veyangoda → Colombo Fort'));
        await settle(tester);

        expect(find.text('Journey record'), findsOneWidget);
        expect(find.text('Dep 08:28 (08:20)'), findsOneWidget);
        expect(find.text('Passed (not reported)'), findsOneWidget);
        expect(find.text('Departed Veyangoda'), findsOneWidget);
        expect(find.text('Delay: Signal issue, ~10 min'), findsOneWidget);
        expect(find.textContaining('cannot be changed'), findsOneWidget);
        await app.close(tester);
      },
    );

    testWidgets('a record that cannot be loaded says it needs a connection', (
      tester,
    ) async {
      final history = FakeHistoryApi(
        pages: [
          historyPage([pastJourney('j1')]),
        ],
      );
      final app = TestApp(history: history);
      await openPrevious(tester, app);
      history.offline = true;

      await tester.tap(find.text('Train 8058 Veyangoda → Colombo Fort'));
      await settle(tester);

      expect(
        find.textContaining('Connect to open this journey'),
        findsOneWidget,
      );
      await app.close(tester);
    });
  });
}
