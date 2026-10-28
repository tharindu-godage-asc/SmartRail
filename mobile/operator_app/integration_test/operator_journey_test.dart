import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:operator_app/app/app.dart';
import 'package:operator_app/app/providers.dart';
import 'package:operator_app/features/auth/domain/auth.dart';
import 'package:operator_app/features/auth/domain/auth_ports.dart';
import 'package:operator_app/features/auth/domain/auth_repository.dart';

/// Integration test on a phone or emulator, against the real SmartRail API and the real Keycloak (Compose stack).
/// Needs: the stack up, `adb reverse tcp:8080 tcp:8080` and `adb reverse tcp:8180 tcp:8180`, then
///   flutter test integration_test --device-id emulator-5554
/// The browser sign-in (Authorization Code with PKCE) cannot be driven by this package, so the test takes a real access token for
/// `nimal` from Keycloak (the Compose-only `smartrail-dev-token` client, development password) and hands it to the app as if the
/// sign-in had happened. Everything after that is real: the journey read, the screen, the local database. It reads only.
/// Needs a journey Nimal is assigned to now: use ./scripts/reset-db.sh --demo first, otherwise the test is skipped with a message.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    'the operator app shows the journey the real API assigns to Nimal',
    (tester) async {
      final dio = Dio(BaseOptions(validateStatus: (_) => true));
      final tokenResponse = await dio.post<Map<String, dynamic>>(
        'http://localhost:8180/realms/smartrail/protocol/openid-connect/token',
        data: {
          'client_id': 'smartrail-dev-token',
          'grant_type': 'password',
          'username': 'nimal',
          'password': 'nimal-dev-only',
        },
        options: Options(contentType: Headers.formUrlEncodedContentType),
      );
      expect(
        tokenResponse.statusCode,
        200,
        reason: 'a token for nimal from the real Keycloak (adb reverse tcp:8180 tcp:8180)',
      );
      final accessToken = tokenResponse.data!['access_token'] as String;

      final journeyResponse = await dio.get<Map<String, dynamic>>(
        'http://localhost:8080/api/v1/operator/journey',
        options: Options(headers: {'Authorization': 'Bearer $accessToken'}),
      );
      if (journeyResponse.statusCode == 404) {
        markTestSkipped(
          'Nimal has no journey now: run ./scripts/reset-db.sh --demo first.',
        );
        return;
      }
      expect(journeyResponse.statusCode, 200);
      final trainNumber = journeyResponse.data!['trainNumber'] as String;

      final store = _MemoryTokenStore(
        TokenSet(
          accessToken: accessToken,
          refreshToken: 'unused',
          accessTokenExpiry: DateTime.now().add(const Duration(minutes: 4)),
        ),
      );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authRepositoryProvider.overrideWithValue(
              AuthRepository(gateway: _NoBrowserGateway(), store: store),
            ),
          ],
          child: const OperatorApp(),
        ),
      );
      // The loading spinner never stops, so pumpAndSettle cannot be used: wait for the real journey read instead.
      for (
        var i = 0;
        i < 40 && find.textContaining('Train $trainNumber').evaluate().isEmpty;
        i++
      ) {
        await tester.pump(const Duration(milliseconds: 250));
      }
      expect(
        find.textContaining('Train $trainNumber'),
        findsWidgets,
        reason: "the app shows Nimal's real journey, train $trainNumber",
      );
      expect(
        find.text('Veyangoda'),
        findsWidgets,
        reason: 'the first stop of the real journey',
      );
    },
  );
}

class _MemoryTokenStore implements TokenStore {
  _MemoryTokenStore(this._tokens);
  TokenSet? _tokens;
  bool _started = false;

  @override
  Future<TokenSet?> read() async => _tokens;
  @override
  Future<void> write(TokenSet tokens) async => _tokens = tokens;
  @override
  Future<void> clear() async => _tokens = null;
  @override
  Future<bool> readSignInStarted() async => _started;
  @override
  Future<void> writeSignInStarted(bool started) async => _started = started;
}

/// The sign-in browser is not used: the token is handed over. Any refresh or sign-in would be a bug in this test.
class _NoBrowserGateway implements AuthGateway {
  @override
  Future<TokenSet> authorize() =>
      throw StateError('the browser sign-in is not used in this test');
  @override
  Future<TokenSet> refresh(String refreshToken) =>
      throw StateError('the token is fresh, no refresh expected');
  @override
  Future<void> endSession(String refreshToken) async {}
}
