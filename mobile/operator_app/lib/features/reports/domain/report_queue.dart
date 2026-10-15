import 'report.dart';
import 'send_result.dart';

/// Reports waiting on this phone, and the ones already answered. It survives the app being closed (AC-E03-05 6).
abstract interface class ReportQueue {
  /// Saves a new report, unless a report for the same stop that is still pending or delivered exists: then that one
  /// is returned instead (spec rule 9, one Departed per stop). A rejected report does not block a new one.
  Future<QueuedReport> enqueue(QueuedReport report);

  /// Reports still to send, oldest occurrence first (the order the server needs, FR-36).
  Future<List<QueuedReport>> pending();

  Future<void> update(QueuedReport report);

  /// Every report of one journey, newest occurrence first, whatever its state.
  Stream<List<QueuedReport>> watch(String journeyId);

  Future<int> pendingCount();
}

/// Sends one report. Implementations never throw for network or server trouble: they answer with a [SendResult].
abstract interface class ReportApi {
  Future<SendResult> send(QueuedReport report);
}
