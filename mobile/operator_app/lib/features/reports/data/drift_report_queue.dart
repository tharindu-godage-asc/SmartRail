import 'package:drift/drift.dart';

import '../domain/report.dart';
import '../domain/report_queue.dart';
import 'app_database.dart';

class DriftReportQueue implements ReportQueue {
  /// Not scoped to an operator: every row is visible (single-user use and the data tests).
  DriftReportQueue(this._db) : _scoped = false, _owner = '';

  /// Scoped to one operator (his Keycloak subject): he sees and sends only his own reports. With no one signed in ([owner] null)
  /// nothing is visible. Reports saved before ownership existed have an empty owner and stay hidden.
  DriftReportQueue.forOwner(this._db, String? owner)
    : _scoped = true,
      _owner = owner ?? '\u0000';

  final AppDatabase _db;
  final bool _scoped;
  final String _owner;

  Expression<bool> _mine(Reports r) => _scoped
      ? r.owner.equals(_owner) & r.owner.equals('').not()
      : const Constant(true);

  @override
  Future<QueuedReport> enqueue(QueuedReport report) async {
    if (!report.isDelay) {
      final existing =
          await (_db.select(_db.reports)..where(
                (r) =>
                    // Stop ids are the same every day, so the journey must match too.
                    _mine(r) &
                    r.journeyId.equals(report.journeyId) &
                    r.serviceStopId.equals(report.serviceStopId!) &
                    r.kind.equals(report.kind.index) &
                    r.state.isNotValue(ReportState.rejected.index),
              ))
              .getSingleOrNull();
      if (existing != null) {
        return _toReport(existing);
      }
    }
    await _db
        .into(_db.reports)
        .insert(_toRow(report).copyWith(owner: Value(_scoped ? _owner : '')));
    return report;
  }

  @override
  Future<DateTime?> latestOccurredAt(String journeyId) async {
    final latest = _db.reports.occurredAt.max();
    final query = _db.selectOnly(_db.reports)
      ..addColumns([latest])
      ..where(_mine(_db.reports) & _db.reports.journeyId.equals(journeyId));
    return (await query.map((row) => row.read(latest)).getSingle())?.toUtc();
  }

  @override
  Future<List<QueuedReport>> pending() async {
    final rows =
        await (_db.select(_db.reports)
              ..where(
                (r) => _mine(r) & r.state.equals(ReportState.pending.index),
              )
              ..orderBy([(r) => OrderingTerm.asc(r.occurredAt)]))
            .get();
    return rows.map(_toReport).toList();
  }

  @override
  Future<void> update(QueuedReport report) =>
      (_db.update(_db.reports)
            ..where((r) => r.clientEventId.equals(report.clientEventId)))
          .write(_toRow(report)); // the owner is not touched: the companion leaves it out

  @override
  Stream<List<QueuedReport>> watch(String journeyId) {
    final query = _db.select(_db.reports)
      ..where((r) => _mine(r) & r.journeyId.equals(journeyId))
      ..orderBy([(r) => OrderingTerm.desc(r.occurredAt)]);
    return query.watch().map((rows) => rows.map(_toReport).toList());
  }

  @override
  Stream<List<QueuedReport>> watchAll() {
    final query = _db.select(_db.reports)
      ..where(_mine)
      ..orderBy([(r) => OrderingTerm.desc(r.occurredAt)]);
    return query.watch().map((rows) => rows.map(_toReport).toList());
  }

  @override
  Future<int> deleteDeliveredBefore(DateTime cutoff) =>
      (_db.delete(_db.reports)..where(
            (r) =>
                r.state.equals(ReportState.delivered.index) &
                r.occurredAt.isSmallerThanValue(cutoff),
          ))
          .go();

  @override
  Future<int> pendingCount() async {
    final count = _db.reports.clientEventId.count();
    final query = _db.selectOnly(_db.reports)
      ..addColumns([count])
      ..where(
        _mine(_db.reports) &
            _db.reports.state.equals(ReportState.pending.index),
      );
    return (await query.map((row) => row.read(count)).getSingle()) ?? 0;
  }

  static QueuedReport _toReport(Report row) => QueuedReport(
    clientEventId: row.clientEventId,
    journeyId: row.journeyId,
    kind: row.kind,
    serviceStopId: row.serviceStopId,
    stationName: row.stationName,
    occurredAt: row.occurredAt.toUtc(),
    delayReason: DelayReason.fromCode(row.delayReason),
    totalDelayMinutes: row.totalDelayMinutes,
    state: row.state,
    attempts: row.attempts,
    nextTryAt: row.nextTryAt?.toUtc(),
    lastReason: row.lastReason,
  );

  static ReportsCompanion _toRow(QueuedReport r) => ReportsCompanion.insert(
    clientEventId: r.clientEventId,
    journeyId: r.journeyId,
    kind: Value(r.kind),
    serviceStopId: Value(r.serviceStopId),
    stationName: Value(r.stationName),
    occurredAt: r.occurredAt,
    delayReason: Value(r.delayReason?.code),
    totalDelayMinutes: Value(r.totalDelayMinutes),
    state: r.state,
    attempts: Value(r.attempts),
    nextTryAt: Value(r.nextTryAt),
    lastReason: Value(r.lastReason),
  );
}
