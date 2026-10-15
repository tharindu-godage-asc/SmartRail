import 'auth.dart';

/// The sign-in library behind an interface, so the refresh and restore rules can be tested without Android.
abstract interface class AuthGateway {
  /// Opens Keycloak's page (Authorization Code with PKCE) and trades the code for tokens.
  /// Throws [SignInCancelled] when the page is closed.
  Future<TokenSet> authorize();

  /// Throws [RefreshRejected] when Keycloak refuses the refresh token, [RefreshUnavailable] for anything else.
  Future<TokenSet> refresh(String refreshToken);

  /// Ends the Keycloak session. Best effort: failures are ignored by the caller.
  Future<void> endSession(String? idToken);
}

/// Secure storage for the tokens, plus the "sign-in started" flag that detects a result lost to process death.
abstract interface class TokenStore {
  Future<TokenSet?> read();
  Future<void> write(TokenSet tokens);
  Future<void> clear();

  Future<bool> readSignInStarted();
  Future<void> writeSignInStarted(bool started);
}
