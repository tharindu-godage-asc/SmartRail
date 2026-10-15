import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/core/strings.dart';
import 'package:operator_app/features/journey/presentation/delay_screens.dart';
import 'package:operator_app/features/reports/domain/report.dart';

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

  group('Delay screens (UI only, sending is not built)', () {
    testWidgets(
      'the reason list leads to the estimate step, whose Send report is off',
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
        expect(
          send.onPressed,
          isNull,
          reason: 'AC-E03-04 needs a backend slice first',
        );
        expect(find.text(Strings.delayUnavailable), findsOneWidget);
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

        expect(find.text('+10 min'), findsOneWidget);
        expect(find.text('+15 min'), findsOneWidget);
        expect(find.text('+20 min'), findsOneWidget);
        expect(find.text('Unknown'), findsOneWidget);
        await app.close(tester);
      },
    );

    test('offeredDelays hides options at or below the current delay', () {
      expect(offeredDelays(0), [10, 15, 20]);
      expect(offeredDelays(10), [15, 20]);
      expect(offeredDelays(20), isEmpty);
    });

    testWidgets('Cancel returns to the journey', (tester) async {
      final app = TestApp();
      await app.pump(tester);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Report delay'));
      await settle(tester);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel'));
      await settle(tester);

      expect(find.text('Next: Veyangoda departure'), findsOneWidget);
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
