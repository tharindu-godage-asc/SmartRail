import 'dart:io';

import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/reports/data/app_database.dart';
import 'package:operator_app/features/reports/data/drift_report_queue.dart';
import 'package:operator_app/features/reports/domain/report.dart';
import 'package:operator_app/features/reports/presentation/reports_screen.dart';

import '../support/fakes.dart';
import '../support/harness.dart';

void main() {
  final now = DateTime.utc(2026, 10, 6, 4);

  group('Reports tab, My reports (spec 12, AC-E03-05)', () {
    Future<void> openReports(WidgetTester tester, TestApp app) async {
      await app.pump(tester);
      await tester.tap(find.text('Reports').last);
      await settle(tester);
    }

    testWidgets('the bottom bar has Journey, Reports and Help', (tester) async {
      final app = TestApp();
      await app.pump(tester);

      expect(find.text('Journey'), findsWidgets);
      expect(find.text('Reports'), findsOneWidget);
      expect(find.text('Help'), findsOneWidget);
      await app.close(tester);
    });

    testWidgets(
      'a delivered, a waiting and a rejected report each show their chip, newest first',
      (tester) async {
        final queue = InMemoryReportQueue()
          ..items.addAll([
            reportFor(
              1,
              state: ReportState.delivered,
              at: now.subtract(const Duration(minutes: 30)),
            ),
            reportFor(
              2,
              kind: ReportKind.arrived,
              state: ReportState.pending,
              at: now.subtract(const Duration(minutes: 20)),
            ),
            reportFor(
              3,
              state: ReportState.rejected,
              at: now.subtract(const Duration(minutes: 10)),
            ),
          ]);
        final app = TestApp(queue: queue, reportApi: OfflineReportApi());
        await openReports(tester, app);

        expect(find.text('Delivered'), findsOneWidget);
        expect(find.text('Not sent yet'), findsOneWidget);
        expect(find.text('Rejected'), findsOneWidget);
        // Newest first: the rejected one (09:20 local) is above the delivered one (08:50).
        final rejectedY = tester.getTopLeft(find.text('Rejected')).dy;
        final deliveredY = tester.getTopLeft(find.text('Delivered')).dy;
        expect(rejectedY, lessThan(deliveredY));
        await app.close(tester);
      },
    );

    testWidgets('a waiting report turns Delivered without leaving the screen', (
      tester,
    ) async {
      final queue = InMemoryReportQueue()
        ..items.add(reportFor(1, state: ReportState.pending, at: now));
      final app = TestApp(queue: queue, reportApi: OfflineReportApi());
      await openReports(tester, app);
      expect(find.text('Not sent yet'), findsOneWidget);

      await queue.update(
        queue.items.first.copyWith(state: ReportState.delivered),
      );
      await settle(tester);

      expect(find.text('Not sent yet'), findsNothing);
      expect(find.text('Delivered'), findsOneWidget);
      await app.close(tester);
    });

    testWidgets(
      'the rejected detail never says a controller will review it, and offers the desk phone',
      (tester) async {
        final queue = InMemoryReportQueue()
          ..items.add(reportFor(1, state: ReportState.rejected, at: now));
        final app = TestApp(queue: queue, reportApi: OfflineReportApi());
        await openReports(tester, app);

        await tester.tap(find.text('Departed Veyangoda').first);
        await settle(tester);

        expect(find.text('Not accepted'), findsWidgets);
        expect(
          find.textContaining('conflicts with what is already recorded'),
          findsOneWidget,
        );
        expect(find.textContaining('will review'), findsNothing);
        expect(find.textContaining('requires Operations review'), findsNothing);
        expect(find.text('Phone the desk'), findsOneWidget);

        await tester.tap(find.widgetWithText(FilledButton, 'Back to reports'));
        await settle(tester);
        expect(find.text('Tap a report to see details.'), findsOneWidget);
        await app.close(tester);
      },
    );

    testWidgets('with no reports the tab says so', (tester) async {
      final app = TestApp();
      await openReports(tester, app);

      expect(find.text('No reports on this phone yet.'), findsOneWidget);
      await app.close(tester);
    });

    testWidgets('a delivered report has a detail too, and no desk button', (
      tester,
    ) async {
      final queue = InMemoryReportQueue()
        ..items.add(reportFor(1, state: ReportState.delivered, at: now));
      final app = TestApp(queue: queue);
      await openReports(tester, app);

      await tester.tap(find.text('Departed Veyangoda').first);
      await settle(tester);

      expect(find.text('The server has this report.'), findsOneWidget);
      expect(find.text('Phone the desk'), findsNothing);
      await app.close(tester);
    });
  });

  group('keeping reports on the phone (FR-31, spec 12 rule 7)', () {
    test(
      'delivered reports older than 7 days go; waiting and rejected ones stay',
      () async {
        final old = now.subtract(const Duration(days: 8));
        final recent = now.subtract(const Duration(days: 6));
        final queue = InMemoryReportQueue()
          ..items.addAll([
            reportFor(1, state: ReportState.delivered, at: old),
            reportFor(2, state: ReportState.pending, at: old),
            reportFor(3, state: ReportState.rejected, at: old),
            reportFor(4, state: ReportState.delivered, at: recent),
          ]);

        final removed = await purgeOldDelivered(queue, now);

        expect(removed, 1);
        expect(
          queue.items.map((r) => r.clientEventId),
          unorderedEquals(['evt-2', 'evt-3', 'evt-4']),
        );
      },
    );

    test('the same rule holds in the real database', () async {
      final dir = Directory.systemTemp.createTempSync('reports_tab_test');
      addTearDown(() => dir.deleteSync(recursive: true));
      final db = AppDatabase(NativeDatabase(File('${dir.path}/q.sqlite')));
      addTearDown(db.close);
      final queue = DriftReportQueue(db);
      final old = now.subtract(const Duration(days: 8));
      await queue.enqueue(reportFor(1, state: ReportState.delivered, at: old));
      await queue.enqueue(reportFor(2, state: ReportState.pending, at: old));
      await queue.enqueue(reportFor(3, state: ReportState.rejected, at: old));

      final removed = await purgeOldDelivered(queue, now);

      expect(removed, 1);
      final left = await queue.watchAll().first;
      expect(
        left.map((r) => r.clientEventId),
        unorderedEquals(['evt-2', 'evt-3']),
      );
    });
  });
}
