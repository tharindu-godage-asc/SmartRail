import 'package:clock/clock.dart';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift_flutter/drift_flutter.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config.dart';
import '../features/auth/data/app_auth_gateway.dart';
import '../features/auth/data/secure_token_store.dart';
import '../features/auth/domain/auth_repository.dart';
import '../features/journey/data/drift_local_cache.dart';
import '../features/journey/data/smartrail_api.dart';
import '../features/journey/domain/journey_api.dart';
import '../features/journey/domain/local_cache.dart';
import '../features/reports/data/app_database.dart';
import '../features/reports/data/drift_report_queue.dart';
import '../features/reports/domain/report_queue.dart';

/// The wiring of the app. Every outside thing (network, storage, sign-in library) is a provider that tests override.

/// The phone clock, as a provider so a test can fix the time the report cleanup uses.
final phoneNowProvider = Provider<DateTime Function()>((ref) => clock.now);

final appConfigProvider = Provider<AppConfig>((ref) => const AppConfig());

final databaseProvider = Provider<AppDatabase>((ref) {
  final db = AppDatabase(driftDatabase(name: 'smartrail_operator'));
  ref.onDispose(db.close);
  return db;
});

final reportQueueProvider = Provider<ReportQueue>(
  (ref) => DriftReportQueue(ref.watch(databaseProvider)),
);

final localCacheProvider = Provider<LocalCache>(
  (ref) => DriftLocalCache(ref.watch(databaseProvider)),
);

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(
    gateway: AppAuthGateway(ref.watch(appConfigProvider)),
    store: SecureTokenStore(),
  ),
);

final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: ref.watch(appConfigProvider).apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 15),
    ),
  );
  ref.onDispose(dio.close);
  return dio;
});

final smartRailApiProvider = Provider<SmartRailApi>(
  (ref) =>
      SmartRailApi(ref.watch(dioProvider), ref.watch(authRepositoryProvider)),
);

final journeyApiProvider = Provider<JourneyApi>(
  (ref) => ref.watch(smartRailApiProvider),
);

final reportApiProvider = Provider<ReportApi>(
  (ref) => ref.watch(smartRailApiProvider),
);

/// True while the phone has some kind of network. It only says the radio is on; a failed request is what proves the server is out of reach.
final isOnlineProvider = StreamProvider<bool>((ref) async* {
  final connectivity = Connectivity();
  yield (await connectivity.checkConnectivity()).any(
    (r) => r != ConnectivityResult.none,
  );
  yield* connectivity.onConnectivityChanged.map(
    (results) => results.any((r) => r != ConnectivityResult.none),
  );
});
