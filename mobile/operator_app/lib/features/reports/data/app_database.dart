import 'package:drift/drift.dart';

import '../domain/report.dart';

part 'app_database.g.dart';

/// The report queue on the phone. Rows stay after delivery so the "Last report" card still has something to show.
class Reports extends Table {
  TextColumn get clientEventId => text()();
  TextColumn get journeyId => text()();

  /// What was reported. Version 2 (spec 06): arrivals and delays join departures.
  IntColumn get kind => intEnum<ReportKind>().withDefault(const Constant(0))();

  /// Null for a delay report, which belongs to the journey and not to a stop.
  TextColumn get serviceStopId => text().nullable()();
  TextColumn get stationName => text().withDefault(const Constant(''))();

  /// A delay report only: the reason code, and the total in minutes (null means "unknown").
  TextColumn get delayReason => text().nullable()();
  IntColumn get totalDelayMinutes => integer().nullable()();
  DateTimeColumn get occurredAt => dateTime()();
  IntColumn get state => intEnum<ReportState>()();
  IntColumn get attempts => integer().withDefault(const Constant(0))();
  DateTimeColumn get nextTryAt => dateTime().nullable()();
  TextColumn get lastReason => text().nullable()();

  @override
  Set<Column> get primaryKey => {clientEventId};
}

/// Small things to remember between runs: the last journey, the clock offset.
class KeyValues extends Table {
  TextColumn get key => text()();
  TextColumn get value => text()();

  @override
  Set<Column> get primaryKey => {key};
}

@DriftDatabase(tables: [Reports, KeyValues])
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.executor);

  @override
  int get schemaVersion => 2;

  @override
  MigrationStrategy get migration => MigrationStrategy(
    onUpgrade: (m, from, to) async {
      if (from < 2) {
        // Version 1 held only departures, with a stop on every row. Rebuild the table with the new shape and keep every
        // row: reports waiting to be sent must survive an app update (AC-E03-05 6).
        await m.alterTable(
          TableMigration(
            reports,
            newColumns: [
              reports.kind,
              reports.delayReason,
              reports.totalDelayMinutes,
            ],
          ),
        );
      }
    },
  );
}
