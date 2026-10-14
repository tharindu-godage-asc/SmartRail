import 'dart:convert';

import 'package:flutter_appauth/flutter_appauth.dart';
import 'package:http/http.dart' as http;

/// SPIKE CODE (spec android-signin-spike). Not product code: nothing here is imported by the future operator app.
/// Tokens are kept in memory only and are never printed, logged or shown (rule S3).
class AuthService {
  // On the emulator "localhost" reaches the PC through `adb reverse`, so the issuer is exactly the one the API accepts.
  static const issuer = 'http://localhost:8180/realms/smartrail';
  static const discoveryUrl = '$issuer/.well-known/openid-configuration';
  static const apiBase = 'http://localhost:8080';
  static const clientId = 'smartrail-operator-app';
  static const redirectUrl = 'com.smartrail.operator:/oauth2redirect';
  static const scopes = ['openid'];

  final FlutterAppAuth _appAuth = const FlutterAppAuth();

  String? _accessToken;
  String? _refreshToken;

  bool get signedIn => _accessToken != null;

  /// Opens Keycloak's login page in a Chrome Custom Tab and exchanges the code (PKCE) for tokens.
  Future<void> signIn() async {
    final result = await _appAuth.authorizeAndExchangeCode(
      AuthorizationTokenRequest(
        clientId,
        redirectUrl,
        discoveryUrl: discoveryUrl,
        scopes: scopes,
        promptValues: ['login'],
        allowInsecureConnections: true, // plain HTTP in Compose; debug builds only (rule S4)
      ),
    );
    _store(result.accessToken, result.refreshToken);
  }

  /// One refresh round trip with the refresh token.
  Future<void> refresh() async {
    final refreshToken = _refreshToken;
    if (refreshToken == null) {
      throw StateError('No refresh token: sign in first.');
    }
    final result = await _appAuth.token(
      TokenRequest(
        clientId,
        redirectUrl,
        discoveryUrl: discoveryUrl,
        refreshToken: refreshToken,
        scopes: scopes,
        allowInsecureConnections: true,
      ),
    );
    _store(result.accessToken, result.refreshToken ?? refreshToken);
  }

  void signOut() {
    // Local only: the spike does not call Keycloak's end-session endpoint.
    _accessToken = null;
    _refreshToken = null;
  }

  /// Subject, roles and expiry only, read from the token payload for display. The token itself is never exposed.
  Map<String, Object?> claimsSummary() {
    final token = _accessToken;
    if (token == null) {
      return {};
    }
    final payload = _decodePayload(token);
    final exp = DateTime.fromMillisecondsSinceEpoch((payload['exp'] as int) * 1000);
    final roles = (payload['roles'] as List?)?.map((role) => '$role').toList() ?? <String>[];
    return {'subject': payload['sub'], 'roles': roles, 'expires': exp.toLocal().toIso8601String(), 'audience': payload['aud']};
  }

  /// Calls the API with the access token and returns the status code and the response body (a subject and roles).
  Future<String> callWhoAmI() async {
    final token = _accessToken;
    if (token == null) {
      throw StateError('Sign in first.');
    }
    final response = await http.get(
      Uri.parse('$apiBase/api/v1/whoami'),
      headers: {'Authorization': 'Bearer $token'},
    );
    return '${response.statusCode} ${response.body}';
  }

  void _store(String? accessToken, String? refreshToken) {
    if (accessToken == null) {
      throw StateError('The sign-in finished without an access token.');
    }
    _accessToken = accessToken;
    _refreshToken = refreshToken;
  }

  static Map<String, Object?> _decodePayload(String jwt) {
    final part = jwt.split('.')[1];
    final json = utf8.decode(base64Url.decode(base64Url.normalize(part)));
    return jsonDecode(json) as Map<String, Object?>;
  }
}
