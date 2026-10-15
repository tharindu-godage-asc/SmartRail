# SmartRail operator app

Flutter app for train operators (Nimal): sign in, see the assigned journey, report `Departed <stop>`, and keep working
without a network. Spec: [`_specs/05-operator-app.md`](../../_specs/05-operator-app.md). Android emulator only.

## Run it against the Compose stack

1. From `smartrail-platform/`: `docker compose up --build` (API on 8080, Keycloak on 8180).
2. Start the emulator, then let it reach the PC as `localhost` (this keeps the Keycloak issuer the API accepts):
   `adb reverse tcp:8180 tcp:8180` and `adb reverse tcp:8080 tcp:8080`.
3. `flutter run` from this folder, and sign in as `nimal` (see `smartrail-platform/docker/keycloak/`).

Settings are `--dart-define`s, with defaults for the emulator: `API_BASE_URL` (`http://localhost:8080`),
`KEYCLOAK_ISSUER` (`http://localhost:8180/realms/smartrail`), `DESK_PHONE` (a placeholder number).

Plain HTTP is allowed to `localhost` in **debug builds only** (`android/app/src/debug`). Release builds need HTTPS.

## Where things are

```text
lib/
  app/        providers, router, app widget
  core/       config, strings (all text), time (Colombo zone, clock offset), theme/ (generated tokens), widgets/
  features/
    auth/     Keycloak sign-in (flutter_appauth), secure token storage, silent refresh
    journey/  assigned journey, public status, progress rules, screens
    reports/  offline queue (drift/SQLite), sender with backoff, delivery chip
```

Each feature has `domain/` (plain Dart rules and interfaces, tested without Flutter), `data/` (network, database, sign-in
library) and `presentation/` (Riverpod controllers and screens). Every outside thing is an interface that tests replace.

## Generated files (committed)

- `lib/core/theme/tokens.g.dart` comes from `smartrail-tokens.json`: `dart run tool/generate_tokens.dart`.
- `lib/features/reports/data/app_database.g.dart` comes from the drift tables: `dart run build_runner build`.

CI regenerates both and fails if they differ from what is committed.

## Checks

`flutter analyze` and `flutter test` (CI job `mobile`). Widget tests use a real font (Roboto from the SDK) so that
overflow at 200 % text size is caught.

## Known gaps (see the spec's Non-Goals)

Only departure reports can be sent. Arrival, delay sending, "flag my last report", the suspended state and the Reports tab
need backend work first. The Noto Sans font file is not bundled yet, so the platform font is used.
