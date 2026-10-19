# SmartRail passenger app

Flutter app for passengers (spec `_specs/10-passenger-app.md`). No account; everything it keeps is on the phone. Languages: English, Sinhala, Tamil (Sinhala and Tamil texts are unverified placeholders).

## Run on the Android emulator
1. Start the platform: from `smartrail-platform/`, `docker compose up --build`.
2. `adb reverse tcp:8080 tcp:8080`
3. From this folder: `flutter pub get`, then `flutter run`.

Another API address: `flutter run --dart-define=API_BASE_URL=http://host:8080`.

## Commands
- `dart run tool/generate_tokens.dart` regenerates `lib/core/theme/tokens.g.dart` from `../operator_app/smartrail-tokens.json` (CI fails if stale).
- `dart format lib test tool`, `flutter analyze --fatal-infos`, `flutter test`.

## Layout
`lib/core` (theme, strings, config), `lib/data` (API, models, preferences), `lib/domain` (rules: primary ETA, statuses, link state), `lib/app` (providers, polling, router), `lib/features` (screens).
