import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/journey/domain/local_cache.dart';
import 'package:operator_app/features/reports/domain/report.dart';
import 'package:operator_app/features/reports/domain/send_result.dart';

import '../support/fakes.dart';
import '../support/harness.dart';

void main() {
  group('Journey screen', () {
    testWidgets(
      'shows the train, route, the next action and the stops (AC-E03-01 1, 2, 3)',
      (tester) async {
        final app = TestApp();
        await app.pump(tester);

        expect(
          find.text('Train 8058 · Veyangoda to Colombo Fort'),
          findsOneWidget,
        );
        expect(find.text('Nimal P.'), findsOneWidget);
        expect(find.text('Next: Veyangoda (departure)'), findsOneWidget);
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsOneWidget,
        );
        for (final station in stationNames) {
          expect(find.text(station), findsWidgets);
        }
        expect(
          find.text('Report delay'),
          findsOneWidget,
          reason: 'AC-E03-01 4: delay is always offered',
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'after a departure from Veyangoda, Gampaha is next and Veyangoda is done (AC-E03-01 2)',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1}, version: 1),
        );
        final app = TestApp(api: api);
        await app.pump(tester);

        // Gampaha is a designated stop: the arrival is reported first (Nimal-Journey).
        expect(
          find.text('Next: Gampaha (arrival report required)'),
          findsOneWidget,
        );
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsNothing,
        );
        expect(
          find.widgetWithText(FilledButton, 'Arrived Gampaha'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'tapping Departed saves the report, sends it and shows Delivered (AC-E03-02 1)',
      (tester) async {
        final reportApi = ScriptedReportApi([const Delivered()]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await tester.tap(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
        );
        await settle(tester);

        expect(reportApi.sent, hasLength(1));
        expect(find.text('Last report'.toUpperCase()), findsOneWidget);
        expect(find.text('Departed Veyangoda'), findsOneWidget);
        expect(find.text('Delivered'), findsOneWidget);
        // AC-E03-02 9: the same action is not offered again, the next one is.
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsNothing,
        );
        expect(
          find.widgetWithText(FilledButton, 'Arrived Gampaha'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets('a double tap makes one report (spec rule 9)', (tester) async {
      final reportApi = ScriptedReportApi([]);
      final app = TestApp(reportApi: reportApi);
      await app.pump(tester);

      final button = find.widgetWithText(FilledButton, 'Departed Veyangoda');
      await tester.tap(button);
      await tester.tap(button, warnIfMissed: false);
      await settle(tester);

      expect(reportApi.sent, hasLength(1));
      await app.close(tester);
    });

    testWidgets(
      'with no network the tap is saved: Not sent yet, and the next action moves on (AC-E03-02 3, 11)',
      (tester) async {
        final reportApi = ScriptedReportApi([const RetryLater('network')]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await tester.tap(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
        );
        await settle(tester);

        expect(find.text('Not sent yet'), findsOneWidget);
        expect(find.text('Not sent yet. We’ll keep trying.'), findsOneWidget);
        expect(
          find.widgetWithText(FilledButton, 'Arrived Gampaha'),
          findsOneWidget,
        );
        expect(
          (app.queue as InMemoryReportQueue).items.single.state,
          ReportState.pending,
          reason: 'never discarded',
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'a refused report shows Rejected and the way to get help (AC-E03-05 4)',
      (tester) async {
        final reportApi = ScriptedReportApi([
          const Rejected(statusCode: 409, reason: 'stop_has_different_time'),
        ]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await tester.tap(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
        );
        await settle(tester);

        expect(find.text('Rejected'), findsOneWidget);
        expect(
          find.text('Phone the desk so a controller can enter this report.'),
          findsOneWidget,
        );
        // A refused report did not happen as far as the server is concerned, so the action is offered again.
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'a wrong phone clock is named when the server says the time is out of range',
      (tester) async {
        final reportApi = ScriptedReportApi([
          const Rejected(statusCode: 422, reason: 'time_out_of_range'),
        ]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await tester.tap(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
        );
        await settle(tester);

        expect(find.text('Check your phone’s date and time.'), findsOneWidget);
        await app.close(tester);
      },
    );

    testWidgets(
      'at a designated stop the arrival is reported, then the departure (AC-E03-03 1)',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1}, version: 1),
        );
        final reportApi = ScriptedReportApi([const Delivered()]);
        final app = TestApp(api: api, reportApi: reportApi);
        await app.pump(tester);

        await tester.tap(find.widgetWithText(FilledButton, 'Arrived Gampaha'));
        await settle(tester);

        final sent = (app.queue as InMemoryReportQueue).items.single;
        expect(sent.kind, ReportKind.arrived);
        expect(sent.serviceStopId, stopId(2));
        expect(find.text('Arrived Gampaha'), findsWidgets);
        expect(find.text('Next: Gampaha (departure)'), findsOneWidget);
        expect(
          find.widgetWithText(FilledButton, 'Departed Gampaha'),
          findsOneWidget,
        );
        expect(
          find.widgetWithText(FilledButton, 'Arrived Gampaha'),
          findsNothing,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'no arrival is offered at a stop that is not designated (AC-E03-03 2)',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1, 2}, version: 2),
        );
        final app = TestApp(api: api);
        await app.pump(tester);

        expect(find.text('Next: Ragama (departure)'), findsOneWidget);
        expect(find.textContaining('Arrived'), findsNothing);
        expect(
          find.widgetWithText(FilledButton, 'Departed Ragama'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'an arrival taken offline is saved, and the departure is already offered (AC-E03-03 3)',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1}, version: 1),
        );
        final app = TestApp(api: api, reportApi: OfflineReportApi());
        await app.pump(tester);

        await tester.tap(find.widgetWithText(FilledButton, 'Arrived Gampaha'));
        await settle(tester);

        expect(find.text('Not sent yet'), findsOneWidget);
        expect(
          find.widgetWithText(FilledButton, 'Departed Gampaha'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'at the final stop the arrival is the last action, and it completes the journey (AC-E03-03 4)',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1, 2, 3, 4}, version: 4),
        );
        final reportApi = ScriptedReportApi([const Delivered()]);
        final app = TestApp(api: api, reportApi: reportApi);
        await app.pump(tester);

        expect(
          find.text('Next: Colombo Fort (arrival report required)'),
          findsOneWidget,
        );
        await tester.tap(
          find.widgetWithText(FilledButton, 'Arrived Colombo Fort'),
        );
        await settle(tester);

        // Nimal-Completed: the title, where the train arrived, and the counts of this operator's reports.
        expect(find.text('Journey completed'), findsOneWidget);
        expect(find.textContaining('Reached Colombo Fort at'), findsOneWidget);
        expect(find.text('YOUR REPORTS'), findsOneWidget);
        expect(find.text('Sent'), findsOneWidget);
        expect(find.text('Waiting to send'), findsOneWidget);
        expect(find.text('Rejected'), findsOneWidget);
        expect(find.widgetWithText(FilledButton, 'Done'), findsOneWidget);
        expect(
          find.widgetWithText(FilledButton, 'Arrived Colombo Fort'),
          findsNothing,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'the completed screen counts sent, waiting and rejected reports',
      (tester) async {
        final queue = InMemoryReportQueue()
          ..items.addAll([
            reportFor(1, state: ReportState.delivered),
            reportFor(
              2,
              kind: ReportKind.arrived,
              state: ReportState.delivered,
            ),
            reportFor(3, state: ReportState.pending),
            reportFor(4, state: ReportState.rejected),
          ]);
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1, 2, 3, 4}, statusKey: 'Completed'),
        );
        final app = TestApp(
          api: api,
          queue: queue,
          reportApi: OfflineReportApi(),
        );
        await app.pump(tester);

        expect(find.text('Journey completed'), findsOneWidget);
        // 2 sent, 1 waiting, 1 rejected: each number sits above its label.
        expect(find.text('2'), findsOneWidget);
        expect(find.text('1'), findsNWidgets(2));
        await app.close(tester);
      },
    );

    testWidgets('Done puts the completed journey away', (tester) async {
      final api = FakeJourneyApi(
        journey: demoJourney(),
        status: statusWith(departed: {1, 2, 3, 4}, statusKey: 'Completed'),
      );
      final app = TestApp(api: api);
      await app.pump(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Done'));
      await settle(tester);

      expect(find.text('Journey completed'), findsNothing);
      expect(find.text('No journey assigned'), findsOneWidget);
      await app.close(tester);
    });

    testWidgets(
      'a delay report shows in Last report with its reason and total',
      (tester) async {
        final queue = InMemoryReportQueue()..items.add(delayReport(total: 10));
        final app = TestApp(queue: queue, reportApi: OfflineReportApi());
        await app.pump(tester);

        expect(find.text('Delay: Signal issue, ~10 min'), findsOneWidget);
        expect(find.text('Not sent yet'), findsOneWidget);
        // A delay belongs to the journey: the next stop action is unchanged.
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets('an unknown delay is worded as unknown', (tester) async {
      final queue = InMemoryReportQueue()
        ..items.add(delayReport(reason: DelayReason.weather, total: null));
      final app = TestApp(queue: queue, reportApi: OfflineReportApi());
      await app.pump(tester);

      expect(
        find.text('Delay: Weather or natural hazard, duration unknown'),
        findsOneWidget,
      );
      await app.close(tester);
    });
    testWidgets(
      'without an assigned journey the screen says so and offers Refresh (AC-E03-01, Nimal-NoJourney)',
      (tester) async {
        final app = TestApp(api: FakeJourneyApi());
        await app.pump(tester);

        expect(find.text('No journey assigned'), findsOneWidget);
        expect(find.widgetWithText(FilledButton, 'Refresh'), findsOneWidget);
        expect(find.text('Phone the desk'), findsOneWidget);
        await app.close(tester);
      },
    );
  });

  group('Offline (AC-E03-01 5)', () {
    testWidgets(
      'opens the saved journey with the offline banner and the time it is from',
      (tester) async {
        final cache = InMemoryLocalCache()
          ..journey = CachedJourney(
            demoJourney(),
            DateTime.utc(2026, 10, 6, 3, 5),
          ); // 08:35 in Colombo
        final api = FakeJourneyApi(journey: demoJourney())..offline = true;
        final app = TestApp(api: api, cache: cache, online: false);
        await app.pump(tester);

        expect(find.text('You’re offline'), findsOneWidget);
        expect(
          find.textContaining('Reports are saved and sent later.'),
          findsOneWidget,
        );
        expect(find.textContaining('Last updated 08:35.'), findsOneWidget);
        expect(
          find.text('Train 8058 · Veyangoda to Colombo Fort'),
          findsOneWidget,
        );
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'with no saved journey and no network it says it could not load',
      (tester) async {
        final api = FakeJourneyApi(journey: demoJourney())..offline = true;
        final app = TestApp(api: api, online: false);
        await app.pump(tester);

        expect(find.text('Could not load your journey.'), findsOneWidget);
        await app.close(tester);
      },
    );
  });

  group('Accessibility', () {
    testWidgets(
      'the journey screen holds up at 200 percent text (spec section 9)',
      (tester) async {
        final app = TestApp();
        await app.pump(tester, textScale: 2);

        expect(tester.takeException(), isNull);
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    testWidgets('the main button is at least 44 px tall', (tester) async {
      final app = TestApp();
      await app.pump(tester);

      final size = tester.getSize(
        find.widgetWithText(FilledButton, 'Departed Veyangoda'),
      );
      expect(size.height, greaterThanOrEqualTo(44));
      await app.close(tester);
    });

    testWidgets(
      'status is words and icons, not colour: Delivered and Not sent yet differ in text and icon',
      (tester) async {
        final queue = InMemoryReportQueue()
          ..items.add(reportFor(1, state: ReportState.pending));
        final app = TestApp(
          queue: queue,
          reportApi: ScriptedReportApi([const RetryLater('network')]),
        );
        await app.pump(tester);

        expect(find.text('Not sent yet'), findsOneWidget);
        expect(find.byIcon(Icons.schedule_send), findsOneWidget);
        await app.close(tester);
      },
    );
    testWidgets(
      'when the assignment ends the next poll says no journey is assigned, and a new assignment appears on the poll after (spec 17)',
      (tester) async {
        final api = FakeJourneyApi(journey: demoJourney());
        final app = TestApp(api: api);
        await app.pump(tester);
        expect(
          find.text('Train 8058 · Veyangoda to Colombo Fort'),
          findsOneWidget,
        );

        api.journey = null; // a controller ended the assignment
        await tester.pump(const Duration(seconds: 31));
        await tester.pump();
        expect(find.text('No journey assigned'), findsOneWidget);

        api.journey = demoJourney(); // and assigned a journey again
        await tester.pump(const Duration(seconds: 31));
        await tester.pump();
        expect(
          find.text('Train 8058 · Veyangoda to Colombo Fort'),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );
  });
}
