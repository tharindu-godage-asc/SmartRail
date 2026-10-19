import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:passenger_app/app/app.dart';
import 'package:passenger_app/app/providers.dart';
import 'package:passenger_app/data/menu.dart';
import 'package:passenger_app/data/models.dart';
import 'package:passenger_app/data/passenger_api.dart';
import 'package:passenger_app/data/preferences.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'fixtures.dart';

/// The API with answers a test sets and a switch to make every call fail (a server that does not answer).
class FakeApi implements PassengerApi {
  Map<String, dynamic>? status = statusJson();
  List<Map<String, dynamic>> trainsToday = [resultJson()];
  Map<String, dynamic> stationList = stationsBody;
  Map<String, dynamic> line = {
    'statusKey': 'Normal',
    'journeysInProgress': 1,
    'serverTime': '2026-10-16T03:12:00Z',
  };
  Map<String, dynamic>?
  menuAnswer; // null: no food on this trip (an empty list)
  bool failing = false;
  int stationCalls = 0;
  int statusCalls = 0;

  void _check() {
    if (failing) throw StateError('server unreachable');
  }

  @override
  Future<StationsAnswer?> stations({String? knownVersion}) async {
    stationCalls++;
    _check();
    if (knownVersion == stationList['version']) return null; // 304
    return StationsAnswer(
      stationList['version'] as String,
      (stationList['stations'] as List)
          .map((s) => Station.fromJson((s as Map).cast<String, dynamic>()))
          .toList(),
    );
  }

  @override
  Future<TrainsAnswer> trains({
    required String from,
    required String to,
    String? date,
  }) async {
    _check();
    return TrainsAnswer.fromJson({
      'trains': date == null ? trainsToday : <Map<String, dynamic>>[],
      'serverTime': '2026-10-16T03:12:00Z',
    });
  }

  @override
  Future<TrainStatus?> trainStatus(String trainNumber) async {
    statusCalls++;
    _check();
    final json = status;
    return json == null ? null : TrainStatus.fromJson(json);
  }

  @override
  Future<TrainMenu?> menu(
    String trainNumber, {
    String? from,
    String? to,
  }) async {
    _check();
    return TrainMenu.fromJson(menuAnswer ?? menuJson(empty: true));
  }

  @override
  Future<LineStatus> lineStatus() async {
    _check();
    return LineStatus.fromJson(line);
  }
}

/// One app run against the fake API, a controllable network and in-memory preferences.
class Harness {
  Harness._(this.api, this.network, this.prefs);

  final FakeApi api;
  final StreamController<bool> network;
  final Preferences prefs;

  /// [stored] pre-fills the phone (language, onboarding, origin ...), as if the app had run before.
  static Future<Harness> create({
    Map<String, Object> stored = const {},
    FakeApi? api,
  }) async {
    SharedPreferences.setMockInitialValues(stored);
    final prefs = Preferences(await SharedPreferences.getInstance());
    return Harness._(
      api ?? FakeApi(),
      StreamController<bool>.broadcast(),
      prefs,
    );
  }

  /// A phone that has been used: English chosen and the intro seen.
  static const used = <String, Object>{'language': 'en', 'onboarded': true};

  void goOffline() => network.add(false);
  void goOnline() => network.add(true);

  Future<void> pump(
    WidgetTester tester, {
    double textScale = 1.0,
    List<Override> overrides = const [],
  }) async {
    tester.view.physicalSize = const Size(390 * 2, 844 * 2);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          ...overrides,
          preferencesProvider.overrideWithValue(prefs),
          apiProvider.overrideWithValue(api),
          hasNetworkProvider.overrideWith((ref) async* {
            yield true;
            yield* network.stream;
          }),
        ],
        child: MediaQuery(
          data: MediaQueryData(
            size: const Size(390, 844),
            textScaler: TextScaler.linear(textScale),
          ),
          child: const PassengerApp(),
        ),
      ),
    );
    await tester.pump(const Duration(milliseconds: 1000)); // the splash
    await tester.pumpAndSettle();
  }

  /// Lets the timers run: polls fire every 10 s (a train) and every 30 s (lists).
  static Future<void> pass(WidgetTester tester, Duration d) async {
    await tester.pump(d);
    await tester.pumpAndSettle(const Duration(milliseconds: 50));
  }
}
