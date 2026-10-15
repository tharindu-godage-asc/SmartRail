import 'dart:convert';

import 'package:clock/clock.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:operator_app/features/auth/domain/auth.dart';
import 'package:operator_app/features/auth/domain/auth_repository.dart';

import '../support/fakes.dart';

String jwt(Map<String, Object> claims) {
  String part(Object o) =>
      base64Url.encode(utf8.encode(jsonEncode(o))).replaceAll('=', '');
  return '${part({'alg': 'none'})}.${part(claims)}.sig';
}

void main() {
  final t0 = DateTime.utc(2026, 10, 6, 3);

  TokenSet tokens({
    required DateTime expiry,
    String access = 'access-1',
    String refresh = 'refresh-1',
    String? id = 'id-1',
  }) => TokenSet(
    accessToken: access,
    refreshToken: refresh,
    idToken: id,
    accessTokenExpiry: expiry,
  );

  late FakeTokenStore store;
  late FakeAuthGateway gateway;
  late AuthRepository repo;

  setUp(() {
    store = FakeTokenStore();
    gateway = FakeAuthGateway();
    repo = AuthRepository(gateway: gateway, store: store);
  });

  group('sign in (AC-E01-01 1)', () {
    test(
      'stores the tokens, shows the operator name and clears the started flag',
      () async {
        final access = jwt({
          'given_name': 'Nimal',
          'family_name': 'Perera',
          'preferred_username': 'nimal',
        });
        gateway.onAuthorize = () async =>
            tokens(expiry: t0.add(const Duration(minutes: 15)), access: access);

        final state = await repo.signIn();

        expect(state.status, AuthStatus.signedIn);
        expect(state.displayName, 'Nimal P.');
        expect(store.tokens?.refreshToken, 'refresh-1');
        expect(store.signInStarted, isFalse);
      },
    );

    test('falls back to the user name when the token has no given name', () {
      expect(
        displayNameFromToken(jwt({'preferred_username': 'nimal'})),
        'nimal',
      );
      expect(displayNameFromToken('not-a-token'), '');
    });

    test('closing the Keycloak page is a cancellation, not an error', () async {
      gateway.onAuthorize = () async => throw const SignInCancelled();
      expect((await repo.signIn()).status, AuthStatus.signInCancelled);
      expect(store.tokens, isNull);
    });
  });

  group('process death during sign-in (spike finding 5)', () {
    test('a started flag with no tokens means the result was lost: ask again, once', () async {
      store.signInStarted = true;

      expect((await repo.restore()).status, AuthStatus.signInInterrupted);
      expect(
        (await repo.restore()).status,
        AuthStatus.signedOut,
        reason: 'the notice is shown once',
      );
    });

    test('a stored session is signed in', () async {
      store.tokens = tokens(expiry: t0.add(const Duration(minutes: 5)));
      expect((await repo.restore()).isSignedIn, isTrue);
    });
  });

  group('silent refresh (AC-E01-01 3)', () {
    test('a token with time left is used as it is', () async {
      store.tokens = tokens(expiry: t0.add(const Duration(minutes: 5)));
      await withClock(Clock.fixed(t0), () async {
        expect(await repo.accessToken(), 'access-1');
      });
      expect(gateway.refreshCalls, 0);
    });

    test('an expired token is refreshed without asking the operator, and the new tokens are kept', () async {
      store.tokens = tokens(expiry: t0.subtract(const Duration(minutes: 1)));
      gateway.onRefresh = (refresh) async {
        expect(refresh, 'refresh-1');
        return tokens(
          expiry: t0.add(const Duration(minutes: 15)),
          access: 'access-2',
          refresh: 'refresh-2',
        );
      };

      await withClock(Clock.fixed(t0), () async {
        expect(await repo.accessToken(), 'access-2');
      });
      expect(store.tokens?.refreshToken, 'refresh-2');
      expect(
        store.tokens?.idToken,
        'id-1',
        reason: 'the id token is kept for sign-out',
      );
    });

    test('a token about to expire is refreshed too', () async {
      store.tokens = tokens(expiry: t0.add(const Duration(seconds: 10)));
      gateway.onRefresh = (_) async => tokens(
        expiry: t0.add(const Duration(minutes: 15)),
        access: 'access-2',
      );

      await withClock(Clock.fixed(t0), () async {
        expect(await repo.accessToken(), 'access-2');
      });
    });

    test('a refused refresh token ends the session and clears the tokens (AC-E01-01 4)', () async {
      store.tokens = tokens(expiry: t0.subtract(const Duration(hours: 9)));
      gateway.onRefresh = (_) async => throw const RefreshRejected();

      await withClock(Clock.fixed(t0), () async {
        await expectLater(repo.accessToken(), throwsA(isA<RefreshRejected>()));
      });
      expect(store.tokens, isNull);
    });

    test(
      'offline refresh keeps the session: the failure is not a sign-out',
      () async {
        store.tokens = tokens(expiry: t0.subtract(const Duration(minutes: 1)));
        gateway.onRefresh = (_) async => throw const RefreshUnavailable();

        await withClock(Clock.fixed(t0), () async {
          await expectLater(
            repo.accessToken(),
            throwsA(isA<RefreshUnavailable>()),
          );
        });
        expect(store.tokens, isNotNull);
      },
    );

    test('no session gives no token', () async {
      expect(await repo.accessToken(), isNull);
    });
  });

  group('sign out', () {
    test('ends the Keycloak session and forgets the tokens', () async {
      store.tokens = tokens(expiry: t0);
      await repo.signOut();
      expect(gateway.endSessionCalls, 1);
      expect(store.tokens, isNull);
    });
  });
}
