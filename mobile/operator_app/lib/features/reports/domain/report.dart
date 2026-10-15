/// Where a report is on its way to the server (AC-E03-05).
enum ReportState {
  /// Waiting to be sent, or being retried. Shown as "Not sent yet".
  pending,

  /// The server recorded it (201) or already had it (200). Shown as "Delivered".
  delivered,

  /// The server refused it for good (403, 404, 409, 422). Kept, never resent, shown as "Rejected".
  rejected,
}

/// One `Departed <stop>` tap, stored on the phone until the server has it. [clientEventId] never changes, so
/// every retry is a duplicate the server ignores (FR-35, D-35).
class QueuedReport {
  const QueuedReport({
    required this.clientEventId,
    required this.journeyId,
    required this.serviceStopId,
    required this.stationName,
    required this.occurredAt,
    this.state = ReportState.pending,
    this.attempts = 0,
    this.nextTryAt,
    this.lastReason,
  });

  final String clientEventId;
  final String journeyId;
  final String serviceStopId;

  /// For the "Last report" card; the server only needs the stop ID.
  final String stationName;

  /// When the departure happened (server-corrected phone time, UTC), not when it was sent (FR-21, FR-24).
  final DateTime occurredAt;
  final ReportState state;
  final int attempts;
  final DateTime? nextTryAt;

  /// The server's stable `reason` code for a rejection, or why the last attempt failed.
  final String? lastReason;

  QueuedReport copyWith({
    ReportState? state,
    int? attempts,
    DateTime? nextTryAt,
    String? lastReason,
  }) => QueuedReport(
    clientEventId: clientEventId,
    journeyId: journeyId,
    serviceStopId: serviceStopId,
    stationName: stationName,
    occurredAt: occurredAt,
    state: state ?? this.state,
    attempts: attempts ?? this.attempts,
    nextTryAt: nextTryAt ?? this.nextTryAt,
    lastReason: lastReason ?? this.lastReason,
  );
}
