import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_appauth/flutter_appauth.dart';

import '../../../core/config.dart';
import '../domain/auth.dart';
import '../domain/auth_ports.dart';

/// [AuthGateway] on top of flutter_appauth (spike: `authorizeAndExchangeCode` works with these settings).
class AppAuthGateway implements AuthGateway {
  AppAuthGateway(
    this._config, [
    this._appAuth = const FlutterAppAuth(),
    Dio? http,
  ]) : _http = http ?? Dio();

  final AppConfig _config;
  final FlutterAppAuth _appAuth;
  final Dio _http;

  @override
  Future<TokenSet> authorize() async {
    try {
      final result = await _appAuth.authorizeAndExchangeCode(
        AuthorizationTokenRequest(
          AppConfig.clientId,
          AppConfig.redirectUrl,
          discoveryUrl: _config.discoveryUrl,
          scopes: AppConfig.scopes,
          promptValues: const ['login'],
          allowInsecureConnections: _config.allowInsecure,
        ),
      );
      return _toTokens(
        result.accessToken,
        result.refreshToken,
        result.idToken,
        result.accessTokenExpirationDateTime,
      );
    } on PlatformException catch (error) {
      // Closing the Custom Tab is reported as a cancellation (spike: "Sign-in cancelled").
      if (error.code == 'authorize_and_exchange_code_failed' ||
          error.code == 'user_cancelled') {
        throw const SignInCancelled();
      }
      // The code names the failure (for example a missing `adb reverse`) and never contains a token.
      debugPrint('Sign-in failed: ${error.code}');
      rethrow;
    }
  }

  @override
  Future<TokenSet> refresh(String refreshToken) async {
    try {
      final result = await _appAuth.token(
        TokenRequest(
          AppConfig.clientId,
          AppConfig.redirectUrl,
          discoveryUrl: _config.discoveryUrl,
          refreshToken: refreshToken,
          scopes: AppConfig.scopes,
          allowInsecureConnections: _config.allowInsecure,
        ),
      );
      return _toTokens(
        result.accessToken,
        result.refreshToken,
        result.idToken,
        result.accessTokenExpirationDateTime,
      );
    } on PlatformException catch (error) {
      // Keycloak says invalid_grant when the refresh token is expired or revoked; anything else is most likely the network.
      final text = '${error.message} ${error.details}'.toLowerCase();
      throw text.contains('invalid_grant')
          ? const RefreshRejected()
          : const RefreshUnavailable();
    }
  }

  @override
  Future<void> endSession(String refreshToken) async {
    // A direct call instead of flutter_appauth's endSession: that one opens a browser tab and fetches the server's
    // configuration with a native connection that refuses plain HTTP, which crashed the app inside Android code
    // where Dart cannot catch it (found on the emulator). Revoking the refresh token ends the Keycloak session.
    await _http.post<void>(
      '${_config.issuer}/protocol/openid-connect/logout',
      data: {'client_id': AppConfig.clientId, 'refresh_token': refreshToken},
      options: Options(
        contentType: Headers.formUrlEncodedContentType,
        validateStatus: (_) => true,
        sendTimeout: const Duration(seconds: 5),
        receiveTimeout: const Duration(seconds: 5),
      ),
    );
  }

  static TokenSet _toTokens(
    String? access,
    String? refresh,
    String? id,
    DateTime? expiry,
  ) {
    if (access == null || refresh == null) {
      throw const RefreshUnavailable();
    }
    return TokenSet(
      accessToken: access,
      refreshToken: refresh,
      idToken: id,
      accessTokenExpiry: expiry?.toUtc(),
    );
  }
}
