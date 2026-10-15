/// Where a report is on its way to the server (AC-E03-05).
enum ReportState {
  /// Waiting to be sent, or being retried. Shown as "Not sent yet".
  pending,

  /// The server recorded it (201) or already had it (200). Shown as "Delivered".
  delivered,

  /// The server refused it for good (403, 404, 409, 422). Kept, never resent, shown as "Rejected".
  rejected,
}

/// What the operator reported. The order of the enum is stored in the database, so only ever add to the end.
enum ReportKind {
  departed,
  arrived,

  /// "The train is late": a reason and a total delay, or "unknown". It belongs to the journey, not to a stop.
  delay,
}

/// Why the train is late (AC-E03-04). [code] is the exact text the server accepts.
enum DelayReason {
  signalIssue('SignalIssue'),
  technicalFault('TechnicalFault'),
  obstruction('Obstruction'),
  weather('Weather'),
  unknown('Unknown');

  const DelayReason(this.code);

  final String code;

  static DelayReason? fromCode(String? code) {
    for (final reason in values) {
      if (reason.code == code) {
        return reason;
      }
    }
    return null;
  }
}

/// One report (a departure, an arrival or a delay), stored on the phone until the server has it. [clientEventId] never
/// changes, so every retry is a duplicate the server ignores (FR-35, D-35).
class QueuedReport {
  const QueuedReport({
    required this.clientEventId,
    required this.journeyId,
    required this.occurredAt,
    this.kind = ReportKind.departed,
    this.serviceStopId,
    this.stationName = '',
    this.delayReason,
    this.totalDelayMinutes,
    this.state = ReportState.pending,
    this.attempts = 0,
    this.nextTryAt,
    this.lastReason,
  });

  final String clientEventId;
  final String journeyId;
  final ReportKind kind;

  /// The stop of a departure or an arrival; null for a delay report.
  final String? serviceStopId;

  /// For the "Last report" card; the server only needs the stop ID.
  final String stationName;

  /// When it happened (server-corrected phone time, UTC), not when it was sent (FR-21, FR-24).
  final DateTime occurredAt;

  /// A delay report only: why, and the TOTAL delay in minutes against the timetable. A null total means "unknown".
  final DelayReason? delayReason;
  final int? totalDelayMinutes;
  final ReportState state;
  final int attempts;
  final DateTime? nextTryAt;

  /// The server's stable `reason` code for a rejection, or why the last attempt failed.
  final String? lastReason;

  bool get isDelay => kind == ReportKind.delay;

  bool get isDelayUnknown => isDelay && totalDelayMinutes == null;

  QueuedReport copyWith({
    ReportState? state,
    int? attempts,
    DateTime? nextTryAt,
    String? lastReason,
  }) => QueuedReport(
    clientEventId: clientEventId,
    journeyId: journeyId,
    kind: kind,
    serviceStopId: serviceStopId,
    stationName: stationName,
    occurredAt: occurredAt,
    delayReason: delayReason,
    totalDelayMinutes: totalDelayMinutes,
    state: state ?? this.state,
    attempts: attempts ?? this.attempts,
    nextTryAt: nextTryAt ?? this.nextTryAt,
    lastReason: lastReason ?? this.lastReason,
  );
}
