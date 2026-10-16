# SmartRail

A public railway passenger and operations platform (individual assessment project). It is an **information system, not a safety system**: it never controls trains or detects collisions.

| Part | Folder | What it is |
|---|---|---|
| Platform API | `smartrail-platform/` | .NET 10 modular monolith, Postgres, Keycloak, observability (Compose) |
| Controller web app and station displays | `web/ops/` | React 19 + TypeScript + Tailwind |
| Passenger app | `mobile/passenger_app/` | Flutter (Android emulator) |
| Operator app | `mobile/operator_app/` | Flutter (Android emulator) |
| Specs, requirements | `_specs/`, `docs/` | One spec per feature slice |

## Quick start

Needs: Docker Desktop (running), Git Bash, Node 22+, Flutter, Android SDK with an emulator named `Pixel_6`, .NET 10 SDK (only to build or test the API by hand).

```bash
./start.sh                # Compose stack + controller web app
./start.sh --emulator     # ... plus the emulator and the passenger app
./start.sh --operator     # ... the operator app instead
./start.sh --stop         # stop everything (data is kept)
```

The first run builds the images and takes a few minutes. The demo data (a journey for train 8058, eight more demo trains in both directions, a night train and a train leaving within minutes) is created at every API start.

| What | Address | Sign in |
|---|---|---|
| API | http://localhost:8080 | public reads need none |
| Controller web app | http://localhost:5173 | `jayasinghe` / `jayasinghe-dev-only` |
| Station displays | http://localhost:5174/display/board/VYG and `/display/platform/VYG/1` | none |
| Keycloak | http://localhost:8180 | `admin` / `admin-dev-only` |
| Grafana (SmartRail Overview) | http://localhost:3000 | none (anonymous) |

Other demo users: `nimal` (operator, assigned to 8058), `operator2` (operator, not assigned). All passwords are development-only.

## Step by step (without the script)

1. **Stack:** `cd smartrail-platform && docker compose up --build`.
2. **Web:** `cd web/ops && npm install && npm run dev`.
3. **Emulator:** start `Pixel_6`, then `adb reverse tcp:8080 tcp:8080` and `adb reverse tcp:8180 tcp:8180` (the `localhost` forwarding keeps the Keycloak issuer the API accepts).
4. **Passenger app:** `cd mobile/passenger_app && flutter pub get && flutter run`. No sign-in. Try Veyangoda → Colombo Fort, open a train, tap Follow.
5. **Operator app:** `cd mobile/operator_app && flutter run`, sign in as `nimal`, report a delay on 8058, and watch it appear in the passenger app and the dashboard within about 30 seconds.

Reset the database: `cd smartrail-platform && docker compose down -v`, then start again.

## Tests and checks

| Part | Command (from the folder) |
|---|---|
| API | `smartrail-platform`: `dotnet build -warnaserror`, `dotnet test` (integration tests need Docker) |
| Web | `web/ops`: `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`; `npm run e2e` by hand against Compose |
| Mobile | `mobile/<app>`: `dart format lib test tool`, `flutter analyze --fatal-infos`, `flutter test` |

Design tokens come from one file, `mobile/operator_app/smartrail-tokens.json`; CI fails if the generated files are stale (`npm run tokens` in `web/ops`, `dart run tool/generate_tokens.dart` in each Flutter app).

## More

- Working rules, conventions and the full endpoint notes: `.claude/CLAUDE.md`.
- Delivery (GitHub Actions, Argo CD, kind cluster): `smartrail-platform/docs/workflows.md`, `smartrail-platform/docs/` ADRs; create the cluster with `smartrail-platform/scripts/bootstrap-cluster.sh`.
- Each app has its own README with details.
- Not built: push notifications, food ordering (designed only), rate limiting.
