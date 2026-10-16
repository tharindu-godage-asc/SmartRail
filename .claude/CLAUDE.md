# SmartRail

SmartRail is a public railway passenger and operations platform (individual assessment, 29 Sep to 20 Oct 2026). It is an **information system, not a safety system**: it never controls trains or detects collisions.

## Stack
- Backend: .NET 10, modular monolith, feature folders, Clean Architecture (Domain, Application, Infrastructure, Api)
- Web (controller dashboard, platform displays): React
- Mobile (passenger app, operator app): Flutter, tested on an Android emulator
- Data: Postgres, EF Core migrations. Identity: Keycloak (ADR-08)
- Delivery: Docker, GitHub Actions, Argo CD on a local kind cluster (ADR-15). Environments as built:
  - `local`: Docker Compose on the owner's machine (`deployment_environment="local"`).
  - `dev`, `test`, `staging`, `prod`: one namespace each (`smartrail-<env>`) in the kind cluster, one Argo CD app each.
  - CI deploys every `main` build to dev, test and staging. Prod only changes through the manual `promote-prod` workflow plus a manual Argo CD sync. Details: `smartrail-platform/docs/workflows.md`.
- Identity: Keycloak in Compose only for now (not yet in the cluster).
- Observability: OpenTelemetry to Prometheus, Loki, Tempo, Grafana (Compose stack; the in-cluster collector forwards to it). Dashboard: SmartRail Overview.

## Where things are
- `_specs/` (repo root) one spec per feature slice. Start from `_specs/_template.md`. Read the spec before coding.
- **Requirements** (repo root): `tools/stakeholder-sim/context/smartrail-discovery.md` is the single discovery document. Read the relevant section before coding:
  - Assumptions (A-xx): section 2. Actors and MVP status: section 4. Design principles: section 5.
  - Live data, ETA, confidence and staleness: section 6. Event validation rules: section 8. Overrides and disruptions: section 9.
  - Catering: section 10. Station experience and accessibility: section 11. Component scope: section 12.
  - Epics, thin slices and decisions (D-xx): section 14. MoSCoW: section 15. Build order: section 17.
  - Architecture and ADR references: section 18. UX wireframes: section 19. Design system and status groups: section 20.
  - **Edge cases and open questions: section 23.** Anything marked *Assumption* or *Open* there is not confirmed; ask before relying on it.
  - The requirements (FR-/NFR-), acceptance criteria (AC-) and edge cases (EC-) are in `docs/`: `smartrail-refined-requirements.md`, `smartrail-acceptance-criteria.md`, `smartrail-edge-cases.md` (also `smartrail-assumptions.md`, `smartrail-discovery-register.md`). Specs must quote the criteria they use; if one a spec needs is missing, stop and ask.
- `smartrail-platform/` the .NET platform. Everything below is relative to it:
  - `src/` (Domain, Application, Infrastructure, Api), `tests/` (UnitTests, IntegrationTests), `SmartRail.sln`
  - `docs/` ADRs (ADR-15 delivery, ADR-16 mobile), `workflows.md`, `branch-protection.md`
  - `deploy/` Kustomize base and overlays, Argo CD apps, kind config, in-cluster collector
  - `observability/` collector, Prometheus, Tempo and Grafana config; `scripts/` `bootstrap.sh`, `bootstrap-cluster.sh`
  - `docker/postgres/init/` SQL that Compose and the integration tests both run: roles, the `keycloak` database, default privileges
  - `docker/keycloak/` the Keycloak realm file (source of truth); `scripts/get-token.sh`, `scripts/smoke-auth.sh`
- `.github/` workflows and Dependabot config (repo root, not under `smartrail-platform/`).
- `tools/stakeholder-sim/` the stakeholder simulation tool (Node).
- `web/ops/` the controller web app (spec `_specs/08-controller-web.md`): Vite, React 19, TypeScript, Tailwind 4, TanStack Query, `oidc-client-ts`. README inside. From that folder: `npm run dev` (http://localhost:5173, fixed port), `npm run tokens` (regenerate `src/styles/tokens.css` from `mobile/operator_app/smartrail-tokens.json`; CI fails if stale), `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, `npm run e2e` (Playwright against Compose, by hand, not in CI). Workflow `.github/workflows/web.yml`.

## Commands
Run from `smartrail-platform/` unless noted.
- Build: `dotnet build -warnaserror`
- Test: `dotnet test`
- Run local stack: `docker compose up --build` (API on http://localhost:8080, Grafana on http://localhost:3000)
- Create or repair the cluster: `./scripts/bootstrap-cluster.sh` (Git Bash; safe to re-run)
- Database (EF Core, Postgres). Restore the pinned tool once: `dotnet tool restore`. The tool commands use `--project src/SmartRail.Infrastructure --startup-project src/SmartRail.Api`:
  - Add a migration: `dotnet ef migrations add <Name> --project src/SmartRail.Infrastructure --startup-project src/SmartRail.Api --output-dir Persistence/Migrations`
  - Apply to the Compose database: `dotnet ef database update --project src/SmartRail.Infrastructure --startup-project src/SmartRail.Api` (uses the admin connection from `SMARTRAIL_MIGRATIONS_CONNECTION`, default is the Compose admin user)
  - Print the SQL: `dotnet ef migrations script --project src/SmartRail.Infrastructure --startup-project src/SmartRail.Api -o migration.sql`
  - Undo the last migration (only if it is not applied or merged yet): `dotnet ef migrations remove --project src/SmartRail.Infrastructure --startup-project src/SmartRail.Api`
  - Never edit a merged migration; add a new one. Each slice adds its own tables in its own migration. A new table is usable by the API role automatically (default privileges); if it must be append-only or read-only, revoke explicitly in that migration and test it.
  - Two settings control the database at startup, both off by default: `Database:ApplyMigrationsOnStartup` (apply migrations as the admin user) and `Seeding:DemoData` (today's demo journey 8058). Only Docker Compose turns them on (`Database__ApplyMigrationsOnStartup`, `Seeding__DemoData`); the cluster and prod never do. With no `ConnectionStrings:Smartrail` the API runs without a database.
  - Reset the local database: `docker compose down -v`, then `docker compose up --build` (the init scripts in `docker/postgres/init/` run again).

- Identity (Keycloak, spec `_specs/02-keycloak-identity.md`). Compose runs Keycloak 26.7.5 in development mode: console http://localhost:8180 (user `admin`, password `admin-dev-only`), realm `smartrail`, database `keycloak`. Development-only passwords throughout.
  - The realm file `docker/keycloak/realm-smartrail.json` is the source of truth (roles, clients, demo users). It is imported only when the realm does not exist, so after editing it run `docker compose down -v` and `up`. The demo user IDs equal `DemoIds` in `Seeding/DemoIds.cs`; a test checks it.
  - Demo users: `nimal` (operator, assigned to the demo journey), `operator2` (operator, never assigned), `jayasinghe` (controller), and the `smartrail-caterer` client (client credentials).
  - Get a token: `scripts/get-token.sh nimal` (also `operator2`, `jayasinghe`, `caterer`). Example: `curl -H "Authorization: Bearer $(scripts/get-token.sh nimal)" http://localhost:8080/api/v1/whoami`. The `smartrail-dev-token` client behind it is Compose-only and must never be in another realm.
  - Check the real Keycloak by hand before merging identity changes: `scripts/smoke-auth.sh` (not in CI). The 403 cases are proved by the integration tests, which use their own test token issuer.
  - Export the live realm to compare with the file (the file is hand-kept and much smaller than an export): `docker compose exec -T keycloak /opt/keycloak/bin/kc.sh export --dir /tmp/export --realm smartrail --users realm_file --http-port=18080 --http-management-port=19000`, then `docker compose cp keycloak:/tmp/export/smartrail-realm.json .`. The extra ports avoid a clash with the running server.
  - **The issuer rule:** a token's `iss` is the address Keycloak was reached at. Compose fixes it with `KC_HOSTNAME=http://localhost:8180`, the API accepts only that issuer (`Authentication:Authority`), and fetches keys from `Authentication:MetadataAddress` (`http://keycloak:8080/...`, thanks to `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`). Always fetch tokens from `localhost:8180`. The Android emulator reaches the host as `10.0.2.2`; that is for the sign-in spike.
  - API settings (section `Authentication`): `Authority`, `Audience`, `MetadataAddress`, `RequireHttpsMetadata`, `ClockSkewSeconds` (30). With none set (the cluster today) protected endpoints answer 401 and public ones work; setting only some stops startup with "half configured". `Diagnostics:WhoAmI` (off by default, on in Compose) maps `GET /api/v1/whoami`.
  - **Secure by default:** every endpoint requires a signed-in user unless it is marked `.AllowAnonymous()`. The public list is `/api/v1/status`, `/health/live`, `/health/ready`, plus the five passenger reads `/api/v1/journeys/{journeyId}/status`, `/api/v1/trains/{trainNumber}/status`, `/api/v1/stations`, `/api/v1/trains` and `/api/v1/lines/{code}/status`; a test fails if anything else is anonymous. Policies `Operator`, `Controller`, `Caterer` come from the flat `roles` claim.

- Operator report and live status (slice 1, spec `_specs/03-operator-report-live-status.md`). Endpoints, all under `/api/v1/` and mapped **only when `ConnectionStrings:Smartrail` is set**, so the cluster (no database yet) does not have them:
  - `GET operator/journey` (role operator): the journey the operator is assigned to now, with stop IDs, scheduled UTC instants and `isArrivalStation` (true for designated stops and the final stop); 404 when none.
  - `POST journeys/{journeyId}/events` (role operator): body `{ clientEventId, eventType: "Departed" | "Arrived" | "DelayReported", serviceStopId, occurredAt }` (spec 06). A stop report needs `serviceStopId` and nothing else. A **delay report** has no stop and needs `reason` (`SignalIssue`, `TechnicalFault`, `Obstruction`, `Weather`, `Unknown`) and exactly one of `totalDelayMinutes` (1 to 240, the TOTAL against the timetable, never added to the delay running) or `delayUnknown: true`; a total not greater than the delay in force is 409 `delay_not_greater` (after "unknown", equal to the last number is allowed). The reason is stored in the event payload and **never returned by any read** (D-10). "Unknown" sets `delayKnown: false` in the status read, with no estimated times and no `primaryEta`. No two reports of one journey may share an `occurredAt` (409 `order_conflict`). An arrival is accepted only at a designated stop or the final stop (422 `not_arrival_station`); an arrival at the final stop sets the journey `Completed`. 201 recorded, 200 repeat (same client event ID, or a different one for the same stop and time), 403 not assigned at `occurredAt`, 404 unknown journey, 409 conflict (journey Unassigned or Completed, same ID with different content, a second different time for a stop, impossible stop order, version conflict after 3 tries), 422 bad request or time outside the limits. The error body carries a stable `reason` code.
  - `GET journeys/{journeyId}/status` and `GET trains/{trainNumber}/status` (public). The train lookup returns the journey in progress (started, not Completed, not past its expected end plus the grace period), otherwise today's (even if Completed), otherwise 404 (rule R15; a night train stays visible after midnight). The status gains `actualArrival` per stop, `lastKnownPosition.state` ("Arrived" or "Departed") and `statusKey` `Completed`.
  - Settings (all have defaults): `Events:MaxFutureMinutes` (1), `Events:MaxAgeHours` (8), `Journeys:OnTimeToleranceMinutes` (2), `Journeys:VisibleAfterEndMinutes` (15, and it stays 15: a `Completed` journey leaves "in progress" by its status, spec 06 R11; 0 would hide a train that runs later than its last estimate).
  - Rules worth remembering: departure times must strictly increase with stop sequence; delay is rounded away from zero, then never negative; stop states are rebuilt from all events in occurrence order after every report; stop times are plain local times, so `ScheduleInstants` places stops after midnight on the next day (journeys up to 24 hours). Times are stored to the microsecond, so the handler trims incoming times to microseconds (a repeat must equal the stored value).
  - Try it on the Compose stack: `curl -H "Authorization: Bearer $(scripts/get-token.sh nimal)" http://localhost:8080/api/v1/operator/journey`, then POST a report with a time up to a minute ahead and up to 8 hours back; read it back with `curl http://localhost:8080/api/v1/trains/8058/status`.
  - Logging from a request or token: pass values through `LogSafe.Clean` (CodeQL flags log forging). Use typed values (GUIDs) where possible.
  - Integration tests: one fresh database per test (`JourneyApiTestBase`), the clock is a `FakeTimeProvider` you set per test, and each test clears its Npgsql pools (otherwise ~100 databases exhaust Postgres connections).

- Passenger reads (spec `_specs/04-passenger-find-trains.md`), all public, read-only, under `/api/v1/` and mapped **only when a database is configured** (so absent in the cluster today):
  - `GET stations`: the five stations in line order, **starting at Colombo Fort**, each with English, Sinhala and Tamil names and line sequences, plus `version`. `version` is also the `ETag`; `If-None-Match` with one tag, several, weak (`W/`) or `*` gets 304. `Cache-Control: no-cache`.
  - `GET trains?from=VYG&to=CF&date=2026-10-12`: trains between two stations (codes are case-insensitive: `CF`, `MDA`, `RGM`, `GPH`, `VYG`). `date` defaults to today and may be up to 7 days ahead (422 `date_out_of_range` otherwise). Each train has `atFrom.arrival`, `atFrom.departure` and `atTo.arrival` (each `scheduled` and `estimated`; `atFrom.arrival` is null at a service's first stop), `delayMinutes`, `statusGroup`, `statusKey`, `version`, `lastEventAt`. `live: false` means the answer is from the timetable (no journey yet) and the estimates, delay, version and `lastEventAt` are null. Errors: 404 `station_not_found`, 422 `invalid_request`, `same_station`, `invalid_date`, `date_out_of_range`. An empty list is a normal 200 answer (also for the opposite direction, because the seed has Inbound trains only).
  - `GET lines/MAIN/status`: `statusKey` `Normal` or `SomeDelayed`, `journeysInProgress`, `delayedJourneys`, `lastEventAt` (how fresh the data is) and `serverTime` (when computed; never show it as "updated"). No English text: the client words it, and with 0 journeys in progress says "No trains running now". `GET /api/v1/status` is unchanged.
  - `trains` and `lines` send `Cache-Control: public, max-age=5`: a deliberate approximation of OPS-10 (it bounds staleness, it does not invalidate on a version change).
  - Settings: `Search:MaxDaysAhead` (7), `Search:MaxResults` (50); the grace period is the one shared `Journeys:VisibleAfterEndMinutes`.
  - Which trains: live journeys not Completed, not over (expected end plus the grace period) and not yet departed From (a report at From or any later stop means it left); a service with no journey row for the date comes from the timetable (`runs_on` of Daily, Weekdays or Weekends; anything else is never offered and is logged); today's timetable-only trains only while the scheduled departure from From is not in the past. Holidays are not modelled.
  - **Not rate-limited.** NFR-13 (a Must) is deliberately deferred for every endpoint: no 429, no `Retry-After`. FR-11 (journeys generated 7 days ahead) is not built either, which is why future dates come from the timetable.

- Exceptions queue (slice B1, spec `_specs/07-exceptions-queue-controller-read.md`), controller role only, mapped only when a database is configured:
  - A delay report raises `ReasonAwaitingVerification` (Critical for TechnicalFault, Obstruction, Weather; High for SignalIssue, Unknown); a departure at least 30 seconds before its scheduled time raises `EarlyDeparture` (High). One report, one exception (unique `related_event_id` + `type`), saved in the same transaction as the event. The rules are `ExceptionPolicy` in Domain.
  - `GET /api/v1/exceptions?status=resolved`: the latest 50 closed exceptions, newest closure first, `{ items, total, limit }`, each with `resolvedAt`, `resolvedByName` (the user name stored at resolve time from the token's `preferred_username`, null for older rows), `resolution` and `secondsToResolve`. Rows are never deleted: resolving only sets `resolved_at`, `resolved_by`, `resolved_by_name` and `resolution`. The web app shows them on the Closed tab.
  - `GET /api/v1/exceptions?status=open` (Critical first, then oldest; 422 for any status other than `open` and `resolved`) returns `{ items, openCount }`, each item with `trainNumber`, `stationName` (null when nothing was reported yet), `type`, `severity`, `createdAt`, `ageSeconds`. `POST /api/v1/exceptions/{id}/resolve` with `{ action: "Acknowledged" | "NoActionNeeded" | "Escalated" }`: 204, 404 `exception_not_found`, 409 `already_resolved`, 422 `invalid_request`. Who resolved it is stored in `resolved_by` and logged.
  - `GET /api/v1/overview` (controller, slice B2): `{ openExceptionCount, journeys, serverTime }`. A journey is listed while in progress (started, not Completed, not past its expected end plus the grace period). Each has `statusKey`, `delayMinutes`, `delayKnown`, `lastEvent { type, stationName, occurredAt, source }`, `nextStop { stationName, expectedAt }` (null when nothing reported or the delay is unknown), `secondsSinceUpdate` (server clock, from when the report was received) and `verification` (`awaiting` when an open reason exception exists, else `none`). `source` is always `OperatorReport` today. Clients poll; nothing is pushed.
  - **CORS** (spec 08): off unless `Cors:AllowedOrigins` is set (a list `Cors__AllowedOrigins__0`, or one comma-separated value). Exact origins only (startup fails on `*`, a path or a trailing slash); GET and POST; headers `Authorization` and `Content-Type`; no credentials. Compose sets `http://localhost:5173`; the cluster sets nothing, so nothing changes there. The overview journeys also carry `from` and `to` (first and last station names).
  - Not built: the operator flag (`OperatorCorrectionRequested`), backward and same-station conflicts. Metrics `smartrail.exceptions.raised` and `.resolved` (Grafana panel "Exceptions raised and resolved").

## Architecture rules
- Dependencies point inward: Api and Infrastructure may use Application; Application may use Domain; Domain uses nothing.
- One folder per feature (for example `Features/Reports/SubmitReport`) containing its endpoint, handler, validator and tests. Features do not reference each other's internals.
- Validate at the edge. Return RFC 9457 ProblemDetails for errors. No business logic in endpoints.
- Every external call goes through an interface in Application and an implementation in Infrastructure.
- Time comes from `TimeProvider`, never `DateTime.Now`.
- Every feature emits traces and metrics through OpenTelemetry with the `deployment.environment` attribute.

## .NET conventions
- `Directory.Build.props` sets `net10.0`, nullable, implicit usings and warnings as errors. Fix warnings; do not suppress them.
- Endpoints are minimal APIs: one `Map<Feature>` extension per feature (see `Features/Status/StatusEndpoint.cs`), registered in `Program.cs`. Prefer `TypedResults` for new endpoints, and put the route under `/api/v1/`.
- Use modern C#: primary constructors, collection expressions, records for DTOs and value objects, pattern matching. Seal classes that are not designed for inheritance.
- Pass `CancellationToken` through every async call from the endpoint down.
- Register services in each layer's own `DependencyInjection` extension; respect lifetimes (no scoped service captured by a singleton).
- Tests: xUnit. Unit tests for Application and Domain; integration tests use `WebApplicationFactory` against the Api project. Add Testcontainers once there is a real database.
- Packages are added per project with `dotnet add package` (no central package management yet).

## Product rules (do not break these)
- **Roles are fixed:** Passenger, Operator, Controller, Caterer, System Administrator. Never add a role.
- **Status is never colour alone.** Four status groups (normal, warning, unknown, disrupted), each with an icon and text.
- **Operators report only on their own assigned journey** (FR-03). Journey order is judged by occurrence time (FR-36).
- **Live updates target 30 seconds** from report to passenger screen (NFR-04). Offline and old data is labelled with the time it is from.
- **Scope decisions (12 Oct 2026):** food ordering is designed, not built (D-71). The passenger app shows the caterer's menu as a read-only list and a button to the caterer's website. SmartRail sends the caterer no train events (D-72). Do not build ordering, tokens, refunds or an outbox.
- Platform clashes are advice only, never blocking (FR-57).
- No hardware implementations.

## Working rules
- The owner is learning and is assessed on their own judgment. Explain choices briefly; do not silently make large design decisions. Ask when a requirement is unclear.
- Never delete files, branches or data without asking first.
- Small changes: one feature slice per branch (`claude/feature/<slug>`), one pull request, CI green before merge. Never push to `main` directly.
- Write the test with the code. A feature is done when: the spec's acceptance criteria pass, CI is green, and its metric or trace is visible in Grafana.
- Keep secrets out of the repo. Compose passwords are development-only.
- If code and a requirement disagree, stop and say so; do not pick one quietly.
