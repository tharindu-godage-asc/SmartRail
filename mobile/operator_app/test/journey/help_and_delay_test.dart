import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/core/strings.dart';
import 'package:operator_app/features/journey/presentation/delay_screens.dart';
import 'package:operator_app/features/reports/domain/report.dart';
import 'package:operator_app/features/reports/domain/send_result.dart';

import '../support/fakes.dart';
import '../support/harness.dart';

void main() {
  group(
    'Help tab and sign out (Nimal-PhoneFallback, Nimal-SignOut-Confirm)',
    () {
      testWidgets('shows the desk phone and the account', (tester) async {
        final app = TestApp();
        await app.pump(tester);

        await tester.tap(find.text('Help'));
        await settle(tester);

        expect(find.text('Help: can’t send reports?'), findsOneWidget);
        expect(find.text('+94 11 000 0000'), findsOneWidget);
        expect(find.widgetWithText(OutlinedButton, 'Sign out'), findsOneWidget);
        await app.close(tester);
      });

      testWidgets(
        'sign out says how many reports are waiting, and keeps them',
        (tester) async {
          final queue = InMemoryReportQueue()
            ..items.addAll([reportFor(1), reportFor(2)]);
          final app = TestApp(queue: queue, reportApi: OfflineReportApi());
          await app.pump(tester);

          await tester.tap(find.text('Help'));
          await settle(tester);
          await tester.tap(find.widgetWithText(OutlinedButton, 'Sign out'));
          await settle(tester);

          expect(find.text('Sign out?'), findsOneWidget);
          expect(
            find.text('2 reports are still waiting to send from this phone.'),
            findsOneWidget,
          );
          expect(find.text('Not sent yet'), findsOneWidget);

          await tester.tap(find.widgetWithText(FilledButton, 'Sign out'));
          await settle(tester);

          expect(find.text('Start your shift'), findsOneWidget);
          expect(app.store.tokens, isNull, reason: 'the session is over');
          expect(
            queue.items,
            hasLength(2),
            reason: 'waiting reports stay on the phone',
          );
          expect(
            (app.cache as InMemoryLocalCache).journey,
            isNull,
            reason: 'the next operator must not see this journey',
          );
          await app.close(tester);
        },
      );

      testWidgets('Stay signed in changes nothing', (tester) async {
        final app = TestApp();
        await app.pump(tester);

        await tester.tap(find.text('Help'));
        await settle(tester);
        await tester.tap(find.widgetWithText(OutlinedButton, 'Sign out'));
        await settle(tester);
        expect(
          find.text('No reports are waiting on this phone.'),
          findsOneWidget,
        );
        await tester.tap(find.widgetWithText(OutlinedButton, 'Stay signed in'));
        await settle(tester);

        expect(app.store.tokens, isNotNull);
        expect(find.text('Sign out?'), findsNothing);
        await app.close(tester);
      });
    },
  );

  group('Delay screens (AC-E03-04)', () {
    testWidgets(
      'the reason list leads to the estimate step, with Send report ready',
      (tester) async {
        final app = TestApp();
        await app.pump(tester);

        await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
        await settle(tester);
        expect(find.text('What is the reason?'), findsOneWidget);
        expect(find.textContaining('Step 1 of 2'), findsOneWidget);
        for (final reason in delayReasons) {
          expect(find.text(reason), findsOneWidget);
        }

        await tester.tap(find.text('Technical fault'));
        await tester.tap(find.widgetWithText(FilledButton, 'Next'));
        await settle(tester);

        expect(
          find.text('How late will the train be in total?'),
          findsOneWidget,
        );
        expect(find.textContaining('Step 2 of 2'), findsOneWidget);
        final send = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, 'Send report'),
        );
        expect(send.onPressed, isNotNull);
        await app.close(tester);
      },
    );

    testWidgets(
      'only totals above the running delay are offered, plus Unknown (AC-E03-04 6)',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1}, delay: 8, version: 1),
        );
        final app = TestApp(api: api);
        await app.pump(tester);

        await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
        await settle(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Next'));
        await settle(tester);

        expect(find.text('Running +8 min late now'), findsOneWidget);
        for (final option in [
          '+10 min',
          '+15 min',
          '+20 min',
          '+30 min',
          '+45 min',
          '+1 hour',
          'Unknown',
        ]) {
          expect(find.text(option), findsOneWidget, reason: option);
        }
        await app.close(tester);
      },
    );

    testWidgets(
      'with +25 running only +30, +45, +1 hour and Unknown are offered',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1}, delay: 25, version: 1),
        );
        final app = TestApp(api: api);
        await app.pump(tester);

        await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
        await settle(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Next'));
        await settle(tester);

        for (final gone in ['+10 min', '+15 min', '+20 min']) {
          expect(find.text(gone), findsNothing, reason: gone);
        }
        for (final option in ['+30 min', '+45 min', '+1 hour', 'Unknown']) {
          expect(find.text(option), findsOneWidget, reason: option);
        }
        await app.close(tester);
      },
    );

    testWidgets('after an unknown delay the screen does not claim a number', (
      tester,
    ) async {
      final api = FakeJourneyApi(
        journey: demoJourney(),
        status: statusWith(
          departed: {1},
          delay: 8,
          delayKnown: false,
          version: 1,
        ),
      );
      final app = TestApp(api: api);
      await app.pump(tester);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
      await settle(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await settle(tester);

      expect(
        find.text('The length of the delay is unknown now'),
        findsOneWidget,
      );
      expect(find.text('Running +8 min late now'), findsNothing);
      await app.close(tester);
    });

    test('offeredDelays hides options at or below the current delay', () {
      expect(offeredDelays(0), [10, 15, 20, 30, 45, 60]);
      expect(offeredDelays(10), [15, 20, 30, 45, 60]);
      expect(offeredDelays(25), [30, 45, 60]);
      expect(offeredDelays(45), [60]);
      expect(offeredDelays(60), isEmpty, reason: 'the buttons stop at 1 hour');
      expect(offeredDelays(213), isEmpty);
    });

    testWidgets(
      'with a delay of an hour or more only Unknown is offered, as there is no longer total to choose',
      (tester) async {
        final api = FakeJourneyApi(
          journey: demoJourney(),
          status: statusWith(departed: {1}, delay: 213, version: 1),
        );
        final app = TestApp(api: api);
        await app.pump(tester);

        await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
        await settle(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Next'));
        await settle(tester);

        expect(find.text('Running +213 min late now'), findsOneWidget);
        expect(find.text('Unknown'), findsOneWidget);
        for (final gone in ['+10 min', '+45 min', '+1 hour']) {
          expect(find.text(gone), findsNothing, reason: gone);
        }
        await app.close(tester);
      },
    );

    test(
      '60 minutes is worded as 1 hour everywhere, other lengths in minutes',
      () {
        expect(Strings.plusMinutes(60), '+1 hour');
        expect(Strings.plusMinutes(45), '+45 min');
        expect(Strings.aboutLate(60), 'about 1 hour late in total');
        expect(Strings.aboutLate(30), 'about 30 minutes late in total');
        expect(Strings.delayTotalShort(60), '~1 hour');
        expect(Strings.delayTotalShort(10), '~10 min');
        expect(Strings.delayShownKnown(60), 'Delayed (~1 hour)');
        expect(Strings.delayShownKnown(15), 'Delayed (~15 min)');
      },
    );

    testWidgets(
      'choosing +1 hour sends a total of 60 and the confirmation says 1 hour',
      (tester) async {
        final reportApi = ScriptedReportApi([const Delivered()]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
        await settle(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Next'));
        await settle(tester);
        await tester.tap(find.text('+1 hour'));
        await tester.pump();
        await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
        await settle(tester);

        expect(
          (app.queue as InMemoryReportQueue).items.single.totalDelayMinutes,
          60,
        );
        expect(
          find.text('Signal issue · about 1 hour late in total'),
          findsOneWidget,
        );
        expect(
          find.text(
            'Passengers now see Delayed (~1 hour). The reason appears after Operations verifies it.',
          ),
          findsOneWidget,
        );
        await app.close(tester);
      },
    );

    Future<void> openEstimate(
      WidgetTester tester, {
      String reason = 'Signal issue',
    }) async {
      await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
      await settle(tester);
      await tester.tap(find.text(reason));
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await settle(tester);
    }

    testWidgets(
      'Send report saves a delay with the chosen reason and total, sends it, and says it is delivered (Nimal-DelaySent)',
      (tester) async {
        final reportApi = ScriptedReportApi([const Delivered()]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await openEstimate(tester);
        await tester.tap(find.text('+15 min'));
        await tester.pump();
        await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
        await settle(tester);

        final saved = (app.queue as InMemoryReportQueue).items.single;
        expect(saved.kind, ReportKind.delay);
        expect(saved.delayReason, DelayReason.signalIssue);
        expect(saved.totalDelayMinutes, 15);
        expect(
          saved.serviceStopId,
          isNull,
          reason: 'a delay belongs to the journey',
        );
        expect(saved.state, ReportState.delivered);

        expect(find.text('Delay reported'), findsOneWidget);
        expect(find.text('Delivered'), findsOneWidget);
        expect(
          find.text('Signal issue · about 15 minutes late in total'),
          findsOneWidget,
        );
        expect(
          find.text(
            'Passengers now see Delayed (~15 min). The reason appears after Operations verifies it.',
          ),
          findsOneWidget,
        );
        expect(find.text('Phone the desk'), findsNothing);
        await app.close(tester);
      },
    );

    testWidgets(
      'the first option offered is already chosen, as in the design',
      (tester) async {
        final reportApi = ScriptedReportApi([const Delivered()]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await openEstimate(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
        await settle(tester);

        expect(
          (app.queue as InMemoryReportQueue).items.single.totalDelayMinutes,
          10,
        );
        await app.close(tester);
      },
    );

    testWidgets('Unknown sends a delay with no total (AC-E03-04 5)', (
      tester,
    ) async {
      final reportApi = ScriptedReportApi([const Delivered()]);
      final app = TestApp(reportApi: reportApi);
      await app.pump(tester);

      await openEstimate(tester, reason: 'Weather or natural hazard');
      await tester.tap(find.text('Unknown'));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
      await settle(tester);

      final saved = (app.queue as InMemoryReportQueue).items.single;
      expect(saved.delayReason, DelayReason.weather);
      expect(saved.totalDelayMinutes, isNull);
      expect(saved.isDelayUnknown, isTrue);
      expect(
        find.text('Weather or natural hazard · how late is not known yet'),
        findsOneWidget,
      );
      expect(find.textContaining('Delayed, duration unknown'), findsOneWidget);
      await app.close(tester);
    });

    testWidgets(
      'offline the delay is saved and the screen says so, with the desk phone (Nimal-DelayNotSent)',
      (tester) async {
        final app = TestApp(reportApi: OfflineReportApi());
        await app.pump(tester);

        await openEstimate(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
        await settle(tester);

        expect(find.text('Delay saved on this phone'), findsOneWidget);
        expect(find.text('Not sent yet'), findsOneWidget);
        expect(find.text('1 report waiting to send'), findsOneWidget);
        expect(find.textContaining('phone your line desk'), findsOneWidget);
        expect(find.text('Phone the desk'), findsOneWidget);
        expect(
          (app.queue as InMemoryReportQueue).items.single.state,
          ReportState.pending,
        );
        await app.close(tester);
      },
    );

    testWidgets(
      'a delay the server refuses is shown as not accepted, never as sent',
      (tester) async {
        final reportApi = ScriptedReportApi([
          const Rejected(statusCode: 409, reason: 'delay_not_greater'),
        ]);
        final app = TestApp(reportApi: reportApi);
        await app.pump(tester);

        await openEstimate(tester);
        await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
        await settle(tester);

        expect(find.text('Delay not accepted'), findsOneWidget);
        expect(
          find.text('Rejected, requires Operations review'),
          findsOneWidget,
        );
        expect(find.text('Delay reported'), findsNothing);
        await app.close(tester);
      },
    );

    testWidgets('Back to journey leaves the delay screens', (tester) async {
      final app = TestApp(reportApi: ScriptedReportApi([const Delivered()]));
      await app.pump(tester);

      await openEstimate(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
      await settle(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Back to journey'));
      await settle(tester);

      expect(find.text('Delay reported'), findsNothing);
      expect(
        find.text('Train 8058 · Veyangoda to Colombo Fort'),
        findsOneWidget,
      );
      await app.close(tester);
    });

    testWidgets('a double tap on Send report makes one delay', (tester) async {
      final reportApi = ScriptedReportApi([]);
      final app = TestApp(reportApi: reportApi);
      await app.pump(tester);

      await openEstimate(tester);
      final send = find.widgetWithText(FilledButton, 'Send report');
      await tester.tap(send);
      await tester.tap(send, warnIfMissed: false);
      await settle(tester);

      expect((app.queue as InMemoryReportQueue).items, hasLength(1));
      await app.close(tester);
    });
    testWidgets('Cancel returns to the journey', (tester) async {
      final app = TestApp();
      await app.pump(tester);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
      await settle(tester);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel'));
      await settle(tester);

      expect(find.text('Next: Veyangoda (departure)'), findsOneWidget);
      await app.close(tester);
    });
  });

  testWidgets(
    'a pending report is shown in the sign-out count even with the Journey tab hidden',
    (tester) async {
      final queue = InMemoryReportQueue()
        ..items.add(reportFor(1, state: ReportState.pending));
      final app = TestApp(queue: queue, reportApi: OfflineReportApi());
      await app.pump(tester);
      await tester.tap(find.text('Help'));
      await settle(tester);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Sign out'));
      await settle(tester);

      expect(
        find.text('1 report is still waiting to send from this phone.'),
        findsOneWidget,
      );
      await app.close(tester);
    },
  );
}
