// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'app_database.dart';

// ignore_for_file: type=lint
class $ReportsTable extends Reports with TableInfo<$ReportsTable, Report> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $ReportsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _clientEventIdMeta = const VerificationMeta(
    'clientEventId',
  );
  @override
  late final GeneratedColumn<String> clientEventId = GeneratedColumn<String>(
    'client_event_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _journeyIdMeta = const VerificationMeta(
    'journeyId',
  );
  @override
  late final GeneratedColumn<String> journeyId = GeneratedColumn<String>(
    'journey_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  @override
  late final GeneratedColumnWithTypeConverter<ReportKind, int> kind =
      GeneratedColumn<int>(
        'kind',
        aliasedName,
        false,
        type: DriftSqlType.int,
        requiredDuringInsert: false,
        defaultValue: const Constant(0),
      ).withConverter<ReportKind>($ReportsTable.$converterkind);
  static const VerificationMeta _serviceStopIdMeta = const VerificationMeta(
    'serviceStopId',
  );
  @override
  late final GeneratedColumn<String> serviceStopId = GeneratedColumn<String>(
    'service_stop_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _stationNameMeta = const VerificationMeta(
    'stationName',
  );
  @override
  late final GeneratedColumn<String> stationName = GeneratedColumn<String>(
    'station_name',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
    defaultValue: const Constant(''),
  );
  static const VerificationMeta _delayReasonMeta = const VerificationMeta(
    'delayReason',
  );
  @override
  late final GeneratedColumn<String> delayReason = GeneratedColumn<String>(
    'delay_reason',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _totalDelayMinutesMeta = const VerificationMeta(
    'totalDelayMinutes',
  );
  @override
  late final GeneratedColumn<int> totalDelayMinutes = GeneratedColumn<int>(
    'total_delay_minutes',
    aliasedName,
    true,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _occurredAtMeta = const VerificationMeta(
    'occurredAt',
  );
  @override
  late final GeneratedColumn<DateTime> occurredAt = GeneratedColumn<DateTime>(
    'occurred_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  @override
  late final GeneratedColumnWithTypeConverter<ReportState, int> state =
      GeneratedColumn<int>(
        'state',
        aliasedName,
        false,
        type: DriftSqlType.int,
        requiredDuringInsert: true,
      ).withConverter<ReportState>($ReportsTable.$converterstate);
  static const VerificationMeta _attemptsMeta = const VerificationMeta(
    'attempts',
  );
  @override
  late final GeneratedColumn<int> attempts = GeneratedColumn<int>(
    'attempts',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _nextTryAtMeta = const VerificationMeta(
    'nextTryAt',
  );
  @override
  late final GeneratedColumn<DateTime> nextTryAt = GeneratedColumn<DateTime>(
    'next_try_at',
    aliasedName,
    true,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _lastReasonMeta = const VerificationMeta(
    'lastReason',
  );
  @override
  late final GeneratedColumn<String> lastReason = GeneratedColumn<String>(
    'last_reason',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _ownerMeta = const VerificationMeta('owner');
  @override
  late final GeneratedColumn<String> owner = GeneratedColumn<String>(
    'owner',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
    defaultValue: const Constant(''),
  );
  @override
  List<GeneratedColumn> get $columns => [
    clientEventId,
    journeyId,
    kind,
    serviceStopId,
    stationName,
    delayReason,
    totalDelayMinutes,
    occurredAt,
    state,
    attempts,
    nextTryAt,
    lastReason,
    owner,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'reports';
  @override
  VerificationContext validateIntegrity(
    Insertable<Report> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('client_event_id')) {
      context.handle(
        _clientEventIdMeta,
        clientEventId.isAcceptableOrUnknown(
          data['client_event_id']!,
          _clientEventIdMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_clientEventIdMeta);
    }
    if (data.containsKey('journey_id')) {
      context.handle(
        _journeyIdMeta,
        journeyId.isAcceptableOrUnknown(data['journey_id']!, _journeyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_journeyIdMeta);
    }
    if (data.containsKey('service_stop_id')) {
      context.handle(
        _serviceStopIdMeta,
        serviceStopId.isAcceptableOrUnknown(
          data['service_stop_id']!,
          _serviceStopIdMeta,
        ),
      );
    }
    if (data.containsKey('station_name')) {
      context.handle(
        _stationNameMeta,
        stationName.isAcceptableOrUnknown(
          data['station_name']!,
          _stationNameMeta,
        ),
      );
    }
    if (data.containsKey('delay_reason')) {
      context.handle(
        _delayReasonMeta,
        delayReason.isAcceptableOrUnknown(
          data['delay_reason']!,
          _delayReasonMeta,
        ),
      );
    }
    if (data.containsKey('total_delay_minutes')) {
      context.handle(
        _totalDelayMinutesMeta,
        totalDelayMinutes.isAcceptableOrUnknown(
          data['total_delay_minutes']!,
          _totalDelayMinutesMeta,
        ),
      );
    }
    if (data.containsKey('occurred_at')) {
      context.handle(
        _occurredAtMeta,
        occurredAt.isAcceptableOrUnknown(data['occurred_at']!, _occurredAtMeta),
      );
    } else if (isInserting) {
      context.missing(_occurredAtMeta);
    }
    if (data.containsKey('attempts')) {
      context.handle(
        _attemptsMeta,
        attempts.isAcceptableOrUnknown(data['attempts']!, _attemptsMeta),
      );
    }
    if (data.containsKey('next_try_at')) {
      context.handle(
        _nextTryAtMeta,
        nextTryAt.isAcceptableOrUnknown(data['next_try_at']!, _nextTryAtMeta),
      );
    }
    if (data.containsKey('last_reason')) {
      context.handle(
        _lastReasonMeta,
        lastReason.isAcceptableOrUnknown(data['last_reason']!, _lastReasonMeta),
      );
    }
    if (data.containsKey('owner')) {
      context.handle(
        _ownerMeta,
        owner.isAcceptableOrUnknown(data['owner']!, _ownerMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {clientEventId};
  @override
  Report map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return Report(
      clientEventId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}client_event_id'],
      )!,
      journeyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}journey_id'],
      )!,
      kind: $ReportsTable.$converterkind.fromSql(
        attachedDatabase.typeMapping.read(
          DriftSqlType.int,
          data['${effectivePrefix}kind'],
        )!,
      ),
      serviceStopId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}service_stop_id'],
      ),
      stationName: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}station_name'],
      )!,
      delayReason: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}delay_reason'],
      ),
      totalDelayMinutes: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}total_delay_minutes'],
      ),
      occurredAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}occurred_at'],
      )!,
      state: $ReportsTable.$converterstate.fromSql(
        attachedDatabase.typeMapping.read(
          DriftSqlType.int,
          data['${effectivePrefix}state'],
        )!,
      ),
      attempts: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}attempts'],
      )!,
      nextTryAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}next_try_at'],
      ),
      lastReason: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}last_reason'],
      ),
      owner: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}owner'],
      )!,
    );
  }

  @override
  $ReportsTable createAlias(String alias) {
    return $ReportsTable(attachedDatabase, alias);
  }

  static JsonTypeConverter2<ReportKind, int, int> $converterkind =
      const EnumIndexConverter<ReportKind>(ReportKind.values);
  static JsonTypeConverter2<ReportState, int, int> $converterstate =
      const EnumIndexConverter<ReportState>(ReportState.values);
}

class Report extends DataClass implements Insertable<Report> {
  final String clientEventId;
  final String journeyId;

  /// What was reported. Version 2 (spec 06): arrivals and delays join departures.
  final ReportKind kind;

  /// Null for a delay report, which belongs to the journey and not to a stop.
  final String? serviceStopId;
  final String stationName;

  /// A delay report only: the reason code, and the total in minutes (null means "unknown").
  final String? delayReason;
  final int? totalDelayMinutes;
  final DateTime occurredAt;
  final ReportState state;
  final int attempts;
  final DateTime? nextTryAt;
  final String? lastReason;

  /// The Keycloak subject of the operator who made the report (version 3). Empty for reports saved before it existed: no operator
  /// owns those, so no one sees them. Each operator sees only his own reports on a shared phone.
  final String owner;
  const Report({
    required this.clientEventId,
    required this.journeyId,
    required this.kind,
    this.serviceStopId,
    required this.stationName,
    this.delayReason,
    this.totalDelayMinutes,
    required this.occurredAt,
    required this.state,
    required this.attempts,
    this.nextTryAt,
    this.lastReason,
    required this.owner,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['client_event_id'] = Variable<String>(clientEventId);
    map['journey_id'] = Variable<String>(journeyId);
    {
      map['kind'] = Variable<int>($ReportsTable.$converterkind.toSql(kind));
    }
    if (!nullToAbsent || serviceStopId != null) {
      map['service_stop_id'] = Variable<String>(serviceStopId);
    }
    map['station_name'] = Variable<String>(stationName);
    if (!nullToAbsent || delayReason != null) {
      map['delay_reason'] = Variable<String>(delayReason);
    }
    if (!nullToAbsent || totalDelayMinutes != null) {
      map['total_delay_minutes'] = Variable<int>(totalDelayMinutes);
    }
    map['occurred_at'] = Variable<DateTime>(occurredAt);
    {
      map['state'] = Variable<int>($ReportsTable.$converterstate.toSql(state));
    }
    map['attempts'] = Variable<int>(attempts);
    if (!nullToAbsent || nextTryAt != null) {
      map['next_try_at'] = Variable<DateTime>(nextTryAt);
    }
    if (!nullToAbsent || lastReason != null) {
      map['last_reason'] = Variable<String>(lastReason);
    }
    map['owner'] = Variable<String>(owner);
    return map;
  }

  ReportsCompanion toCompanion(bool nullToAbsent) {
    return ReportsCompanion(
      clientEventId: Value(clientEventId),
      journeyId: Value(journeyId),
      kind: Value(kind),
      serviceStopId: serviceStopId == null && nullToAbsent
          ? const Value.absent()
          : Value(serviceStopId),
      stationName: Value(stationName),
      delayReason: delayReason == null && nullToAbsent
          ? const Value.absent()
          : Value(delayReason),
      totalDelayMinutes: totalDelayMinutes == null && nullToAbsent
          ? const Value.absent()
          : Value(totalDelayMinutes),
      occurredAt: Value(occurredAt),
      state: Value(state),
      attempts: Value(attempts),
      nextTryAt: nextTryAt == null && nullToAbsent
          ? const Value.absent()
          : Value(nextTryAt),
      lastReason: lastReason == null && nullToAbsent
          ? const Value.absent()
          : Value(lastReason),
      owner: Value(owner),
    );
  }

  factory Report.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return Report(
      clientEventId: serializer.fromJson<String>(json['clientEventId']),
      journeyId: serializer.fromJson<String>(json['journeyId']),
      kind: $ReportsTable.$converterkind.fromJson(
        serializer.fromJson<int>(json['kind']),
      ),
      serviceStopId: serializer.fromJson<String?>(json['serviceStopId']),
      stationName: serializer.fromJson<String>(json['stationName']),
      delayReason: serializer.fromJson<String?>(json['delayReason']),
      totalDelayMinutes: serializer.fromJson<int?>(json['totalDelayMinutes']),
      occurredAt: serializer.fromJson<DateTime>(json['occurredAt']),
      state: $ReportsTable.$converterstate.fromJson(
        serializer.fromJson<int>(json['state']),
      ),
      attempts: serializer.fromJson<int>(json['attempts']),
      nextTryAt: serializer.fromJson<DateTime?>(json['nextTryAt']),
      lastReason: serializer.fromJson<String?>(json['lastReason']),
      owner: serializer.fromJson<String>(json['owner']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'clientEventId': serializer.toJson<String>(clientEventId),
      'journeyId': serializer.toJson<String>(journeyId),
      'kind': serializer.toJson<int>($ReportsTable.$converterkind.toJson(kind)),
      'serviceStopId': serializer.toJson<String?>(serviceStopId),
      'stationName': serializer.toJson<String>(stationName),
      'delayReason': serializer.toJson<String?>(delayReason),
      'totalDelayMinutes': serializer.toJson<int?>(totalDelayMinutes),
      'occurredAt': serializer.toJson<DateTime>(occurredAt),
      'state': serializer.toJson<int>(
        $ReportsTable.$converterstate.toJson(state),
      ),
      'attempts': serializer.toJson<int>(attempts),
      'nextTryAt': serializer.toJson<DateTime?>(nextTryAt),
      'lastReason': serializer.toJson<String?>(lastReason),
      'owner': serializer.toJson<String>(owner),
    };
  }

  Report copyWith({
    String? clientEventId,
    String? journeyId,
    ReportKind? kind,
    Value<String?> serviceStopId = const Value.absent(),
    String? stationName,
    Value<String?> delayReason = const Value.absent(),
    Value<int?> totalDelayMinutes = const Value.absent(),
    DateTime? occurredAt,
    ReportState? state,
    int? attempts,
    Value<DateTime?> nextTryAt = const Value.absent(),
    Value<String?> lastReason = const Value.absent(),
    String? owner,
  }) => Report(
    clientEventId: clientEventId ?? this.clientEventId,
    journeyId: journeyId ?? this.journeyId,
    kind: kind ?? this.kind,
    serviceStopId: serviceStopId.present
        ? serviceStopId.value
        : this.serviceStopId,
    stationName: stationName ?? this.stationName,
    delayReason: delayReason.present ? delayReason.value : this.delayReason,
    totalDelayMinutes: totalDelayMinutes.present
        ? totalDelayMinutes.value
        : this.totalDelayMinutes,
    occurredAt: occurredAt ?? this.occurredAt,
    state: state ?? this.state,
    attempts: attempts ?? this.attempts,
    nextTryAt: nextTryAt.present ? nextTryAt.value : this.nextTryAt,
    lastReason: lastReason.present ? lastReason.value : this.lastReason,
    owner: owner ?? this.owner,
  );
  Report copyWithCompanion(ReportsCompanion data) {
    return Report(
      clientEventId: data.clientEventId.present
          ? data.clientEventId.value
          : this.clientEventId,
      journeyId: data.journeyId.present ? data.journeyId.value : this.journeyId,
      kind: data.kind.present ? data.kind.value : this.kind,
      serviceStopId: data.serviceStopId.present
          ? data.serviceStopId.value
          : this.serviceStopId,
      stationName: data.stationName.present
          ? data.stationName.value
          : this.stationName,
      delayReason: data.delayReason.present
          ? data.delayReason.value
          : this.delayReason,
      totalDelayMinutes: data.totalDelayMinutes.present
          ? data.totalDelayMinutes.value
          : this.totalDelayMinutes,
      occurredAt: data.occurredAt.present
          ? data.occurredAt.value
          : this.occurredAt,
      state: data.state.present ? data.state.value : this.state,
      attempts: data.attempts.present ? data.attempts.value : this.attempts,
      nextTryAt: data.nextTryAt.present ? data.nextTryAt.value : this.nextTryAt,
      lastReason: data.lastReason.present
          ? data.lastReason.value
          : this.lastReason,
      owner: data.owner.present ? data.owner.value : this.owner,
    );
  }

  @override
  String toString() {
    return (StringBuffer('Report(')
          ..write('clientEventId: $clientEventId, ')
          ..write('journeyId: $journeyId, ')
          ..write('kind: $kind, ')
          ..write('serviceStopId: $serviceStopId, ')
          ..write('stationName: $stationName, ')
          ..write('delayReason: $delayReason, ')
          ..write('totalDelayMinutes: $totalDelayMinutes, ')
          ..write('occurredAt: $occurredAt, ')
          ..write('state: $state, ')
          ..write('attempts: $attempts, ')
          ..write('nextTryAt: $nextTryAt, ')
          ..write('lastReason: $lastReason, ')
          ..write('owner: $owner')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    clientEventId,
    journeyId,
    kind,
    serviceStopId,
    stationName,
    delayReason,
    totalDelayMinutes,
    occurredAt,
    state,
    attempts,
    nextTryAt,
    lastReason,
    owner,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is Report &&
          other.clientEventId == this.clientEventId &&
          other.journeyId == this.journeyId &&
          other.kind == this.kind &&
          other.serviceStopId == this.serviceStopId &&
          other.stationName == this.stationName &&
          other.delayReason == this.delayReason &&
          other.totalDelayMinutes == this.totalDelayMinutes &&
          other.occurredAt == this.occurredAt &&
          other.state == this.state &&
          other.attempts == this.attempts &&
          other.nextTryAt == this.nextTryAt &&
          other.lastReason == this.lastReason &&
          other.owner == this.owner);
}

class ReportsCompanion extends UpdateCompanion<Report> {
  final Value<String> clientEventId;
  final Value<String> journeyId;
  final Value<ReportKind> kind;
  final Value<String?> serviceStopId;
  final Value<String> stationName;
  final Value<String?> delayReason;
  final Value<int?> totalDelayMinutes;
  final Value<DateTime> occurredAt;
  final Value<ReportState> state;
  final Value<int> attempts;
  final Value<DateTime?> nextTryAt;
  final Value<String?> lastReason;
  final Value<String> owner;
  final Value<int> rowid;
  const ReportsCompanion({
    this.clientEventId = const Value.absent(),
    this.journeyId = const Value.absent(),
    this.kind = const Value.absent(),
    this.serviceStopId = const Value.absent(),
    this.stationName = const Value.absent(),
    this.delayReason = const Value.absent(),
    this.totalDelayMinutes = const Value.absent(),
    this.occurredAt = const Value.absent(),
    this.state = const Value.absent(),
    this.attempts = const Value.absent(),
    this.nextTryAt = const Value.absent(),
    this.lastReason = const Value.absent(),
    this.owner = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  ReportsCompanion.insert({
    required String clientEventId,
    required String journeyId,
    this.kind = const Value.absent(),
    this.serviceStopId = const Value.absent(),
    this.stationName = const Value.absent(),
    this.delayReason = const Value.absent(),
    this.totalDelayMinutes = const Value.absent(),
    required DateTime occurredAt,
    required ReportState state,
    this.attempts = const Value.absent(),
    this.nextTryAt = const Value.absent(),
    this.lastReason = const Value.absent(),
    this.owner = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : clientEventId = Value(clientEventId),
       journeyId = Value(journeyId),
       occurredAt = Value(occurredAt),
       state = Value(state);
  static Insertable<Report> custom({
    Expression<String>? clientEventId,
    Expression<String>? journeyId,
    Expression<int>? kind,
    Expression<String>? serviceStopId,
    Expression<String>? stationName,
    Expression<String>? delayReason,
    Expression<int>? totalDelayMinutes,
    Expression<DateTime>? occurredAt,
    Expression<int>? state,
    Expression<int>? attempts,
    Expression<DateTime>? nextTryAt,
    Expression<String>? lastReason,
    Expression<String>? owner,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (clientEventId != null) 'client_event_id': clientEventId,
      if (journeyId != null) 'journey_id': journeyId,
      if (kind != null) 'kind': kind,
      if (serviceStopId != null) 'service_stop_id': serviceStopId,
      if (stationName != null) 'station_name': stationName,
      if (delayReason != null) 'delay_reason': delayReason,
      if (totalDelayMinutes != null) 'total_delay_minutes': totalDelayMinutes,
      if (occurredAt != null) 'occurred_at': occurredAt,
      if (state != null) 'state': state,
      if (attempts != null) 'attempts': attempts,
      if (nextTryAt != null) 'next_try_at': nextTryAt,
      if (lastReason != null) 'last_reason': lastReason,
      if (owner != null) 'owner': owner,
      if (rowid != null) 'rowid': rowid,
    });
  }

  ReportsCompanion copyWith({
    Value<String>? clientEventId,
    Value<String>? journeyId,
    Value<ReportKind>? kind,
    Value<String?>? serviceStopId,
    Value<String>? stationName,
    Value<String?>? delayReason,
    Value<int?>? totalDelayMinutes,
    Value<DateTime>? occurredAt,
    Value<ReportState>? state,
    Value<int>? attempts,
    Value<DateTime?>? nextTryAt,
    Value<String?>? lastReason,
    Value<String>? owner,
    Value<int>? rowid,
  }) {
    return ReportsCompanion(
      clientEventId: clientEventId ?? this.clientEventId,
      journeyId: journeyId ?? this.journeyId,
      kind: kind ?? this.kind,
      serviceStopId: serviceStopId ?? this.serviceStopId,
      stationName: stationName ?? this.stationName,
      delayReason: delayReason ?? this.delayReason,
      totalDelayMinutes: totalDelayMinutes ?? this.totalDelayMinutes,
      occurredAt: occurredAt ?? this.occurredAt,
      state: state ?? this.state,
      attempts: attempts ?? this.attempts,
      nextTryAt: nextTryAt ?? this.nextTryAt,
      lastReason: lastReason ?? this.lastReason,
      owner: owner ?? this.owner,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (clientEventId.present) {
      map['client_event_id'] = Variable<String>(clientEventId.value);
    }
    if (journeyId.present) {
      map['journey_id'] = Variable<String>(journeyId.value);
    }
    if (kind.present) {
      map['kind'] = Variable<int>(
        $ReportsTable.$converterkind.toSql(kind.value),
      );
    }
    if (serviceStopId.present) {
      map['service_stop_id'] = Variable<String>(serviceStopId.value);
    }
    if (stationName.present) {
      map['station_name'] = Variable<String>(stationName.value);
    }
    if (delayReason.present) {
      map['delay_reason'] = Variable<String>(delayReason.value);
    }
    if (totalDelayMinutes.present) {
      map['total_delay_minutes'] = Variable<int>(totalDelayMinutes.value);
    }
    if (occurredAt.present) {
      map['occurred_at'] = Variable<DateTime>(occurredAt.value);
    }
    if (state.present) {
      map['state'] = Variable<int>(
        $ReportsTable.$converterstate.toSql(state.value),
      );
    }
    if (attempts.present) {
      map['attempts'] = Variable<int>(attempts.value);
    }
    if (nextTryAt.present) {
      map['next_try_at'] = Variable<DateTime>(nextTryAt.value);
    }
    if (lastReason.present) {
      map['last_reason'] = Variable<String>(lastReason.value);
    }
    if (owner.present) {
      map['owner'] = Variable<String>(owner.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('ReportsCompanion(')
          ..write('clientEventId: $clientEventId, ')
          ..write('journeyId: $journeyId, ')
          ..write('kind: $kind, ')
          ..write('serviceStopId: $serviceStopId, ')
          ..write('stationName: $stationName, ')
          ..write('delayReason: $delayReason, ')
          ..write('totalDelayMinutes: $totalDelayMinutes, ')
          ..write('occurredAt: $occurredAt, ')
          ..write('state: $state, ')
          ..write('attempts: $attempts, ')
          ..write('nextTryAt: $nextTryAt, ')
          ..write('lastReason: $lastReason, ')
          ..write('owner: $owner, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $KeyValuesTable extends KeyValues
    with TableInfo<$KeyValuesTable, KeyValue> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $KeyValuesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _keyMeta = const VerificationMeta('key');
  @override
  late final GeneratedColumn<String> key = GeneratedColumn<String>(
    'key',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _valueMeta = const VerificationMeta('value');
  @override
  late final GeneratedColumn<String> value = GeneratedColumn<String>(
    'value',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  @override
  List<GeneratedColumn> get $columns => [key, value];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'key_values';
  @override
  VerificationContext validateIntegrity(
    Insertable<KeyValue> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('key')) {
      context.handle(
        _keyMeta,
        key.isAcceptableOrUnknown(data['key']!, _keyMeta),
      );
    } else if (isInserting) {
      context.missing(_keyMeta);
    }
    if (data.containsKey('value')) {
      context.handle(
        _valueMeta,
        value.isAcceptableOrUnknown(data['value']!, _valueMeta),
      );
    } else if (isInserting) {
      context.missing(_valueMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {key};
  @override
  KeyValue map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return KeyValue(
      key: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}key'],
      )!,
      value: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}value'],
      )!,
    );
  }

  @override
  $KeyValuesTable createAlias(String alias) {
    return $KeyValuesTable(attachedDatabase, alias);
  }
}

class KeyValue extends DataClass implements Insertable<KeyValue> {
  final String key;
  final String value;
  const KeyValue({required this.key, required this.value});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['key'] = Variable<String>(key);
    map['value'] = Variable<String>(value);
    return map;
  }

  KeyValuesCompanion toCompanion(bool nullToAbsent) {
    return KeyValuesCompanion(key: Value(key), value: Value(value));
  }

  factory KeyValue.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return KeyValue(
      key: serializer.fromJson<String>(json['key']),
      value: serializer.fromJson<String>(json['value']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'key': serializer.toJson<String>(key),
      'value': serializer.toJson<String>(value),
    };
  }

  KeyValue copyWith({String? key, String? value}) =>
      KeyValue(key: key ?? this.key, value: value ?? this.value);
  KeyValue copyWithCompanion(KeyValuesCompanion data) {
    return KeyValue(
      key: data.key.present ? data.key.value : this.key,
      value: data.value.present ? data.value.value : this.value,
    );
  }

  @override
  String toString() {
    return (StringBuffer('KeyValue(')
          ..write('key: $key, ')
          ..write('value: $value')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(key, value);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is KeyValue && other.key == this.key && other.value == this.value);
}

class KeyValuesCompanion extends UpdateCompanion<KeyValue> {
  final Value<String> key;
  final Value<String> value;
  final Value<int> rowid;
  const KeyValuesCompanion({
    this.key = const Value.absent(),
    this.value = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  KeyValuesCompanion.insert({
    required String key,
    required String value,
    this.rowid = const Value.absent(),
  }) : key = Value(key),
       value = Value(value);
  static Insertable<KeyValue> custom({
    Expression<String>? key,
    Expression<String>? value,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (key != null) 'key': key,
      if (value != null) 'value': value,
      if (rowid != null) 'rowid': rowid,
    });
  }

  KeyValuesCompanion copyWith({
    Value<String>? key,
    Value<String>? value,
    Value<int>? rowid,
  }) {
    return KeyValuesCompanion(
      key: key ?? this.key,
      value: value ?? this.value,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (key.present) {
      map['key'] = Variable<String>(key.value);
    }
    if (value.present) {
      map['value'] = Variable<String>(value.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('KeyValuesCompanion(')
          ..write('key: $key, ')
          ..write('value: $value, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

abstract class _$AppDatabase extends GeneratedDatabase {
  _$AppDatabase(QueryExecutor e) : super(e);
  $AppDatabaseManager get managers => $AppDatabaseManager(this);
  late final $ReportsTable reports = $ReportsTable(this);
  late final $KeyValuesTable keyValues = $KeyValuesTable(this);
  @override
  Iterable<TableInfo<Table, Object?>> get allTables =>
      allSchemaEntities.whereType<TableInfo<Table, Object?>>();
  @override
  List<DatabaseSchemaEntity> get allSchemaEntities => [reports, keyValues];
}

typedef $$ReportsTableCreateCompanionBuilder = ReportsCompanion Function({
  required String clientEventId,
  required String journeyId,
  Value<ReportKind> kind,
  Value<String?> serviceStopId,
  Value<String> stationName,
  Value<String?> delayReason,
  Value<int?> totalDelayMinutes,
  required DateTime occurredAt,
  required ReportState state,
  Value<int> attempts,
  Value<DateTime?> nextTryAt,
  Value<String?> lastReason,
  Value<String> owner,
  Value<int> rowid,
});
typedef $$ReportsTableUpdateCompanionBuilder = ReportsCompanion Function({
  Value<String> clientEventId,
  Value<String> journeyId,
  Value<ReportKind> kind,
  Value<String?> serviceStopId,
  Value<String> stationName,
  Value<String?> delayReason,
  Value<int?> totalDelayMinutes,
  Value<DateTime> occurredAt,
  Value<ReportState> state,
  Value<int> attempts,
  Value<DateTime?> nextTryAt,
  Value<String?> lastReason,
  Value<String> owner,
  Value<int> rowid,
});

class $$ReportsTableFilterComposer
    extends Composer<_$AppDatabase, $ReportsTable> {
  $$ReportsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get clientEventId => $composableBuilder(
    column: $table.clientEventId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get journeyId => $composableBuilder(
    column: $table.journeyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnWithTypeConverterFilters<ReportKind, ReportKind, int> get kind =>
      $composableBuilder(
        column: $table.kind,
        builder: (column) => ColumnWithTypeConverterFilters(column),
      );

  ColumnFilters<String> get serviceStopId => $composableBuilder(
    column: $table.serviceStopId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get stationName => $composableBuilder(
    column: $table.stationName,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get delayReason => $composableBuilder(
    column: $table.delayReason,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get totalDelayMinutes => $composableBuilder(
    column: $table.totalDelayMinutes,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get occurredAt => $composableBuilder(
    column: $table.occurredAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnWithTypeConverterFilters<ReportState, ReportState, int> get state =>
      $composableBuilder(
        column: $table.state,
        builder: (column) => ColumnWithTypeConverterFilters(column),
      );

  ColumnFilters<int> get attempts => $composableBuilder(
    column: $table.attempts,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get nextTryAt => $composableBuilder(
    column: $table.nextTryAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get lastReason => $composableBuilder(
    column: $table.lastReason,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get owner => $composableBuilder(
    column: $table.owner,
    builder: (column) => ColumnFilters(column),
  );
}

class $$ReportsTableOrderingComposer
    extends Composer<_$AppDatabase, $ReportsTable> {
  $$ReportsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get clientEventId => $composableBuilder(
    column: $table.clientEventId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get journeyId => $composableBuilder(
    column: $table.journeyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get kind => $composableBuilder(
    column: $table.kind,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get serviceStopId => $composableBuilder(
    column: $table.serviceStopId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get stationName => $composableBuilder(
    column: $table.stationName,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get delayReason => $composableBuilder(
    column: $table.delayReason,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get totalDelayMinutes => $composableBuilder(
    column: $table.totalDelayMinutes,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get occurredAt => $composableBuilder(
    column: $table.occurredAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get state => $composableBuilder(
    column: $table.state,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get attempts => $composableBuilder(
    column: $table.attempts,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get nextTryAt => $composableBuilder(
    column: $table.nextTryAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get lastReason => $composableBuilder(
    column: $table.lastReason,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get owner => $composableBuilder(
    column: $table.owner,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$ReportsTableAnnotationComposer
    extends Composer<_$AppDatabase, $ReportsTable> {
  $$ReportsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get clientEventId => $composableBuilder(
    column: $table.clientEventId,
    builder: (column) => column,
  );

  GeneratedColumn<String> get journeyId =>
      $composableBuilder(column: $table.journeyId, builder: (column) => column);

  GeneratedColumnWithTypeConverter<ReportKind, int> get kind =>
      $composableBuilder(column: $table.kind, builder: (column) => column);

  GeneratedColumn<String> get serviceStopId => $composableBuilder(
    column: $table.serviceStopId,
    builder: (column) => column,
  );

  GeneratedColumn<String> get stationName => $composableBuilder(
    column: $table.stationName,
    builder: (column) => column,
  );

  GeneratedColumn<String> get delayReason => $composableBuilder(
    column: $table.delayReason,
    builder: (column) => column,
  );

  GeneratedColumn<int> get totalDelayMinutes => $composableBuilder(
    column: $table.totalDelayMinutes,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get occurredAt => $composableBuilder(
    column: $table.occurredAt,
    builder: (column) => column,
  );

  GeneratedColumnWithTypeConverter<ReportState, int> get state =>
      $composableBuilder(column: $table.state, builder: (column) => column);

  GeneratedColumn<int> get attempts =>
      $composableBuilder(column: $table.attempts, builder: (column) => column);

  GeneratedColumn<DateTime> get nextTryAt =>
      $composableBuilder(column: $table.nextTryAt, builder: (column) => column);

  GeneratedColumn<String> get lastReason => $composableBuilder(
    column: $table.lastReason,
    builder: (column) => column,
  );

  GeneratedColumn<String> get owner =>
      $composableBuilder(column: $table.owner, builder: (column) => column);
}

class $$ReportsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $ReportsTable,
          Report,
          $$ReportsTableFilterComposer,
          $$ReportsTableOrderingComposer,
          $$ReportsTableAnnotationComposer,
          $$ReportsTableCreateCompanionBuilder,
          $$ReportsTableUpdateCompanionBuilder,
          (Report, BaseReferences<_$AppDatabase, $ReportsTable, Report>),
          Report,
          PrefetchHooks Function()
        > {
  $$ReportsTableTableManager(_$AppDatabase db, $ReportsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$ReportsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$ReportsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$ReportsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> clientEventId = const Value.absent(),
                Value<String> journeyId = const Value.absent(),
                Value<ReportKind> kind = const Value.absent(),
                Value<String?> serviceStopId = const Value.absent(),
                Value<String> stationName = const Value.absent(),
                Value<String?> delayReason = const Value.absent(),
                Value<int?> totalDelayMinutes = const Value.absent(),
                Value<DateTime> occurredAt = const Value.absent(),
                Value<ReportState> state = const Value.absent(),
                Value<int> attempts = const Value.absent(),
                Value<DateTime?> nextTryAt = const Value.absent(),
                Value<String?> lastReason = const Value.absent(),
                Value<String> owner = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => ReportsCompanion(
                clientEventId: clientEventId,
                journeyId: journeyId,
                kind: kind,
                serviceStopId: serviceStopId,
                stationName: stationName,
                delayReason: delayReason,
                totalDelayMinutes: totalDelayMinutes,
                occurredAt: occurredAt,
                state: state,
                attempts: attempts,
                nextTryAt: nextTryAt,
                lastReason: lastReason,
                owner: owner,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String clientEventId,
                required String journeyId,
                Value<ReportKind> kind = const Value.absent(),
                Value<String?> serviceStopId = const Value.absent(),
                Value<String> stationName = const Value.absent(),
                Value<String?> delayReason = const Value.absent(),
                Value<int?> totalDelayMinutes = const Value.absent(),
                required DateTime occurredAt,
                required ReportState state,
                Value<int> attempts = const Value.absent(),
                Value<DateTime?> nextTryAt = const Value.absent(),
                Value<String?> lastReason = const Value.absent(),
                Value<String> owner = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => ReportsCompanion.insert(
                clientEventId: clientEventId,
                journeyId: journeyId,
                kind: kind,
                serviceStopId: serviceStopId,
                stationName: stationName,
                delayReason: delayReason,
                totalDelayMinutes: totalDelayMinutes,
                occurredAt: occurredAt,
                state: state,
                attempts: attempts,
                nextTryAt: nextTryAt,
                lastReason: lastReason,
                owner: owner,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$ReportsTable, Report>(table),
                  BaseReferences<_$AppDatabase, $ReportsTable, Report>(
                    db,
                    table,
                    e,
                  ),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$ReportsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $ReportsTable,
      Report,
      $$ReportsTableFilterComposer,
      $$ReportsTableOrderingComposer,
      $$ReportsTableAnnotationComposer,
      $$ReportsTableCreateCompanionBuilder,
      $$ReportsTableUpdateCompanionBuilder,
      (Report, BaseReferences<_$AppDatabase, $ReportsTable, Report>),
      Report,
      PrefetchHooks Function()
    >;
typedef $$KeyValuesTableCreateCompanionBuilder = KeyValuesCompanion Function({
  required String key,
  required String value,
  Value<int> rowid,
});
typedef $$KeyValuesTableUpdateCompanionBuilder = KeyValuesCompanion Function({
  Value<String> key,
  Value<String> value,
  Value<int> rowid,
});

class $$KeyValuesTableFilterComposer
    extends Composer<_$AppDatabase, $KeyValuesTable> {
  $$KeyValuesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get key => $composableBuilder(
    column: $table.key,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get value => $composableBuilder(
    column: $table.value,
    builder: (column) => ColumnFilters(column),
  );
}

class $$KeyValuesTableOrderingComposer
    extends Composer<_$AppDatabase, $KeyValuesTable> {
  $$KeyValuesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get key => $composableBuilder(
    column: $table.key,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get value => $composableBuilder(
    column: $table.value,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$KeyValuesTableAnnotationComposer
    extends Composer<_$AppDatabase, $KeyValuesTable> {
  $$KeyValuesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get key =>
      $composableBuilder(column: $table.key, builder: (column) => column);

  GeneratedColumn<String> get value =>
      $composableBuilder(column: $table.value, builder: (column) => column);
}

class $$KeyValuesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $KeyValuesTable,
          KeyValue,
          $$KeyValuesTableFilterComposer,
          $$KeyValuesTableOrderingComposer,
          $$KeyValuesTableAnnotationComposer,
          $$KeyValuesTableCreateCompanionBuilder,
          $$KeyValuesTableUpdateCompanionBuilder,
          (KeyValue, BaseReferences<_$AppDatabase, $KeyValuesTable, KeyValue>),
          KeyValue,
          PrefetchHooks Function()
        > {
  $$KeyValuesTableTableManager(_$AppDatabase db, $KeyValuesTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$KeyValuesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$KeyValuesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$KeyValuesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback: ({
            Value<String> key = const Value.absent(),
            Value<String> value = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) => KeyValuesCompanion(key: key, value: value, rowid: rowid),
          createCompanionCallback: ({
            required String key,
            required String value,
            Value<int> rowid = const Value.absent(),
          }) => KeyValuesCompanion.insert(key: key, value: value, rowid: rowid),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$KeyValuesTable, KeyValue>(table),
                  BaseReferences<_$AppDatabase, $KeyValuesTable, KeyValue>(
                    db,
                    table,
                    e,
                  ),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$KeyValuesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $KeyValuesTable,
      KeyValue,
      $$KeyValuesTableFilterComposer,
      $$KeyValuesTableOrderingComposer,
      $$KeyValuesTableAnnotationComposer,
      $$KeyValuesTableCreateCompanionBuilder,
      $$KeyValuesTableUpdateCompanionBuilder,
      (KeyValue, BaseReferences<_$AppDatabase, $KeyValuesTable, KeyValue>),
      KeyValue,
      PrefetchHooks Function()
    >;

class $AppDatabaseManager {
  final _$AppDatabase _db;
  $AppDatabaseManager(this._db);
  $$ReportsTableTableManager get reports =>
      $$ReportsTableTableManager(_db, _db.reports);
  $$KeyValuesTableTableManager get keyValues =>
      $$KeyValuesTableTableManager(_db, _db.keyValues);
}
