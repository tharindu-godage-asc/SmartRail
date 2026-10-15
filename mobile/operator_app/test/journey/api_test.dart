import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/auth/domain/auth.dart';
import 'package:operator_app/features/auth/domain/auth_repository.dart';
import 'package:operator_app/features/journey/data/smartrail_api.dart';
import 'package:operator_app/features/journey/domain/journey_api.dart';
import 'package:operator_app/features/reports/domain/send_result.dart';

import '../support/fakes.dart';

/// A scripted network: answers each request from a list and records what was asked.
class _Adapter implements HttpClientAdapter {
  _Adapter(this.answers);

  final List<Object> answers; // (status, body) pairs or a DioException to throw
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final next = answers.removeAt(0);
    if (next is DioException) {
      throw next;
    }
    final (status, body) = next as (int, Object?);
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  final journeyJson = demoJourney().toJson();

  ({
    SmartRailApi api,
    _Adapter adapter,
    FakeAuthGateway gateway,
    FakeTokenStore store,
  })
  build(List<Object> answers, {DateTime? expiry}) {
    final adapter = _Adapter(answers);
    final dio = Dio(BaseOptions(baseUrl: 'http://api.test'))
      ..httpClientAdapter = adapter;
    final store = FakeTokenStore()
      ..tokens = TokenSet(
        accessToken: 'access-1',
        refreshToken: 'refresh-1',
        accessTokenExpiry: expiry ?? DateTime.utc(2099),
      );
    final gateway = FakeAuthGateway();
    final api = SmartRailApi(
      dio,
      AuthRepository(gateway: gateway, store: store),
    );
    return (api: api, adapter: adapter, gateway: gateway, store: store);
  }

  group('reads', () {
    test('the assigned journey is read with the bearer token', () async {
      final t = build([(200, journeyJson)]);

      final journey = await t.api.fetchAssignedJourney();

      expect(journey?.trainNumber, '8058');
      expect(journey?.stops, hasLength(5));
      expect(t.adapter.requests.single.path, '/api/v1/operator/journey');
      expect(
        t.adapter.requests.single.headers['Authorization'],
        'Bearer access-1',
      );
    });

    test('404 means no journey is assigned', () async {
      final t = build([
        (404, {'reason': 'no_assignment_now'}),
      ]);
      expect(await t.api.fetchAssignedJourney(), isNull);
    });

    test(
      'the public train status needs no token and lists which stops departed',
      () async {
        final t = build([
          (
            200,
            {
              'journeyId': demoJourney().journeyId,
              'version': 2,
              'serverTime': '2026-10-06T03:00:00+00:00',
              'delayMinutes': 8,
              'stops': [
                {
                  'serviceStopId': stopId(1),
                  'sequence': 1,
                  'confirmation': 'Reported',
                  'actualDeparture': '2026-10-06T02:58:00+00:00',
                },
                {
                  'serviceStopId': stopId(2),
                  'sequence': 2,
                  'confirmation': 'Pending',
                  'actualDeparture': null,
                },
              ],
            },
          ),
        ]);

        final status = await t.api.fetchTrainStatus('8058');

        expect(status?.delayMinutes, 8);
        expect(status?.stops.first.hasDeparted, isTrue);
        expect(status?.stops.last.hasDeparted, isFalse);
        expect(status?.serverTime, DateTime.utc(2026, 10, 6, 3));
        expect(
          t.adapter.requests.single.headers.containsKey('Authorization'),
          isFalse,
        );
      },
    );

    test('a dead network is a NetworkException, not a crash', () async {
      final t = build([
        DioException.connectionError(
          requestOptions: RequestOptions(),
          reason: 'no route',
        ),
      ]);
      await expectLater(
        t.api.fetchAssignedJourney(),
        throwsA(isA<NetworkException>()),
      );
    });

    test('a server error is a ServerException', () async {
      final t = build([(503, {})]);
      await expectLater(
        t.api.fetchAssignedJourney(),
        throwsA(isA<ServerException>()),
      );
    });
  });

  group('token handling (AC-E01-01 3, AC-E01-03 1)', () {
    test('a 401 refreshes the token once and asks again', () async {
      final t = build([(401, {}), (200, journeyJson)]);
      t.gateway.onRefresh = (_) async =>
          const TokenSet(accessToken: 'access-2', refreshToken: 'refresh-2');

      final journey = await t.api.fetchAssignedJourney();

      expect(journey, isNotNull);
      expect(
        t.adapter.requests.last.headers['Authorization'],
        'Bearer access-2',
      );
      expect(t.gateway.refreshCalls, 1);
    });

    test('still 401 after a refresh means the session is over', () async {
      final t = build([(401, {}), (401, {})]);
      t.gateway.onRefresh = (_) async =>
          const TokenSet(accessToken: 'access-2', refreshToken: 'refresh-2');

      await expectLater(
        t.api.fetchAssignedJourney(),
        throwsA(isA<SessionExpiredException>()),
      );
    });

    test('a refused refresh token means the session is over and the tokens are gone', () async {
      final t = build([(401, {})]);
      t.gateway.onRefresh = (_) async => throw const RefreshRejected();

      await expectLater(
        t.api.fetchAssignedJourney(),
        throwsA(isA<SessionExpiredException>()),
      );
      expect(t.store.tokens, isNull);
    });

    test('an expired token that cannot be refreshed offline is a network failure, not a sign-out', () async {
      final t = build([], expiry: DateTime.utc(2000));
      t.gateway.onRefresh = (_) async => throw const RefreshUnavailable();

      await expectLater(
        t.api.fetchAssignedJourney(),
        throwsA(isA<NetworkException>()),
      );
      expect(t.store.tokens, isNotNull);
    });
  });

  group('sending a report (spec section 4)', () {
    final report = reportFor(1);

    test('posts the exact body the server expects, with the same client event id every time', () async {
      final t = build([
        (201, {'eventId': 'e', 'journeyVersion': 1, 'status': 'Delivered'}),
      ]);

      final result = await t.api.send(report);

      expect(result, isA<Delivered>());
      final request = t.adapter.requests.single;
      expect(request.method, 'POST');
      expect(request.path, '/api/v1/journeys/${report.journeyId}/events');
      expect(request.data, {
        'clientEventId': 'evt-1',
        'eventType': 'Departed',
        'serviceStopId': stopId(1),
        'occurredAt': report.occurredAt.toIso8601String(),
      });
    });

    test('200 (a repeat) is Delivered too', () async {
      final t = build([
        (200, {'eventId': 'e', 'journeyVersion': 1, 'status': 'Delivered'}),
      ]);
      expect(await t.api.send(report), isA<Delivered>());
    });

    test('the server reason code travels with a refusal', () async {
      final t = build([
        (409, {'reason': 'stop_has_different_time'}),
      ]);

      final result = await t.api.send(report);

      expect(
        result,
        isA<Rejected>().having(
          (r) => r.reason,
          'reason',
          'stop_has_different_time',
        ),
      );
    });

    test('a version conflict is retried, a failure to connect is retried, a lapsed session pauses the queue', () async {
      final conflict = build([
        (409, {'reason': 'version_conflict'}),
      ]);
      expect(await conflict.api.send(report), isA<RetryLater>());

      final offline = build([
        DioException.connectionTimeout(
          timeout: const Duration(seconds: 1),
          requestOptions: RequestOptions(),
        ),
      ]);
      expect(await offline.api.send(report), isA<RetryLater>());

      final expired = build([(401, {}), (401, {})]);
      expired.gateway.onRefresh = (_) async =>
          const TokenSet(accessToken: 'a', refreshToken: 'r');
      expect(await expired.api.send(report), isA<Unauthorized>());
    });
  });
}
