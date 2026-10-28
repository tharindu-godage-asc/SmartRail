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

**Only the screens** (when the backend is already running): `./scripts/start-ui.sh` starts the Operations Dashboard (Vite dev server on 5173) and makes sure the station displays (port 5174) are up, then prints every address, including all five main boards and platform displays. Options: `--open` opens the dashboard and a board in the browser, `--emulator` starts the Pixel_6 emulator and forwards ports 8080, 8180 and 8090 (without this the apps cannot reach the API or sign in), `--apps` also installs the passenger and operator apps, `--stop` stops the dashboard dev server.

The first run builds the images and takes a few minutes. The demo data (a journey for train 8058, eight more demo trains, with `nimal` assigned to 8102 because 8058 leaves at 08:20 in both directions, a night train and a train leaving within minutes) is created at every API start.

| What | Address | Sign in |
|---|---|---|
| API | http://localhost:8080 | public reads need none |
| Controller web app | http://localhost:5173 | `jayasinghe` / `jayasinghe-dev-only` |
| Station displays | http://localhost:5174/display/board/VYG and `/display/platform/VYG/1` | none |
| SmallBites admin (sell an item out) | http://localhost:8090/admin | none |
| Keycloak | http://localhost:8180 | `admin` / `admin-dev-only` |
| Grafana (SmartRail Overview) | http://localhost:3000 | none (anonymous) |
| API explorer (Swagger UI, Compose only) | http://localhost:8080/swagger | none to open it. To call a protected route click **Authorize** and paste a token from `smartrail-platform/scripts/get-token.sh nimal` (or `jayasinghe`) |
| API description (OpenAPI JSON, Compose only) | http://localhost:8080/openapi/v1.json | none (both are switched on by `OpenApi__Enabled`, off everywhere else) |
| Argo CD (the delivery cluster, see below) | https://localhost:8443 after a port-forward | `admin` / the password in the cluster secret (see below) |

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

## Argo CD and the delivery cluster

Besides Docker Compose there is a local Kubernetes cluster (kind, context `kind-smartrail`) where **Argo CD** deploys the API to four environments, one namespace each: `smartrail-dev`, `smartrail-test`, `smartrail-staging` and `smartrail-prod`, plus `observability`. CI only edits the image tag in `smartrail-platform/deploy/overlays/<env>`; Argo CD pulls the change in. Prod changes only through the manual `promote-prod` workflow and a manual sync (`smartrail-platform/docs/workflows.md`). The cluster has no database yet and no Keycloak, so the cluster API runs without the database features; the full system is the Compose stack above.

**Create or repair the cluster** (Git Bash, safe to re-run): `./smartrail-platform/scripts/bootstrap-cluster.sh`. It needs Docker Desktop, `kind` and `kubectl`.

**Check it without a browser**
```bash
kubectl config current-context                 # kind-smartrail
kubectl get applications -n argocd             # the five apps: SYNC STATUS Synced, HEALTH STATUS Healthy
kubectl get pods -A                            # the Argo CD pods and one smartrail-api pod per environment
```

**Open the Argo CD web page**
1. Leave this running in a terminal (the UI is not published to your machine until you do):
   ```bash
   kubectl -n argocd port-forward svc/argocd-server 8443:443
   ```
2. Open https://localhost:8443 in Chrome or Edge. Argo CD uses a self-signed certificate, so the browser warns: click **Advanced**, then **Proceed to localhost**. (If there is no proceed link, click the page and type `thisisunsafe`.) VS Code's built-in browser cannot get past the warning.
3. Sign in as **`admin`**. The password is generated when Argo CD is installed and is **not stored in this repository** (a secret must not be committed). Read it from the cluster:
   ```bash
   kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
   ```
   If that secret is gone, the password was changed or the secret deleted: reset it with `kubectl -n argocd patch secret argocd-secret -p '{"stringData":{"admin.password":"","admin.passwordMtime":""}}'` and restart `argocd-server`, or ask for a new install with `bootstrap-cluster.sh`.

**Without the certificate warning (optional, local cluster only).** This changes the Argo CD server setting so it serves plain HTTP:
```bash
kubectl -n argocd patch configmap argocd-cmd-params-cm --type merge -p '{"data":{"server.insecure":"true"}}'
kubectl -n argocd rollout restart deployment argocd-server
kubectl -n argocd port-forward svc/argocd-server 8081:80      # then open http://localhost:8081
```
Do not do this in a real environment.

**What to look at:** the apps `smartrail-dev`, `-test`, `-staging`, `-prod` and `smartrail-observability`, each Synced and Healthy. Open `smartrail-dev` to see its Deployment and pod, and its image tag: it is the Git commit of the last `main` build, from the commit named `deploy: dev, test, staging <sha>` that CI pushed.

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
npx playwright test e2e/critical-flows.spec.ts         # flows 1, 2 and 3 (and the extra catering check)
npx playwright test e2e/controller-flow.spec.ts       # flow 4 (and three extra controller checks)
npx playwright test e2e/timetable.spec.ts              # flow 5
npx playwright test -g "flow 3"                        # one flow, by its name
npx playwright test e2e/critical-flows.spec.ts --headed   # watch the browser
npm run e2e                                            # every Playwright test, including the extra checks below
```

**Watch them slowly (headed, for a demo)** (from the repo root)
```bash
./scripts/e2e-critical-demo.sh                # the five flows in a visible browser, 700 ms after every action
./scripts/e2e-critical-demo.sh --slow 1500    # slower
./scripts/e2e-critical-demo.sh --flow 4       # one flow only (1 to 5)
./scripts/e2e-critical-demo.sh --reset        # afterwards, reset the database to its initial state
```
Many steps call the API directly, so the browser is only busy on the controller screens. The pause is the `SLOWMO` setting of `web/ops/playwright.config.ts`.

**Start from a clean database, and clean up afterwards** (from the repo root)
```bash
./scripts/reset-db.sh          # removes the Compose containers AND volumes, starts the stack again: the initial state (DELETES local data)
./scripts/reset-db.sh --demo   # the same with the demo layer on (journey 8058 for Nimal, 8070), which the display tests need
./scripts/qa.sh --e2e-only     # the browser flows with one PASS/FAIL table, then resets the database (--no-reset keeps the test data)
```
The five critical flows do not need the demo layer. `e2e/display.spec.ts` does: run `./scripts/reset-db.sh --demo` first, otherwise its delay and next-trains tests skip with a message. Two display tests also depend on the time of day (the main board test runs 08:00 to 13:00 Sri Lanka time only).

**Look at the results**
- The terminal prints each flow with `ok` or `x` and its time. A failure shows the line that failed.
- Screenshots and a trace of a failed run are written to `web/ops/test-results/`. Open one with `npx playwright show-trace test-results/<folder>/trace.zip`.
- For an HTML report of a run: `npx playwright test e2e/critical-flows.spec.ts --reporter=html`, then `npx playwright show-report`.

**What each run does to the data:** every flow schedules its own extra train for today (numbers 9xxx, with a random number, so run again on a 409), assigns **Second Operator** (`operator2`, who the demo seeder never assigns) to it, and releases them at the end. Nimal is not used: he is the demo operator of journey 8058, and an operator cannot be on two journeys at once. If the random number already exists, another is tried. Flows 1 and 2 also run their train to the end so it leaves the station boards. The flows can be repeated without resetting the database. Test trains stay in the database because events are never deleted.

| # | Flow | What it proves | File |
|---|---|---|---|
| 1 | Operator reports reach passengers | A departure, a delay and an arrival at an in-between stop appear in the public status and a station board; the reason is not public (NFR-04, D-10) | `e2e/critical-flows.spec.ts` |
| 2 | Verify a delay reason | The controller verifies in the web app and the passenger status then carries the reason and the time (FR-52) | `e2e/critical-flows.spec.ts` |
| 3 | Lost device | The controller ends an assignment in the web app and the operator is refused, even for an earlier claimed time (FR-12, FR-56) | `e2e/critical-flows.spec.ts` |
| 4 | Controller resolves a fault report | A Technical fault delay report reaches the controller's exceptions queue and the controller resolves it (spec 07, spec 08) | `e2e/controller-flow.spec.ts` |
| 5 | Scheduling a train | A controller schedules a service, finds it in the list, cancels one date and finds it under Cancelled (FR-11, specs 13 to 15) | `e2e/timetable.spec.ts` |

The other controller checks (`e2e/controller-flow.spec.ts`: the overview, no dashboard access for an operator, sign out), the catering check (`e2e/critical-flows.spec.ts`: an item sold out at SmallBites shows as sold out in SmartRail's menu, and the caterer's token cannot read operations, FR-90 and D-72) and `e2e/display.spec.ts` are extra checks. Two display tests depend on the demo trains, which the local database cleanup removed, so they can fail on a changed database.

### Flutter integration tests (the `integration_test` package)

Each phone app has one integration test in `integration_test/`. They run on the Android emulator against the **real** stack (no fakes), so they prove the app and the API work together. The faster widget tests in `test/` (with fakes) are separate and run in CI.

| App | Test | What it proves |
|---|---|---|
| Passenger | `mobile/passenger_app/integration_test/passenger_search_test.dart` | The real station list loads, a passenger picks Veyangoda and Colombo Fort, and the real API's trains are listed. Reads only. |
| Operator | `mobile/operator_app/integration_test/operator_journey_test.dart` | With a real Keycloak token for Nimal, the app shows the journey the real API assigns to him. Reads only. |

```bash
./scripts/reset-db.sh --demo                  # a clean stack with the demo layer (the operator test needs a journey for Nimal)
./start.sh --emulator                 # or any running Android emulator
./scripts/mobile-integration.sh               # both apps; or: ./scripts/mobile-integration.sh passenger
```
The script checks the stack and the emulator, sets `adb reverse` for ports 8080 and 8180, and runs `flutter test integration_test --device-id <emulator>` in each app. The first build of each app takes a few minutes. If Nimal has no journey the operator test skips itself with a message.

The browser sign-in of the operator app cannot be driven by the package, so that test takes a real access token from Keycloak (the Compose-only `smartrail-dev-token` client) and gives it to the app as if the sign-in had happened. Sign-in itself is covered by the Android sign-in spike and the app's unit tests. They are not in CI (they need an emulator).

### Automated QA: one command, and every night on GitHub

**On your machine:** `./scripts/qa.sh` starts the stack if it is down (`./start.sh`), runs every suite (`./scripts/test-report.sh`: unit, integration, SmallBites, web, both Flutter apps), then the browser flows, and ends with one table:

```
QA result  2026-10-22 08:43
PASS  412s      Suites: unit, integration, SmallBites, web, passenger app, operator app
PASS  52s       Browser flows: critical flows, timetable and controller (Playwright)
```

It exits with a non-zero code if any step failed. Options: `--quick` (skip the slow .NET integration tests), `--e2e-only`, `--no-e2e`, `--no-coverage`. Results: `test-report/index.html`, `test-report/qa-summary.txt`, and for the browser flows `web/ops/playwright-report/` (open with `npx playwright show-report` from `web/ops`) with a trace for each failure. The browser flows alone: `npm run e2e:qa` from `web/ops` (the three flow files, nine tests).

**On GitHub:** the workflow `.github/workflows/qa.yml` (`qa`) starts the whole Compose stack on a runner, waits for the API, Keycloak and SmallBites, installs Chromium and runs `npm run e2e:qa`. It runs every night at 02:00 UTC and from the Actions tab (Run workflow). It uploads the Playwright report and traces as an artifact and prints the container logs when something failed. It is not part of the pull request checks (it needs the full stack): the pull request checks are `ci`, `web`, `mobile` and `smallbites`. This workflow has not been run on GitHub yet; the same steps were run locally.

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
