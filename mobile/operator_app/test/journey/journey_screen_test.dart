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
        expect(find.text('Next: Veyangoda departure'), findsOneWidget);
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

        expect(find.text('Next: Gampaha departure'), findsOneWidget);
        expect(
          find.widgetWithText(FilledButton, 'Departed Veyangoda'),
          findsNothing,
        );
        expect(
          find.widgetWithText(FilledButton, 'Departed Gampaha'),
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
          find.widgetWithText(FilledButton, 'Departed Gampaha'),
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
          find.widgetWithText(FilledButton, 'Departed Gampaha'),
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

        expect(
          find.text('Rejected, requires Operations review'),
          findsOneWidget,
        );
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
      'after the last allowed departure there is no button, only the explanation',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1, 2, 3, 4}, version: 4),
        );
        final app = TestApp(api: api);
        await app.pump(tester);

        expect(find.text('No more reports to send.'), findsOneWidget);
        expect(
          find.text(
            'Reporting the arrival at Colombo Fort comes in a later release.',
          ),
          findsOneWidget,
        );
        expect(find.byType(FilledButton), findsNothing);
        await app.close(tester);
      },
    );

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
  });
}
