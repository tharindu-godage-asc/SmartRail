import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../domain/auth.dart';
import '../domain/auth_ports.dart';

/// Tokens in the Android keystore-backed secure storage (decision D1 of the spike). Nothing here is ever logged.
class SecureTokenStore implements TokenStore {
  SecureTokenStore([
    FlutterSecureStorage storage = const FlutterSecureStorage(),
  ]) : _storage = storage;

  final FlutterSecureStorage _storage;

  static const _access = 'access_token';
  static const _refresh = 'refresh_token';
  static const _id = 'id_token';
  static const _expiry = 'access_token_expiry';
  static const _started = 'sign_in_started';

  @override
  Future<TokenSet?> read() async {
    final access = await _storage.read(key: _access);
    final refresh = await _storage.read(key: _refresh);
    if (access == null || refresh == null) {
      return null;
    }
    final expiry = await _storage.read(key: _expiry);
    return TokenSet(
      accessToken: access,
      refreshToken: refresh,
      idToken: await _storage.read(key: _id),
      accessTokenExpiry: expiry == null ? null : DateTime.tryParse(expiry),
    );
  }

  @override
  Future<void> write(TokenSet tokens) async {
    await _storage.write(key: _access, value: tokens.accessToken);
    await _storage.write(key: _refresh, value: tokens.refreshToken);
    await _storage.write(key: _id, value: tokens.idToken);
    await _storage.write(
      key: _expiry,
      value: tokens.accessTokenExpiry?.toUtc().toIso8601String(),
    );
  }

  @override
  Future<void> clear() async {
    for (final key in [_access, _refresh, _id, _expiry]) {
      await _storage.delete(key: key);
    }
  }

  @override
  Future<bool> readSignInStarted() async =>
      await _storage.read(key: _started) == '1';

  @override
  Future<void> writeSignInStarted(bool started) => started
      ? _storage.write(key: _started, value: '1')
      : _storage.delete(key: _started);
}
