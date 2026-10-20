import 'dart:convert';

/// The tokens Keycloak gave us. They never go on screen, in a log, or anywhere but secure storage (spike rule S3).
class TokenSet {
  const TokenSet({
    required this.accessToken,
    required this.refreshToken,
    this.idToken,
    this.accessTokenExpiry,
  });

  final String accessToken;
  final String refreshToken;

  /// Needed only to end the Keycloak session when signing out.
  final String? idToken;
  final DateTime? accessTokenExpiry;

  TokenSet copyWith({
    String? accessToken,
    String? refreshToken,
    String? idToken,
    DateTime? accessTokenExpiry,
  }) => TokenSet(
    accessToken: accessToken ?? this.accessToken,
    refreshToken: refreshToken ?? this.refreshToken,
    idToken: idToken ?? this.idToken,
    accessTokenExpiry: accessTokenExpiry ?? this.accessTokenExpiry,
  );
}

/// Where the app stands with sign-in. The sign-in screens follow this and nothing else.
enum AuthStatus {
  /// Never signed in, or signed out on purpose.
  signedOut,

  signedIn,

  /// The refresh token is no longer accepted (it lasts 8 hours, AC-E01-01 1). Waiting reports are kept.
  sessionExpired,

  /// Android ended the app while the Keycloak page was open, so the result was lost (spike finding 5).
  signInInterrupted,

  /// The operator closed the Keycloak page without signing in.
  signInCancelled,

  /// Something else went wrong while signing in (no network, Keycloak down).
  signInFailed,
}

class AuthState {
  const AuthState(this.status, {this.displayName, this.userId});

  const AuthState.signedOut() : this(AuthStatus.signedOut);

  final AuthStatus status;

  /// For the header, for example "Nimal P.".
  final String? displayName;

  /// The Keycloak subject (the token's `sub`): who owns the reports on this phone.
  final String? userId;

  bool get isSignedIn => status == AuthStatus.signedIn;
}

/// The refresh token was refused for good: the operator has to sign in again.
class RefreshRejected implements Exception {
  const RefreshRejected();
}

/// Refreshing failed for another reason, most likely no network. The session is still valid.
class RefreshUnavailable implements Exception {
  const RefreshUnavailable();
}

class SignInCancelled implements Exception {
  const SignInCancelled();
}

/// The token's subject, or null when it cannot be read.
String? userIdFromToken(String accessToken) {
  try {
    final parts = accessToken.split('.');
    if (parts.length != 3) {
      return null;
    }
    final payload = jsonDecode(
      utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
    ) as Map<String, dynamic>;
    final sub = payload['sub'] as String?;
    return sub == null || sub.isEmpty ? null : sub;
  } on FormatException {
    return null;
  }
}

/// A short name for the header from the access token's claims: "Nimal P." from given and family name, otherwise the user name.
String displayNameFromToken(String accessToken) {
  try {
    final parts = accessToken.split('.');
    if (parts.length != 3) {
      return '';
    }
    final payload = jsonDecode(
      utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
    ) as Map<String, dynamic>;
    final given = (payload['given_name'] as String?)?.trim() ?? '';
    final family = (payload['family_name'] as String?)?.trim() ?? '';
    if (given.isNotEmpty) {
      return family.isEmpty ? given : '$given ${family[0]}.';
    }
    return (payload['preferred_username'] as String?) ?? '';
  } on FormatException {
    return '';
  }
}
