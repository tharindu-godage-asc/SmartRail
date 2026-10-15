import 'package:drift/drift.dart';

import '../domain/report.dart';
import '../domain/report_queue.dart';
import 'app_database.dart';

class DriftReportQueue implements ReportQueue {
  DriftReportQueue(this._db);

  final AppDatabase _db;

  @override
  Future<QueuedReport> enqueue(QueuedReport report) async {
    final existing =
        await (_db.select(_db.reports)..where(
              (r) =>
                  r.serviceStopId.equals(report.serviceStopId) &
                  r.state.isNotValue(ReportState.rejected.index),
            ))
            .getSingleOrNull();
    if (existing != null) {
      return _toReport(existing);
    }
    await _db.into(_db.reports).insert(_toRow(report));
    return report;
  }

  @override
  Future<List<QueuedReport>> pending() async {
    final rows =
        await (_db.select(_db.reports)
              ..where((r) => r.state.equals(ReportState.pending.index))
              ..orderBy([(r) => OrderingTerm.asc(r.occurredAt)]))
            .get();
    return rows.map(_toReport).toList();
  }

  @override
  Future<void> update(QueuedReport report) =>
      _db.update(_db.reports).replace(_toRow(report));

  @override
  Stream<List<QueuedReport>> watch(String journeyId) {
    final query = _db.select(_db.reports)
      ..where((r) => r.journeyId.equals(journeyId))
      ..orderBy([(r) => OrderingTerm.desc(r.occurredAt)]);
    return query.watch().map((rows) => rows.map(_toReport).toList());
  }

  @override
  Future<int> pendingCount() async {
    final count = _db.reports.clientEventId.count();
    final query = _db.selectOnly(_db.reports)
      ..addColumns([count])
      ..where(_db.reports.state.equals(ReportState.pending.index));
    return (await query.map((row) => row.read(count)).getSingle()) ?? 0;
  }

  static QueuedReport _toReport(Report row) => QueuedReport(
    clientEventId: row.clientEventId,
    journeyId: row.journeyId,
    serviceStopId: row.serviceStopId,
    stationName: row.stationName,
    occurredAt: row.occurredAt.toUtc(),
    state: row.state,
    attempts: row.attempts,
    nextTryAt: row.nextTryAt?.toUtc(),
    lastReason: row.lastReason,
  );

  static ReportsCompanion _toRow(QueuedReport r) => ReportsCompanion.insert(
    clientEventId: r.clientEventId,
    journeyId: r.journeyId,
    serviceStopId: r.serviceStopId,
    stationName: r.stationName,
    occurredAt: r.occurredAt,
    state: r.state,
    attempts: Value(r.attempts),
    nextTryAt: Value(r.nextTryAt),
    lastReason: Value(r.lastReason),
  );
}
