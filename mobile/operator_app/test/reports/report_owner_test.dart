import 'dart:io';

import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/auth/domain/auth.dart';
import 'package:operator_app/features/reports/data/app_database.dart';
import 'package:operator_app/features/reports/data/drift_report_queue.dart';
import 'package:sqlite3/sqlite3.dart' as sqlite;

import '../support/fakes.dart';

/// Spec 18: on a shared phone each operator sees and sends only his own reports.
void main() {
  late Directory dir;
  late File file;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('owner_test');
    file = File('${dir.path}/queue.sqlite');
  });

  tearDown(() => dir.deleteSync(recursive: true));

  AppDatabase open() => AppDatabase(NativeDatabase(file));

  test('an operator sees only the reports he made, in every read', () async {
    final db = open();
    final nimal = DriftReportQueue.forOwner(db, 'nimal');
    final john = DriftReportQueue.forOwner(db, 'john');
    await nimal.enqueue(reportFor(1));
    await nimal.enqueue(delayReport(id: 'nimal-delay'));

    expect((await nimal.pending()), hasLength(2));
    expect(await nimal.pendingCount(), 2);
    expect(await nimal.watchAll().first, hasLength(2));
    expect(await john.pending(), isEmpty);
    expect(await john.pendingCount(), 0);
    expect(await john.watchAll().first, isEmpty);
    expect(await john.watch('journey-1').first, isEmpty);
    expect(await john.latestOccurredAt('journey-1'), isNull);
    await db.close();
  });

  test(
    'the same stop reported by two operators is two reports, not a duplicate',
    () async {
      final db = open();
      final nimal = DriftReportQueue.forOwner(db, 'nimal');
      final john = DriftReportQueue.forOwner(db, 'john');
      await nimal.enqueue(reportFor(1));

      final mine = await john.enqueue(reportFor(1, id: 'johns-own'));

      expect(mine.clientEventId, 'johns-own');
      expect(await john.pending(), hasLength(1));
      expect(await nimal.pending(), hasLength(1));
      await db.close();
    },
  );

  test('updating a report keeps its owner', () async {
    final db = open();
    final nimal = DriftReportQueue.forOwner(db, 'nimal');
    final saved = await nimal.enqueue(reportFor(1));

    await nimal.update(saved.copyWith(attempts: 4));

    expect((await nimal.pending()).single.attempts, 4);
    expect(await DriftReportQueue.forOwner(db, 'john').pending(), isEmpty);
    await db.close();
  });

  test('with no one signed in nothing is visible', () async {
    final db = open();
    await DriftReportQueue.forOwner(db, 'nimal').enqueue(reportFor(1));

    final nobody = DriftReportQueue.forOwner(db, null);

    expect(await nobody.pending(), isEmpty);
    expect(await nobody.watchAll().first, isEmpty);
    await db.close();
  });

  test('reports saved before ownership existed belong to no one and stay hidden, not deleted', () async {
    sqlite.sqlite3.open(file.path)
      ..execute('''
        CREATE TABLE reports (
          client_event_id TEXT NOT NULL PRIMARY KEY,
          journey_id TEXT NOT NULL,
          kind INTEGER NOT NULL DEFAULT 0,
          service_stop_id TEXT NULL,
          station_name TEXT NOT NULL DEFAULT '',
          delay_reason TEXT NULL,
          total_delay_minutes INTEGER NULL,
          occurred_at INTEGER NOT NULL,
          state INTEGER NOT NULL,
          attempts INTEGER NOT NULL DEFAULT 0,
          next_try_at INTEGER NULL,
          last_reason TEXT NULL
        );
      ''')
      ..execute(
        'CREATE TABLE key_values ("key" TEXT NOT NULL PRIMARY KEY, value TEXT NOT NULL);',
      )
      ..execute(
        "INSERT INTO reports (client_event_id, journey_id, service_stop_id, station_name, occurred_at, state) "
        "VALUES ('legacy', 'journey-1', 'stop-1', 'Veyangoda', 1791250800, 0)",
      )
      ..execute('PRAGMA user_version = 2;')
      ..close();

    final db = open();

    expect(await DriftReportQueue.forOwner(db, 'nimal').pending(), isEmpty);
    expect(
      (await DriftReportQueue(db).pending()).single.clientEventId,
      'legacy',
    ); // still on the phone
    await db.close();
  });

  test('the signed-in operator is the token subject', () {
    // header.payload.signature with {"sub":"abc-123"}
    const token = 'x.eyJzdWIiOiJhYmMtMTIzIn0.y';

    expect(userIdFromToken(token), 'abc-123');
    expect(userIdFromToken('not a token'), isNull);
  });
}
