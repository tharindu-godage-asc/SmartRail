import 'dart:io';

import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/journey/data/drift_local_cache.dart';
import 'package:operator_app/features/reports/data/app_database.dart';
import 'package:operator_app/features/reports/data/drift_report_queue.dart';
import 'package:operator_app/features/reports/domain/report.dart';

import '../support/fakes.dart';

void main() {
  late Directory dir;
  late File file;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('queue_test');
    file = File('${dir.path}/queue.sqlite');
  });

  tearDown(() => dir.deleteSync(recursive: true));

  AppDatabase open() => AppDatabase(NativeDatabase(file));

  test(
    'the queue is intact after the app is closed and reopened (AC-E03-05 6)',
    () async {
      var db = open();
      var queue = DriftReportQueue(db);
      await queue.enqueue(reportFor(2));
      await queue.enqueue(
        reportFor(1, attempts: 3, nextTryAt: DateTime.utc(2026, 10, 6, 3)),
      );
      await db.close();

      db = open();
      queue = DriftReportQueue(db);
      final pending = await queue.pending();

      expect(pending.map((r) => r.clientEventId), [
        'evt-1',
        'evt-2',
      ], reason: 'oldest occurrence first');
      expect(pending.first.attempts, 3);
      expect(pending.first.nextTryAt, DateTime.utc(2026, 10, 6, 3));
      expect(pending.first.occurredAt.isUtc, isTrue);
      await db.close();
    },
  );

  test('one pending or delivered report per stop; a rejected one does not block a new report (spec rule 9)', () async {
    final db = open();
    final queue = DriftReportQueue(db);

    final first = await queue.enqueue(reportFor(1, id: 'a'));
    final again = await queue.enqueue(reportFor(1, id: 'b'));
    expect(
      again.clientEventId,
      first.clientEventId,
      reason: 'the same stop gives the same report back',
    );
    expect(await queue.pendingCount(), 1);

    await queue.update(
      first.copyWith(state: ReportState.rejected, lastReason: 'order_conflict'),
    );
    final retried = await queue.enqueue(reportFor(1, id: 'c'));
    expect(retried.clientEventId, 'c');
    expect(await queue.pendingCount(), 1);
    await db.close();
  });

  test('update changes state without losing the report; delivered reports leave the pending list', () async {
    final db = open();
    final queue = DriftReportQueue(db);
    final saved = await queue.enqueue(reportFor(1));

    await queue.update(saved.copyWith(state: ReportState.delivered));

    expect(await queue.pending(), isEmpty);
    final all = await queue.watch(saved.journeyId).first;
    expect(all.single.state, ReportState.delivered);
    await db.close();
  });

  test(
    'watch lists one journey newest first and ignores other journeys',
    () async {
      final db = open();
      final queue = DriftReportQueue(db);
      await queue.enqueue(reportFor(1));
      await queue.enqueue(reportFor(2));

      final shown = await queue.watch(demoJourney().journeyId).first;
      expect(shown.map((r) => r.clientEventId), ['evt-2', 'evt-1']);
      expect(await queue.watch('another-journey').first, isEmpty);
      await db.close();
    },
  );

  test('the journey and the clock offset are remembered between runs (AC-E03-01 5)', () async {
    var db = open();
    var cache = DriftLocalCache(db);
    await cache.writeJourney(
      demoJourney(version: 7),
      DateTime.utc(2026, 10, 6, 3, 5),
    );
    await cache.writeClockOffset(const Duration(minutes: -3));
    await db.close();

    db = open();
    cache = DriftLocalCache(db);
    final saved = await cache.readJourney();
    expect(saved?.journey.version, 7);
    expect(saved?.journey.stops.map((s) => s.stationName), stationNames);
    expect(saved?.fetchedAt, DateTime.utc(2026, 10, 6, 3, 5));
    expect(await cache.readClockOffset(), const Duration(minutes: -3));

    await cache.clearJourney();
    expect(await cache.readJourney(), isNull);
    await db.close();
  });
}
