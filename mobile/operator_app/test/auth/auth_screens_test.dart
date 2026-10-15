import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/reports/domain/report.dart';

import '../support/fakes.dart';
import '../support/harness.dart';

void main() {
  testWidgets(
    'first start: the sign-in screen with one Sign in button and the two help lines (Nimal-Login)',
    (tester) async {
      final app = TestApp(store: FakeTokenStore());
      await app.pump(tester);

      expect(find.text('Start your shift'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
      expect(
        find.textContaining('Signing in needs a connection'),
        findsOneWidget,
      );
      expect(find.textContaining('phone the line desk'), findsOneWidget);
      expect(find.text('Train 8058 · Veyangoda to Colombo Fort'), findsNothing);
      await app.close(tester);
    },
  );

  testWidgets(
    'sign-in lost to process death asks the operator to try again, never a blank screen (spike finding 5)',
    (tester) async {
      final app = TestApp(store: FakeTokenStore()..signInStarted = true);
      await app.pump(tester);

      expect(find.text('Sign-in was interrupted'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
      await app.close(tester);
    },
  );

  testWidgets('a saved session goes straight to the journey', (tester) async {
    final app = TestApp();
    await app.pump(tester);

    expect(find.text('Start your shift'), findsNothing);
    expect(find.text('Next: Veyangoda (departure)'), findsOneWidget);
    await app.close(tester);
  });

  testWidgets(
    'the session ends while reports wait: they are listed and kept, and sign-in sends them (AC-E01-01 4)',
    (tester) async {
      final queue = InMemoryReportQueue()
        ..items.addAll([reportFor(1), reportFor(2)]);
      final api = FakeJourneyApi(journey: demoJourney())..sessionExpired = true;
      final app = TestApp(
        api: api,
        queue: queue,
        reportApi: OfflineReportApi(),
      );
      await app.pump(tester);

      expect(find.text('Your session has expired'), findsOneWidget);
      expect(find.text('WAITING ON THIS PHONE · 2'), findsOneWidget);
      expect(find.text('Departed Veyangoda'), findsOneWidget);
      expect(find.text('Departed Gampaha'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Sign in to send'),
        findsOneWidget,
      );
      expect(find.textContaining('Nothing is lost'), findsOneWidget);
      expect(queue.items.every((r) => r.state == ReportState.pending), isTrue);
      await app.close(tester);
    },
  );
}
