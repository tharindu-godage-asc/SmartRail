# SmartRail

A public railway passenger and operations platform (individual assessment project). It is an **information system, not a safety system**: it never controls trains or detects collisions.

| Part | Folder | What it is |
|---|---|---|
| Platform API | `smartrail-platform/` | .NET 10 modular monolith, Postgres, Keycloak, observability (Compose) |
| Controller web app and station displays | `web/ops/` | React 19 + TypeScript + Tailwind |
| Passenger app | `mobile/passenger_app/` | Flutter (Android emulator) |
| SmallBites (caterer, its own system) | `smallbites/` | .NET stand-in caterer: menu API, website, admin page (port 8090) |
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

The first run builds the images and takes a few minutes. The demo data (a journey for train 8058, eight more demo trains, with `nimal` assigned to 8102 because 8058 leaves at 08:20 in both directions, a night train and a train leaving within minutes) is created at every API start.

| What | Address | Sign in |
|---|---|---|
| API | http://localhost:8080 | public reads need none |
| Controller web app | http://localhost:5173 | `jayasinghe` / `jayasinghe-dev-only` |
| Station displays | http://localhost:5174/display/board/VYG and `/display/platform/VYG/1` | none |
| SmallBites admin (sell an item out) | http://localhost:8090/admin | none |
| Keycloak | http://localhost:8180 | `admin` / `admin-dev-only` |
| Grafana (SmartRail Overview) | http://localhost:3000 | none (anonymous) |

## Station codes

The Main Line has five stations. Codes are not case-sensitive in the API. Each link opens that station's departure board or a platform display (platform 1 is the one the demo trains use) once the stack is running.

| Code | Station | km from Colombo Fort | Station display | Platform 1 display | Train search to here |
|---|---|---|---|---|---|
| `CF` | Colombo Fort | 0.0 | [Board](http://localhost:5174/display/board/CF) | [Platform 1](http://localhost:5174/display/platform/CF/1) | [Trains to CF](http://localhost:8080/api/v1/trains?from=VYG&to=CF) |
| `MDA` | Maradana | 1.9 | [Board](http://localhost:5174/display/board/MDA) | [Platform 1](http://localhost:5174/display/platform/MDA/1) | [Trains to MDA](http://localhost:8080/api/v1/trains?from=VYG&to=MDA) |
| `RGM` | Ragama | 15.5 | [Board](http://localhost:5174/display/board/RGM) | [Platform 1](http://localhost:5174/display/platform/RGM/1) | [Trains to RGM](http://localhost:8080/api/v1/trains?from=VYG&to=RGM) |
| `GPH` | Gampaha | 27.5 | [Board](http://localhost:5174/display/board/GPH) | [Platform 1](http://localhost:5174/display/platform/GPH/1) | [Trains to GPH](http://localhost:8080/api/v1/trains?from=VYG&to=GPH) |
| `VYG` | Veyangoda | 37.5 | [Board](http://localhost:5174/display/board/VYG) | [Platform 1](http://localhost:5174/display/platform/VYG/1) | [Trains from VYG to CF](http://localhost:8080/api/v1/trains?from=VYG&to=CF) |

Useful API links (public, no sign-in): [all stations](http://localhost:8080/api/v1/stations), [line status](http://localhost:8080/api/v1/lines/MAIN/status), [board data for Veyangoda](http://localhost:8080/api/v1/stations/VYG/board), [API health](http://localhost:8080/health/ready). The caterer's menu for Colombo Fort (the only station with catering) is at [SmallBites](http://localhost:8090/api/menus/CF).

Other demo users: `nimal` (operator, assigned to 8058), `operator2` (operator, not assigned). All passwords are development-only.

## Step by step (without the script)

1. **Stack:** `cd smartrail-platform && docker compose up --build`.
2. **Web:** `cd web/ops && npm install && npm run dev`.
3. **Emulator:** start `Pixel_6`, then `adb reverse tcp:8080 tcp:8080` and `adb reverse tcp:8180 tcp:8180` and `adb reverse tcp:8090 tcp:8090` (the `localhost` forwarding keeps the Keycloak issuer the API accepts).
4. **Passenger app:** `cd mobile/passenger_app && flutter pub get && flutter run`. No sign-in. Try Veyangoda → Colombo Fort, open a train, tap Follow.
5. **Operator app:** `cd mobile/operator_app && flutter run`, sign in as `nimal`, report a delay on 8058, and watch it appear in the passenger app and the dashboard within about 30 seconds.

Reset the database: `cd smartrail-platform && docker compose down -v`, then start again.

## Tests and checks

| Part | Command (from the folder) |
|---|---|
| API | `smartrail-platform`: `dotnet build -warnaserror`, `dotnet test` (integration tests need Docker) |
| Web | `web/ops`: `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`; `npm run e2e` by hand against Compose |
| Mobile | `mobile/<app>`: `dart format lib test tool`, `flutter analyze --fatal-infos`, `flutter test` |

### The five critical end-to-end flows

These are the flows I automated as the most critical (QA section of the brief). They run in a real browser (Playwright, Chromium) against the running Docker Compose stack, by hand, not in CI.

**Before you run them**
1. Docker Desktop is running and the stack is up: `./start.sh` from the repo root (API on 8080, Keycloak on 8180, SmallBites on 8090, and the web app on 5173).
2. Once only, install the browser: `cd web/ops && npm install && npx playwright install chromium`.
3. It is between 01:00 and 23:00 Sri Lanka time. A train made for today cannot pass midnight, so the flows skip themselves outside that window.

**Run them** (from `web/ops`)
```bash
npm run e2e:critical                                   # flows 1 to 5, one after the other (about 40 seconds)
npx playwright test e2e/critical-flows.spec.ts         # flows 1, 2, 3 and 5
npx playwright test -g "flow 3"                        # one flow, by its name
npx playwright test e2e/critical-flows.spec.ts --headed   # watch the browser
npm run e2e                                            # every Playwright test, including the extra checks below
```

**Look at the results**
- The terminal prints each flow with `ok` or `x` and its time. A failure shows the line that failed.
- Screenshots and a trace of a failed run are written to `web/ops/test-results/`. Open one with `npx playwright show-trace test-results/<folder>/trace.zip`.
- For an HTML report of a run: `npx playwright test e2e/critical-flows.spec.ts --reporter=html`, then `npx playwright show-report`.

**What each run does to the data:** every flow schedules its own extra train for today (numbers 9xxx, with a random number, so run again on a 409), assigns Nimal to it, and releases him at the end. The flows can be repeated without resetting the database. Test trains stay in the database because events are never deleted.

| # | Flow | What it proves | File |
|---|---|---|---|
| 1 | Operator reports reach passengers | A departure, a delay and an arrival at an in-between stop appear in the public status and a station board; the reason is not public (NFR-04, D-10) | `e2e/critical-flows.spec.ts` |
| 2 | Verify a delay reason | The controller verifies in the web app and the passenger status then carries the reason and the time (FR-52) | `e2e/critical-flows.spec.ts` |
| 3 | Lost device | The controller ends an assignment in the web app and the operator is refused, even for an earlier claimed time (FR-12, FR-56) | `e2e/critical-flows.spec.ts` |
| 4 | Timetable | Schedule a service, find it, cancel one date, find it under Cancelled (FR-11) | `e2e/timetable.spec.ts` |
| 5 | Catering | An item sold out at SmallBites shows as sold out in SmartRail's menu; the caterer's token cannot read operations (FR-90, D-72) | `e2e/critical-flows.spec.ts` |

`e2e/controller-flow.spec.ts` (resolve an exception, no dashboard access for an operator, sign out) and `e2e/display.spec.ts` are extra checks. Two display tests depend on the demo trains, which the local database cleanup removed, so they can fail on a changed database.

### Test report (every test, pass or fail, and coverage)

`./scripts/test-report.sh` runs all suites with verbose output (each test name with PASS or FAIL in the terminal), measures line coverage and writes **`test-report/index.html`**: a summary card per suite, a filter box, a "failures only" switch, every test grouped by class or file with its time, and coverage by area (lowest first).

```bash
./scripts/test-report.sh                  # everything, with coverage (about 10 minutes; Docker must be running)
./scripts/test-report.sh --quick          # skip the slow .NET integration tests
./scripts/test-report.sh --no-coverage    # no coverage
./scripts/test-report.sh --only web       # one suite: unit | integration | smallbites | web | passenger | operator
```

Coverage comes from coverlet (.NET), vitest with v8 (web) and `flutter test --coverage`. The .NET unit and integration suites are reported separately, so the unit figure is low by design: the API and database code is exercised by the integration tests. The `test-report/` folder is git-ignored.

Design tokens come from one file, `mobile/operator_app/smartrail-tokens.json`; CI fails if the generated files are stale (`npm run tokens` in `web/ops`, `dart run tool/generate_tokens.dart` in each Flutter app).

## More

- Working rules, conventions and the full endpoint notes: `.claude/CLAUDE.md`.
- Delivery (GitHub Actions, Argo CD, kind cluster): `smartrail-platform/docs/workflows.md`, `smartrail-platform/docs/` ADRs; create the cluster with `smartrail-platform/scripts/bootstrap-cluster.sh`.
- Each app has its own README with details.
- Not built: see **Project status** below.

## Project status (20 October 2026)

The MVP is **not fully met**. This is what is built and what is not.

### What is built and working
- **Operator app:** reports for departures, arrivals and delays (an Arrived button at every stop but the first), offline queueing, My reports, and Previous journeys.
- **Live status:** public journey and train status, search, and line status.
- **Passenger app:** live screen, follow, offline labelling, the food menu, and the verified delay reason.
- **Station displays:** station boards and platform displays, including an Arrived screen.
- **Controller dashboard:** the exceptions queue, verifying a delay reason (FR-52), and the journey detail page with a one-track view.
- **Scheduling:** timetable creation, journeys generated 7 days ahead (FR-11), cancel and amend with versions.
- **Assignment:** assigning operators and ending an assignment, including the lost-device case (FR-12, FR-56).
- **Catering:** the read-only food menu from SmallBites (FR-90).

### What is not met
- **Must requirements not built:**
  - FR-53: the controller entering an event for an operator.
  - FR-54: suspending, cancelling or terminating a journey.
  - FR-55: disruption messages.
  - FR-75 and FR-76: push notifications.
  - NFR-13: rate limiting.
  - FR-07: caterer authentication (dropped by decision; SmartRail pulls the menu, D-72).
- **Should requirements not built:** FR-57 platform change, FR-59 line closure, FR-60 correcting a report, and FR-125 controller in charge.
- **Designed only:** food ordering and payment (D-71), ticketing, and the passenger design gallery.
- **Still open:**
  - Sinhala and Tamil text is unverified by a native speaker.
  - The keyboard-only and 44 px hand checks are not done.
  - The verify Playwright flow (`web/ops/e2e/verify.spec.ts`) was written but not run.
  - The Keycloak operator directory is not built (operator names come from configuration).
