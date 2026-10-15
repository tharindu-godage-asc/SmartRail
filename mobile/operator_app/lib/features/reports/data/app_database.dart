import 'package:drift/drift.dart';

import '../domain/report.dart';

part 'app_database.g.dart';

/// The report queue on the phone. Rows stay after delivery so the "Last report" card still has something to show.
class Reports extends Table {
  TextColumn get clientEventId => text()();
  TextColumn get journeyId => text()();
  TextColumn get serviceStopId => text()();
  TextColumn get stationName => text()();
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
  int get schemaVersion => 1;
}
