/// Build-time settings, passed with --dart-define (see README). Defaults suit the Android emulator with `adb reverse`,
/// so the Keycloak issuer is exactly the one the API accepts (CLAUDE.md, "The issuer rule").
class AppConfig {
  const AppConfig({
    this.apiBaseUrl = const String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://localhost:8080',
    ),
    this.issuer = const String.fromEnvironment(
      'KEYCLOAK_ISSUER',
      defaultValue: 'http://localhost:8180/realms/smartrail',
    ),
    // The number in the design is a placeholder; the real desk number is set per line when the app is built.
    this.deskPhone = const String.fromEnvironment(
      'DESK_PHONE',
      defaultValue: '+94 11 000 0000',
    ),
  });

  static const clientId = 'smartrail-operator-app';
  static const redirectUrl = 'com.smartrail.operator:/oauth2redirect';
  static const scopes = ['openid'];

  final String apiBaseUrl;
  final String issuer;
  final String deskPhone;

  String get discoveryUrl => '$issuer/.well-known/openid-configuration';

  /// Plain HTTP only exists in the local Compose stack (debug builds). Release builds never allow it.
  bool get allowInsecure => issuer.startsWith('http://');
}
