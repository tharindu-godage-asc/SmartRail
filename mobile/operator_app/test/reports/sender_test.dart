import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/reports/domain/backoff.dart';
import 'package:operator_app/features/reports/domain/report.dart';
import 'package:operator_app/features/reports/domain/report_sender.dart';
import 'package:operator_app/features/reports/domain/send_result.dart';

import '../support/fakes.dart';

void main() {
  group('Backoff (AC-E03-05 5)', () {
    const backoff = Backoff();

    test('starts at 5 seconds and doubles up to the 5 minute cap', () {
      final waits = [
        for (var n = 1; n <= 8; n++) backoff.after(n, random: 0.5).inSeconds,
      ];
      expect(waits, [5, 10, 20, 40, 80, 160, 300, 300]);
    });

    test('jitter stays within 20 percent, and never past the cap', () {
      expect(backoff.after(1, random: 0).inMilliseconds, 4000);
      expect(backoff.after(1, random: 1).inMilliseconds, 6000);
      expect(backoff.after(7, random: 1).inSeconds, 300);
      expect(backoff.after(30, random: 1).inSeconds, 300);
    });

    test('random jitter differs between calls', () {
      final waits = {
        for (var i = 0; i < 20; i++) backoff.after(3).inMilliseconds,
      };
      expect(waits.length, greaterThan(1));
    });
  });

  group('SendResult.fromResponse (spec section 4)', () {
    test('201 and 200 are both Delivered', () {
      expect(SendResult.fromResponse(201), isA<Delivered>());
      expect(SendResult.fromResponse(200), isA<Delivered>());
    });

    test(
      '401 asks for sign-in',
      () => expect(SendResult.fromResponse(401), isA<Unauthorized>()),
    );

    test('a 409 version conflict is retried, other 409s are refused', () {
      expect(
        SendResult.fromResponse(409, reason: 'version_conflict'),
        isA<RetryLater>(),
      );
      expect(
        SendResult.fromResponse(409, reason: 'stop_has_different_time'),
        isA<Rejected>(),
      );
      expect(
        SendResult.fromResponse(409, reason: 'order_conflict'),
        isA<Rejected>(),
      );
    });

    test('403, 404 and 422 are refused with the reason kept', () {
      for (final status in [403, 404, 422]) {
        final result = SendResult.fromResponse(status, reason: 'x');
        expect(result, isA<Rejected>());
        expect((result as Rejected).reason, 'x');
      }
    });

    test('5xx, 408 and 429 are retried', () {
      for (final status in [500, 502, 503, 408, 429]) {
        expect(SendResult.fromResponse(status), isA<RetryLater>());
      }
    });
  });

  group('ReportSender', () {
    final now = DateTime.utc(2026, 10, 6, 3);

    test(
      'sends in occurrence order whatever order they were saved in',
      () async {
        final queue = InMemoryReportQueue()
          ..items.addAll([reportFor(2), reportFor(1)]);
        final api = ScriptedReportApi([]);

        final outcome = await ReportSender(queue: queue, api: api).pump(now);

        expect(outcome, isA<QueueEmpty>());
        expect(api.sent, ['evt-1', 'evt-2']);
        expect(
          queue.items.map((r) => r.state),
          everyElement(ReportState.delivered),
        );
      },
    );

    test('a retrying report holds the ones behind it and keeps its client event id (AC-E03-05 5)', () async {
      final queue = InMemoryReportQueue()
        ..items.addAll([reportFor(1), reportFor(2)]);
      final api = ScriptedReportApi([const RetryLater('server_error')]);
      final sender = ReportSender(queue: queue, api: api, random: () => 0.5);

      final outcome = await sender.pump(now);

      expect(
        outcome,
        isA<WaitUntil>().having(
          (o) => o.at,
          'at',
          now.add(const Duration(seconds: 5)),
        ),
      );
      expect(api.sent, [
        'evt-1',
      ], reason: 'the second report must not overtake the first');
      expect(
        queue.items.first.state,
        ReportState.pending,
        reason: 'never discarded',
      );
      expect(queue.items.first.attempts, 1);

      // Too early: nothing is sent.
      await sender.pump(now.add(const Duration(seconds: 4)));
      expect(api.sent, ['evt-1']);

      // Due: the same report goes again with the same id, then the next one.
      await sender.pump(now.add(const Duration(seconds: 5)));
      expect(api.sent, ['evt-1', 'evt-1', 'evt-2']);
    });

    test('a refused report is marked Rejected and does not hold the queue (spec rule 3)', () async {
      final queue = InMemoryReportQueue()
        ..items.addAll([reportFor(1), reportFor(2)]);
      final api = ScriptedReportApi([
        const Rejected(statusCode: 409, reason: 'stop_has_different_time'),
      ]);

      await ReportSender(queue: queue, api: api).pump(now);

      expect(queue.items[0].state, ReportState.rejected);
      expect(queue.items[0].lastReason, 'stop_has_different_time');
      expect(queue.items[1].state, ReportState.delivered);
    });

    test('an expired session pauses the queue without losing or counting anything (AC-E01-01 4)', () async {
      final queue = InMemoryReportQueue()
        ..items.addAll([reportFor(1), reportFor(2)]);
      final api = ScriptedReportApi([const Unauthorized()]);

      final outcome = await ReportSender(queue: queue, api: api).pump(now);

      expect(outcome, isA<NeedsSignIn>());
      expect(
        queue.items.every(
          (r) => r.state == ReportState.pending && r.attempts == 0,
        ),
        isTrue,
      );

      // After signing in again the same reports go out.
      await ReportSender(queue: queue, api: ScriptedReportApi([])).pump(now);
      expect(
        queue.items.every((r) => r.state == ReportState.delivered),
        isTrue,
      );
    });

    test('an empty queue is idle', () async {
      final outcome = await ReportSender(
        queue: InMemoryReportQueue(),
        api: ScriptedReportApi([]),
      ).pump(now);
      expect(outcome, isA<QueueEmpty>());
    });
  });
}
