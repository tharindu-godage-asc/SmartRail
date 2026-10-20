import 'package:clock/clock.dart';

import 'auth.dart';
import 'auth_ports.dart';

/// Sign-in, silent refresh and sign-out (AC-E01-01).
class AuthRepository {
  AuthRepository({required this.gateway, required this.store});

  final AuthGateway gateway;
  final TokenStore store;

  /// Refresh when the access token has less than this left, so a request never leaves with a token that dies on the way.
  static const refreshMargin = Duration(seconds: 30);

  TokenSet? _tokens;

  /// What to show when the app opens (spec section 3, "Session lost to process death").
  Future<AuthState> restore() async {
    final stored = await store.read();
    if (stored != null) {
      _tokens = stored;
      return AuthState(
        AuthStatus.signedIn,
        displayName: displayNameFromToken(stored.accessToken),
        userId: userIdFromToken(stored.accessToken),
      );
    }
    if (await store.readSignInStarted()) {
      // The flag is still set, so a sign-in began and its answer never came back: Android ended the app (spike finding 5).
      await store.writeSignInStarted(false);
      return const AuthState(AuthStatus.signInInterrupted);
    }
    return const AuthState.signedOut();
  }

  /// `prompt=login` is set by the gateway: the phone may be shared between operators, so an old Keycloak session
  /// is never reused silently (spike finding 4).
  Future<AuthState> signIn() async {
    await store.writeSignInStarted(true);
    try {
      final tokens = await gateway.authorize();
      await store.write(tokens);
      _tokens = tokens;
      return AuthState(
        AuthStatus.signedIn,
        displayName: displayNameFromToken(tokens.accessToken),
        userId: userIdFromToken(tokens.accessToken),
      );
    } on SignInCancelled {
      return const AuthState(AuthStatus.signInCancelled);
    } on Object {
      return const AuthState(AuthStatus.signInFailed);
    } finally {
      await store.writeSignInStarted(false);
    }
  }

  /// A token that is good for the next request, refreshed without asking the operator (AC-E01-01 3).
  /// Returns null when there is no session. Throws [RefreshRejected] when the session is over and [RefreshUnavailable]
  /// when it could not be checked (offline): the caller treats that like any other network failure.
  Future<String?> accessToken() async {
    final current = _tokens ?? await store.read();
    if (current == null) {
      return null;
    }
    _tokens = current;

    final expiry = current.accessTokenExpiry;
    if (expiry == null || expiry.isAfter(clock.now().add(refreshMargin))) {
      return current.accessToken;
    }
    return (await _refresh(current)).accessToken;
  }

  /// Forces a refresh after the API said 401 for a token we thought was fine.
  Future<String?> refreshNow() async {
    final current = _tokens ?? await store.read();
    return current == null ? null : (await _refresh(current)).accessToken;
  }

  Future<TokenSet> _refresh(TokenSet current) async {
    try {
      final fresh = await gateway.refresh(current.refreshToken);
      // Keycloak may rotate the refresh token; keep the old one only if no new one came.
      final merged = fresh.copyWith(
        refreshToken: fresh.refreshToken.isEmpty
            ? current.refreshToken
            : fresh.refreshToken,
        idToken: fresh.idToken ?? current.idToken,
      );
      await store.write(merged);
      _tokens = merged;
      return merged;
    } on RefreshRejected {
      // Tokens are cleared; the report queue lives elsewhere and is untouched (AC-E01-01 4).
      await store.clear();
      _tokens = null;
      rethrow;
    }
  }

  /// Ends the Keycloak session and forgets the tokens. Waiting reports stay on the phone.
  Future<void> signOut() async {
    final refreshToken = (_tokens ?? await store.read())?.refreshToken;
    // The phone forgets the session first, so nothing the network does afterwards (a failure, a hang, the app being
    // closed) can leave the operator signed in.
    await store.clear();
    _tokens = null;
    if (refreshToken == null) {
      return;
    }
    try {
      await gateway.endSession(refreshToken);
    } on Object {
      // Best effort: with no network the local sign-out has already happened.
    }
  }
}
