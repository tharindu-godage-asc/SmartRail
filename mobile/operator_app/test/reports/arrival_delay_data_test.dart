import 'dart:io';

import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/journey/data/smartrail_api.dart';
import 'package:operator_app/features/journey/domain/models.dart';
import 'package:operator_app/features/reports/data/app_database.dart';
import 'package:operator_app/features/reports/data/drift_report_queue.dart';
import 'package:operator_app/features/reports/domain/report.dart';
import 'package:operator_app/features/reports/domain/report_sender.dart';
import 'package:operator_app/features/reports/domain/send_result.dart';
import 'package:sqlite3/sqlite3.dart' as sqlite;

import '../support/fakes.dart';

/// Spec 06, the operator app: arrival and delay reports on the phone (queue, migration, request bodies).
void main() {
  late Directory dir;
  late File file;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('arrival_delay_test');
    file = File('${dir.path}/queue.sqlite');
  });

  tearDown(() => dir.deleteSync(recursive: true));

  AppDatabase open() => AppDatabase(NativeDatabase(file));

  group('the database moves from version 1 to 2 without losing a report (AC-E03-05 6)', () {
    /// A database exactly as version 1 of the app made it: departures only, a stop on every row.
    void createVersion1WithAWaitingReport() {
      sqlite.sqlite3.open(file.path)
        ..execute('''
          CREATE TABLE reports (
            client_event_id TEXT NOT NULL PRIMARY KEY,
            journey_id TEXT NOT NULL,
            service_stop_id TEXT NOT NULL,
            station_name TEXT NOT NULL,
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
          "INSERT INTO reports (client_event_id, journey_id, service_stop_id, station_name, occurred_at, state, attempts, last_reason) "
          "VALUES ('old-1', 'journey-1', 'stop-1', 'Veyangoda', 1791250800, 0, 3, 'network')",
        )
        ..execute(
          "INSERT INTO key_values (\"key\", value) VALUES ('clock_offset_ms', '-180000')",
        )
        ..execute('PRAGMA user_version = 1;')
        ..close();
    }

    test('a departure saved by the old app is still there, still waiting, with its attempts', () async {
      createVersion1WithAWaitingReport();

      final db = open();
      final waiting = await DriftReportQueue(db).pending();

      expect(waiting, hasLength(1));
      final old = waiting.single;
      expect(old.clientEventId, 'old-1');
      expect(
        old.kind,
        ReportKind.departed,
        reason: 'every version 1 row was a departure',
      );
      expect(old.serviceStopId, 'stop-1');
      expect(old.stationName, 'Veyangoda');
      expect(old.state, ReportState.pending);
      expect(old.attempts, 3);
      expect(old.lastReason, 'network');
      expect(old.delayReason, isNull);
      expect(old.totalDelayMinutes, isNull);
      await db.close();
    });

    test('the table now takes a delay report with no stop', () async {
      createVersion1WithAWaitingReport();
      final db = open();
      final queue = DriftReportQueue(db);

      await queue.enqueue(
        delayReport(id: 'new-delay', at: DateTime.utc(2026, 10, 6, 3, 5)),
      );

      final all = await queue.pending();
      expect(
        all.map((r) => r.clientEventId),
        containsAll(['old-1', 'new-delay']),
      );
      expect(
        all.singleWhere((r) => r.clientEventId == 'new-delay').serviceStopId,
        isNull,
      );
      await db.close();
    });

    test('the other things the app remembers survive too', () async {
      createVersion1WithAWaitingReport();
      final db = open();

      final row = await (db.select(
        db.keyValues,
      )..where((r) => r.key.equals('clock_offset_ms'))).getSingle();

      expect(row.value, '-180000');
      await db.close();
    });

    test('a database that is already version 2 is left as it is', () async {
      var db = open();
      await DriftReportQueue(db)
          .enqueue(reportFor(1, kind: ReportKind.arrived));
      await db.close();

      db = open();
      final back = await DriftReportQueue(db).pending();

      expect(back.single.kind, ReportKind.arrived);
      await db.close();
    });
  });

  group('the queue keeps each kind of report (AC-E03-03, AC-E03-04)', () {
    test(
      'an arrival and a departure for the same stop are both kept',
      () async {
        final db = open();
        final queue = DriftReportQueue(db);

        final arrival = await queue.enqueue(
          reportFor(
            2,
            kind: ReportKind.arrived,
            id: 'arr',
            at: DateTime.utc(2026, 10, 6, 3, 10),
          ),
        );
        final departure = await queue.enqueue(
          reportFor(2, id: 'dep', at: DateTime.utc(2026, 10, 6, 3, 11)),
        );

        expect(arrival.clientEventId, 'arr');
        expect(departure.clientEventId, 'dep');
        expect(await queue.pendingCount(), 2);
        await db.close();
      },
    );

    test('a second arrival for the same stop returns the first, as a second departure does', () async {
      final db = open();
      final queue = DriftReportQueue(db);

      final first = await queue.enqueue(
        reportFor(2, kind: ReportKind.arrived, id: 'a'),
      );
      final again = await queue.enqueue(
        reportFor(2, kind: ReportKind.arrived, id: 'b'),
      );

      expect(again.clientEventId, first.clientEventId);
      expect(await queue.pendingCount(), 1);
      await db.close();
    });

    test('delay reports are always saved: the operator may update the delay as it changes', () async {
      final db = open();
      final queue = DriftReportQueue(db);

      await queue.enqueue(
        delayReport(id: 'd1', total: 10, at: DateTime.utc(2026, 10, 6, 3, 5)),
      );
      await queue.enqueue(
        delayReport(id: 'd2', total: 15, at: DateTime.utc(2026, 10, 6, 3, 9)),
      );
      await queue.enqueue(
        delayReport(
          id: 'd3',
          total: null,
          at: DateTime.utc(2026, 10, 6, 3, 12),
        ),
      );

      final all = await queue.pending();
      expect(all.map((r) => r.totalDelayMinutes), [10, 15, null]);
      expect(all.map((r) => r.isDelayUnknown), [false, false, true]);
      await db.close();
    });

    test(
      'a delay survives the app being closed with its reason and total',
      () async {
        var db = open();
        await DriftReportQueue(
          db,
        ).enqueue(delayReport(reason: DelayReason.technicalFault, total: 20));
        await db.close();

        db = open();
        final back = (await DriftReportQueue(db).pending()).single;

        expect(back.kind, ReportKind.delay);
        expect(back.delayReason, DelayReason.technicalFault);
        expect(back.totalDelayMinutes, 20);
        expect(back.serviceStopId, isNull);
        await db.close();
      },
    );

    test(
      'reports leave in the order they happened, whatever their kind',
      () async {
        final db = open();
        final queue = DriftReportQueue(db);
        await queue.enqueue(
          reportFor(2, id: 'dep', at: DateTime.utc(2026, 10, 6, 3, 11)),
        );
        await queue.enqueue(
          delayReport(id: 'delay', at: DateTime.utc(2026, 10, 6, 3, 9)),
        );
        await queue.enqueue(
          reportFor(
            2,
            kind: ReportKind.arrived,
            id: 'arr',
            at: DateTime.utc(2026, 10, 6, 3, 7),
          ),
        );

        expect((await queue.pending()).map((r) => r.clientEventId), [
          'arr',
          'delay',
          'dep',
        ]);
        await db.close();
      },
    );

    test('the latest time of a journey is known, so the next report can be stamped after it', () async {
      final db = open();
      final queue = DriftReportQueue(db);
      expect(await queue.latestOccurredAt(demoJourney().journeyId), isNull);

      await queue.enqueue(reportFor(1, at: DateTime.utc(2026, 10, 6, 3, 1)));
      await queue.enqueue(delayReport(at: DateTime.utc(2026, 10, 6, 3, 4)));
      await queue.enqueue(
        reportFor(
          2,
          kind: ReportKind.arrived,
          at: DateTime.utc(2026, 10, 6, 3, 2),
        ),
      );

      expect(
        await queue.latestOccurredAt(demoJourney().journeyId),
        DateTime.utc(2026, 10, 6, 3, 4),
      );
      expect(await queue.latestOccurredAt('another-journey'), isNull);
      await db.close();
    });
  });

  group('the request body for each kind (spec 06, section 4)', () {
    test('a departure carries its stop and nothing about delays', () {
      expect(
        SmartRailApi.requestBody(
          reportFor(1, at: DateTime.utc(2026, 10, 6, 2, 58)),
        ),
        {
          'clientEventId': 'evt-1',
          'eventType': 'Departed',
          'serviceStopId': stopId(1),
          'occurredAt': '2026-10-06T02:58:00.000Z',
        },
      );
    });

    test('an arrival is the same shape with the type Arrived', () {
      final body = SmartRailApi.requestBody(
        reportFor(2, kind: ReportKind.arrived),
      );

      expect(body['eventType'], 'Arrived');
      expect(body['serviceStopId'], stopId(2));
      for (final key in ['reason', 'totalDelayMinutes', 'delayUnknown']) {
        expect(body.keys, isNot(contains(key)), reason: key);
      }
    });

    test('a delay with a total has a reason and the total, and no stop', () {
      final body = SmartRailApi.requestBody(
        delayReport(reason: DelayReason.signalIssue, total: 10),
      );

      expect(body['eventType'], 'DelayReported');
      expect(body['reason'], 'SignalIssue');
      expect(body['totalDelayMinutes'], 10);
      expect(body.keys, isNot(contains('serviceStopId')));
      expect(body.keys, isNot(contains('delayUnknown')));
    });

    test('an unknown delay says delayUnknown and has no total', () {
      final body = SmartRailApi.requestBody(
        delayReport(reason: DelayReason.weather, total: null),
      );

      expect(body['reason'], 'Weather');
      expect(body['delayUnknown'], true);
      expect(body.keys, isNot(contains('totalDelayMinutes')));
      expect(body.keys, isNot(contains('serviceStopId')));
    });

    test('each reason is sent as the exact code the server accepts', () {
      expect(
        {for (final r in DelayReason.values) r.code},
        {'SignalIssue', 'TechnicalFault', 'Obstruction', 'Weather', 'Unknown'},
      );
      expect(DelayReason.fromCode('Weather'), DelayReason.weather);
      expect(DelayReason.fromCode('weather'), isNull);
      expect(DelayReason.fromCode(null), isNull);
    });
  });

  group('what the server says about the new kinds (spec 06 error codes)', () {
    test('an arrival at a stop that is not designated is refused for good', () {
      expect(
        SendResult.fromResponse(422, reason: 'not_arrival_station'),
        isA<Rejected>(),
      );
    });

    test(
      'a delay that is not greater than the one running is refused for good',
      () {
        final result = SendResult.fromResponse(
          409,
          reason: 'delay_not_greater',
        );
        expect(
          result,
          isA<Rejected>().having(
            (r) => r.reason,
            'reason',
            'delay_not_greater',
          ),
        );
      },
    );

    test('an order conflict is refused for good, and a version conflict is retried', () {
      expect(
        SendResult.fromResponse(409, reason: 'order_conflict'),
        isA<Rejected>(),
      );
      expect(
        SendResult.fromResponse(409, reason: 'version_conflict'),
        isA<RetryLater>(),
      );
    });

    test('a refused delay does not hold back the arrival behind it', () async {
      final db = open();
      final queue = DriftReportQueue(db);
      await queue.enqueue(
        delayReport(id: 'delay', at: DateTime.utc(2026, 10, 6, 3, 5)),
      );
      await queue.enqueue(
        reportFor(
          2,
          kind: ReportKind.arrived,
          id: 'arr',
          at: DateTime.utc(2026, 10, 6, 3, 8),
        ),
      );
      final api = ScriptedReportApi([
        const Rejected(statusCode: 409, reason: 'delay_not_greater'),
        const Delivered(),
      ]);

      final outcome = await ReportSender(
        queue: queue,
        api: api,
      ).pump(DateTime.utc(2026, 10, 6, 3, 10));

      expect(outcome, isA<QueueEmpty>());
      expect(api.sent, ['delay', 'arr']);
      final byId = {
        for (final r in await queue.watch(demoJourney().journeyId).first)
          r.clientEventId: r.state,
      };
      expect(byId, {
        'delay': ReportState.rejected,
        'arr': ReportState.delivered,
      });
      await db.close();
    });
  });

  group('what the app reads from the server (AC-E03-01 6, spec 06 section 4)', () {
    test('a stop knows whether an arrival can be reported there, and the old shape still parses', () {
      final withFlag = OperatorStop.fromJson({
        'serviceStopId': 's',
        'sequence': 2,
        'stationCode': 'GPH',
        'stationName': 'Gampaha',
        'isFinalStop': false,
        'isArrivalStation': true,
      });
      final oldShape = OperatorStop.fromJson({
        'serviceStopId': 's',
        'sequence': 2,
        'stationCode': 'GPH',
        'stationName': 'Gampaha',
        'isFinalStop': false,
      });

      expect(withFlag.isArrivalStation, isTrue);
      expect(oldShape.isArrivalStation, isFalse);
      expect(
        OperatorStop.fromJson(withFlag.toJson()).isArrivalStation,
        isTrue,
        reason: 'the cache keeps it',
      );
    });

    test(
      'a stop with only an arrival is Reported but has not left; Inferred has',
      () {
        StopProgress stop(
          String confirmation, {
          String? arrived,
          String? departed,
        }) => StopProgress.fromJson({
          'serviceStopId': 's',
          'sequence': 2,
          'confirmation': confirmation,
          'actualArrival': arrived,
          'actualDeparture': departed,
        });

        expect(
          stop('Reported', arrived: '2026-10-06T03:10:00+00:00').hasDeparted,
          isFalse,
        );
        expect(
          stop(
            'Reported',
            arrived: '2026-10-06T03:10:00+00:00',
            departed: '2026-10-06T03:11:00+00:00',
          ).hasDeparted,
          isTrue,
        );
        expect(stop('Inferred').hasDeparted, isTrue);
        expect(stop('Pending').hasDeparted, isFalse);
      },
    );

    test('the status carries Completed and whether the delay is known', () {
      final json = {
        'journeyId': 'j',
        'version': 3,
        'serverTime': '2026-10-06T03:00:00+00:00',
        'delayMinutes': 8,
        'stops': <Object>[],
      };

      final plain = TrainStatus.fromJson(json);
      final unknown = TrainStatus.fromJson({
        ...json,
        'delayKnown': false,
        'statusKey': 'Completed',
      });

      expect(
        plain.delayKnown,
        isTrue,
        reason: 'an older server does not send the field',
      );
      expect(plain.statusKey, '');
      expect(unknown.delayKnown, isFalse);
      expect(unknown.statusKey, 'Completed');
    });
  });
}
