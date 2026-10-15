import 'backoff.dart';
import 'report.dart';
import 'report_queue.dart';
import 'send_result.dart';

/// What a pass of the sender ended with.
sealed class PassOutcome {
  const PassOutcome();
}

/// Nothing left to send.
final class QueueEmpty extends PassOutcome {
  const QueueEmpty();
}

/// The oldest report is waiting for its next try. Look again at [at].
final class WaitUntil extends PassOutcome {
  const WaitUntil(this.at);
  final DateTime at;
}

/// The token was refused: stop sending and ask the operator to sign in. Nothing was lost.
final class NeedsSignIn extends PassOutcome {
  const NeedsSignIn();
}

/// Sends waiting reports one at a time, oldest occurrence first (AC-E03-05).
///
/// A report that fails for a network or server reason holds the ones behind it, so the server never sees a later
/// departure before an earlier one. A report the server refuses for good does not (spec rule 3): slice 1 stores
/// nothing for it, so no later report depends on it.
class ReportSender {
  ReportSender({
    required this.queue,
    required this.api,
    this.backoff = const Backoff(),
    this.random,
  });

  final ReportQueue queue;
  final ReportApi api;
  final Backoff backoff;

  /// A fixed number from 0 to 1 for tests; null means truly random.
  final double Function()? random;

  Future<PassOutcome> pump(DateTime now) async {
    while (true) {
      final waiting = await queue.pending();
      if (waiting.isEmpty) {
        return const QueueEmpty();
      }

      final head = waiting.first;
      final due = head.nextTryAt;
      if (due != null && due.isAfter(now)) {
        return WaitUntil(due);
      }

      final result = await api.send(head);
      switch (result) {
        case Delivered():
          await queue.update(head.copyWith(state: ReportState.delivered));
        case Rejected(:final reason):
          await queue.update(
            head.copyWith(state: ReportState.rejected, lastReason: reason),
          );
        case RetryLater(:final reason):
          final attempts = head.attempts + 1;
          final next = now.add(backoff.after(attempts, random: random?.call()));
          await queue.update(
            head.copyWith(
              attempts: attempts,
              nextTryAt: next,
              lastReason: reason,
            ),
          );
          return WaitUntil(next);
        case Unauthorized():
          return const NeedsSignIn();
      }
    }
  }
}
