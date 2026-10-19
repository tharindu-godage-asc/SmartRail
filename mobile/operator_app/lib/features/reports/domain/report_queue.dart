import 'report.dart';
import 'send_result.dart';

/// Reports waiting on this phone, and the ones already answered. It survives the app being closed (AC-E03-05 6).
abstract interface class ReportQueue {
  /// Saves a new report, unless a departure or arrival for the same stop and kind that is still pending or delivered
  /// exists: then that one is returned instead (spec rule 9, one per stop and kind). A rejected report does not block a
  /// new one. A delay report is always saved: the operator may update the delay as often as it changes.
  Future<QueuedReport> enqueue(QueuedReport report);

  /// When the latest report of the journey happened, whatever its state; null when there is none. A new report is stamped
  /// after it, because the server refuses two reports of one journey at the same moment.
  Future<DateTime?> latestOccurredAt(String journeyId);

  /// Reports still to send, oldest occurrence first (the order the server needs, FR-36).
  Future<List<QueuedReport>> pending();

  Future<void> update(QueuedReport report);

  /// Every report of one journey, newest occurrence first, whatever its state.
  Stream<List<QueuedReport>> watch(String journeyId);

  Future<int> pendingCount();

  /// Every report on this phone, newest occurrence first, whatever its journey or state (the Reports tab).
  Stream<List<QueuedReport>> watchAll();

  /// Removes delivered reports that happened before [cutoff] and returns how many. Waiting and rejected reports are never
  /// removed (FR-31).
  Future<int> deleteDeliveredBefore(DateTime cutoff);
}

/// Sends one report. Implementations never throw for network or server trouble: they answer with a [SendResult].
abstract interface class ReportApi {
  Future<SendResult> send(QueuedReport report);
}
