/// Build-time settings (`--dart-define`). The default suits the Android emulator with `adb reverse tcp:8080 tcp:8080`, like the operator app.
class AppConfig {
  const AppConfig({
    this.apiBaseUrl = const String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://localhost:8080',
    ),
  });

  final String apiBaseUrl;

  /// How often each screen refreshes while it is open in the foreground (spec 10 rule 8). Not faster: nothing is rate-limited (NFR-13).
  static const liveInterval = Duration(seconds: 10);
  static const listInterval = Duration(seconds: 30);

  /// The food menu refreshes this often while its screen is open (spec 11: a sold-out item shows within 60 s).
  static const menuInterval = Duration(seconds: 60);

  /// After this long of failed polls the app says it is offline (NFR-03).
  static const staleAfter = Duration(seconds: 30);

  /// A followed train is dropped after this long, or when its journey is Completed (spec 10 rule 10).
  static const followLifetime = Duration(hours: 24);
}
