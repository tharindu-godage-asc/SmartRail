import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/app/app.dart';
import 'package:operator_app/app/providers.dart';
import 'package:operator_app/features/auth/domain/auth.dart';
import 'package:operator_app/features/auth/domain/auth_repository.dart';
import 'package:operator_app/features/journey/domain/journey_api.dart';
import 'package:operator_app/features/journey/domain/local_cache.dart';
import 'package:operator_app/features/reports/domain/report_queue.dart';

import 'fakes.dart';

String fakeJwt(Map<String, Object> claims) {
  String part(Object o) =>
      base64Url.encode(utf8.encode(jsonEncode(o))).replaceAll('=', '');
  return '${part({'alg': 'none'})}.${part(claims)}.sig';
}

/// One app run against fakes, so a screen can be checked in every state without a phone, a server or a database.
class TestApp {
  TestApp({
    JourneyApi? api,
    ReportQueue? queue,
    ReportApi? reportApi,
    LocalCache? cache,
    FakeTokenStore? store,
    this.online = true,
  }) : api = api ?? FakeJourneyApi(journey: demoJourney()),
       queue = queue ?? InMemoryReportQueue(),
       reportApi = reportApi ?? ScriptedReportApi([]),
       cache = cache ?? InMemoryLocalCache(),
       store = store ?? signedInStore();

  final JourneyApi api;
  final ReportQueue queue;
  final ReportApi reportApi;
  final LocalCache cache;
  final FakeTokenStore store;
  bool online;

  static FakeTokenStore signedInStore() => FakeTokenStore()
    ..tokens = TokenSet(
      accessToken: fakeJwt({'given_name': 'Nimal', 'family_name': 'Perera'}),
      refreshToken: 'refresh',
      accessTokenExpiry: DateTime.utc(2099),
    );

  Future<void> pump(WidgetTester tester, {double textScale = 1}) async {
    await _loadTestFont();
    tester.view.physicalSize = const Size(390 * 3, 844 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    tester.platformDispatcher.textScaleFactorTestValue = textScale;
    addTearDown(tester.platformDispatcher.clearAllTestValues);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          authRepositoryProvider.overrideWithValue(
            AuthRepository(gateway: FakeAuthGateway(), store: store),
          ),
          journeyApiProvider.overrideWithValue(api),
          reportApiProvider.overrideWithValue(reportApi),
          reportQueueProvider.overrideWithValue(queue),
          localCacheProvider.overrideWithValue(cache),
          isOnlineProvider.overrideWith((ref) => Stream.value(online)),
        ],
        child: const OperatorApp(),
      ),
    );
    await settle(tester);
  }

  /// Takes the app away so its timers stop (a test must not end with a timer pending).
  Future<void> close(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 1));
  }
}

/// Lets futures and short animations finish. `pumpAndSettle` cannot be used: the loading spinner never stops.
Future<void> settle(WidgetTester tester, [int times = 15]) async {
  for (var i = 0; i < times; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

bool _fontLoaded = false;

/// The test engine draws every letter as a square (the Ahem font), which is far wider than real text and would hide
/// or invent overflow problems. Roboto ships with the Flutter SDK; it stands in for Noto Sans, whose widths are close.
Future<void> _loadTestFont() async {
  if (_fontLoaded) {
    return;
  }
  _fontLoaded = true;
  final root = Platform.environment['FLUTTER_ROOT'];
  if (root == null) {
    return;
  }
  final dir = '$root/bin/cache/artifacts/material_fonts';
  final loader = FontLoader('Noto Sans');
  for (final file in [
    'Roboto-Regular.ttf',
    'Roboto-Medium.ttf',
    'Roboto-Bold.ttf',
  ]) {
    final font = File('$dir/$file');
    if (font.existsSync()) {
      final bytes = font.readAsBytesSync();
      loader.addFont(Future.value(ByteData.view(bytes.buffer)));
    }
  }
  await loader.load();
}
