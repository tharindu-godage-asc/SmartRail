# Feature Spec: Database foundation (EF Core, first migration, seed data)

branch: claude/feature/database-foundation
spec: _specs/database-foundation.md
owner: tharindu
date: 2026-10-13
status: ready, approved 2026-10-13 (owner approval given in chat after the independent review)

---

## 0) Summary
**One-liner:** Connect the API to Postgres with EF Core, create the first migration for the 13 core tables the first slices need, and seed the reference data and the Train 8058 demo journey.
**Why now:** The API does not use Postgres yet (the database is empty). Every later feature spec reads or writes these tables, so this comes first (spec order: this one, then `operator-report-live-status`).
**Who is this for:** The developer (you) and every later feature. No end user sees it.
**Success looks like:**
- `docker compose up --build` starts Postgres (healthy), then the API; the migration applies on an empty database; pgAdmin shows the 13 tables with seed data.
- Compose also creates the empty `keycloak` database, the `keycloak` role and the `smartrail_app` role.
- The append-only rule on `train_event` is proven by an automated test, including through the `smartrail_app` role.
- `/health/ready` reports the database state when a database is configured, and stays green in the cluster, where none is configured yet.
- The demo operator can report on today's demo journey at any time of day, so manual testing never fails with a false "not assigned".

## 1) Traceability
- Requirements: NFR-17 (events append-only), FR-43 (audit trail), FR-10, FR-15 (reference data: stations, platforms, express stopping pattern), NFR-10 (database role least privilege)
- Acceptance criteria: AC-E04-02 (5) (the application's database role cannot UPDATE or DELETE events); AC-E02-01 (1), **partly**: stop states are created together with the journey, but generating journeys daily with a 7-day horizon is not covered here (deferred decision D2)
- Edge cases: EC-132 (UPDATE/DELETE on `train_event` refused)
- Decisions and assumptions: ADR-08 (Keycloak has its own database), ADR-09 (journey version), ADR-10 (PostgreSQL), ADR-11 (journey owns operational data), D-35 (client event ID), D-36 (append-only), D-71 (no `order_reference`), D-72 (no outbox rows for the caterer), **new assumption A-TZ: the operating time zone is Asia/Colombo (UTC+05:30)** (added to the discovery doc's assumptions table with a temporary ID; renumber it when the assumptions register is merged, because IDs above A-13, for example A-26, already exist elsewhere)
- Design: [smartrail-schema.sql](smartrail-schema.sql) (in this folder) is the review artefact; its own header says the EF Core migrations are the source of truth from now on. `smartrail-er-diagram.md` is cited by that file but is not in the repo yet.

## 2) Goals and Non-Goals
### Goals
- Add EF Core with the Npgsql provider to Infrastructure, with one `DbContext` and one entity configuration per table.
- Create one migration, `0001_initial`, for the 13 core tables only (section 5).
- Enforce append-only on `train_event` in the database (trigger), and add a second layer: the runtime role `smartrail_app` cannot UPDATE or DELETE it.
- Give `smartrail_app` default privileges, so later slices add tables without writing grants.
- Seed reference data in every environment inside the migration, and demo data only where a configuration flag turns it on.
- Make Compose create **both** databases on the one Postgres server (`smartrail` and an empty `keycloak`), the roles `smartrail_app` and `keycloak`, and give Postgres a healthcheck that the API waits for.
- Read connection strings from configuration, never hard-coded.
- Add a readiness check for the database that exists only when a database is configured, so the cluster stays Ready.
- Pin the EF tool in a local tool manifest and document the exact `dotnet ef` commands in CLAUDE.md (replace its TODO).
### Non-Goals (this spec)
- `controller_assignment` and its unique partial index `ux_controller_in_charge` (FR-125, D-74/D-76): no first-slice feature uses it. It is added by the migration of the slice that builds the controller in charge (decision 2026-10-13, after review).
- Tables for later slices: `journey_override`, `journey_exception`, `disruption_message`, `outbox_message`, `inbox_message`, `train_subscription`, `menu_item` and `menu_item_translation`. Each is added by the migration of the slice that needs it.
- Ticketing tables (D-60: Should, built only after Slices 0 to 7): `fare_rate`, `passenger_account`, `saved_card`, `coach`, `seat`, `purchase`, `ticket`, `seat_reservation`, and the `btree_gist` extension they need.
- `order_reference` (D-71: food ordering is designed, not built) and any caterer outbox rows (D-72).
- The rest of the seed scope in discovery section 14 (four more commuter trains in both directions, and the Kandy to Colombo Fort long-distance train with its stations). They are added, with their own migrations, by the board and catering slices.
- Journeys that cross midnight: stop times are times of day inside one operating date. Asia/Colombo has no daylight saving (A-TZ), so the time-of-day columns are unambiguous, but a journey running past 24:00 needs its own decision.
- **Cluster databases** for the dev, test, staging and prod namespaces, and the deliberate migration step there (job or bundle). They need their own spec (`cluster-databases`), after slice 1 works locally. Until then the cluster runs the API without a database (section 4).
- **Keycloak setup** (container, realm, users). Only the empty `keycloak` database and role are created here. SmartRail's migrations must never touch the Keycloak database.
- Production-grade backup, partitioning or performance tuning.

## 3) User Workflow
### Primary workflow (developer)
1. One time, because Compose init scripts only run on a brand-new volume: `docker compose down -v` (the current `pgdata` volume holds an empty database, so nothing is lost; confirm before running it).
2. `docker compose up --build`. Postgres starts and becomes healthy; the init scripts create the roles, the default privileges and the `keycloak` database.
3. The API starts. Compose turns on `Database:ApplyMigrationsOnStartup` and `Seeding:DemoData`, so it applies pending migrations (as the admin user) and runs the demo seeder.
4. Open pgAdmin: the 13 tables exist and the seed rows are there.
5. Run `dotnet test`: the integration tests start their own Postgres container and pass (Docker must be running).
### Secondary workflows
- Reset the local database: `docker compose down -v`, then up again; the migration and seed rebuild everything.
- Add a column later: create a new migration (never edit `0001_initial` once it is merged).
- An environment started from an old volume (no `smartrail_app` role): the migration fails with a clear error. That is intended (rule R7).

## 4) API Surface
No business endpoints. Infrastructure endpoints only:
- `GET /health/ready`: 200 when the process is up and, **if `ConnectionStrings:Smartrail` is configured**, the database answers and no migration is pending; 503 otherwise. When no connection string is configured (the cluster today) there is no database check and the endpoint behaves exactly as now. No authentication (probe endpoint); the body carries the status only, never connection details.
- `GET /health/live`: 200 when the process is running (already used by the Kubernetes probes).

## 5) Data Contract
Names and columns follow [smartrail-schema.sql](smartrail-schema.sql) exactly (it was read and compared on 2026-10-13).
### Tables created by `0001_initial` (13)
| Group | Tables |
|-------|--------|
| Languages | `language` |
| Reference | `line`, `line_translation`, `station`, `station_translation`, `platform`, `line_station`, `train_service`, `service_stop` |
| Operational | `journey`, `operator_assignment`, `journey_stop_state`, `train_event` |

### Rules the database itself enforces
- All primary keys, foreign keys, CHECKs and unique indexes from the schema file for these tables, including `UNIQUE (journey_id, client_event_id)`, `ux_language_default`, `UNIQUE (service_id, operating_date)` and the composite key `service_stop (planned_platform_id, station_id)` to `platform (id, station_id)`, which keeps a planned platform inside the stop's own station (this needs an alternate key on `platform (id, station_id)` in the model).
- Append-only: a trigger `train_event_no_update_delete` raises an exception on UPDATE or DELETE of `train_event`, and a statement-level trigger blocks TRUNCATE. The schema file has no trigger; it is added by this migration.
- Runtime role `smartrail_app` (decision Q3, simplified by the review): the init script runs `ALTER DEFAULT PRIVILEGES FOR ROLE smartrail IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO smartrail_app`, so every table the admin user creates later is usable by the API with no grant in the migration. `0001_initial` then **revokes** what must be narrower: UPDATE and DELETE on `train_event` (leaving SELECT and INSERT), and INSERT, UPDATE and DELETE on `__EFMigrationsHistory` (leaving SELECT, which the readiness check reads). TRUNCATE and DDL are never granted. The API connects as `smartrail_app`; migrations run as the admin user `smartrail` through a second connection string.
- If `smartrail_app` does not exist, the REVOKE statements stop the migration with "role does not exist" (rule R7).
### Mapping decisions
- Names: snake_case through `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`), so entities stay PascalCase in C#. Check that the package supports EF Core 10 when adding it. Default `public` schema (what pgAdmin shows); the `CREATE SCHEMA smartrail` and `SET search_path` lines in the SQL file are review artefacts and are not used.
- Keys: `uuid`, generated by the database (`gen_random_uuid()`, built in since PostgreSQL 13) except seed rows, which use fixed GUIDs so every environment is identical. Keys that are not uuids: `language.code` (string), composite keys on `line_translation`, `station_translation`, `line_station`, `journey_stop_state`.
- Times: `timestamptz` in UTC for instants; `scheduled_arrival` and `scheduled_departure` are `time` (local time of day). Conversion to an instant happens in Domain from the journey's `operating_date` and the operating time zone `Asia/Colombo` (A-TZ).
- `journey.version` is the optimistic-concurrency token (`IsConcurrencyToken`), incremented by the application in the same transaction as the writes (ADR-09).
- `train_event.payload` is `jsonb`; `event_type`, `source`, `status`, `confirmation`, `service_type`, `service_category` and `direction` are stored as strings with the schema's CHECK constraints, mapped to C# enums with `HasConversion<string>()`.
- `operator_user_id` and `created_by` hold a Keycloak subject (a GUID in text form, varchar(64)).

## 6) Business Rules
1. R1 (NFR-17, D-36): no code path may update or delete a `train_event` row; the trigger makes this true even if code tries, and the `smartrail_app` role makes it true even if the trigger were dropped.
2. R2 (AC-E02-01 (1)): seeding a journey also seeds one `journey_stop_state` row per `service_stop` of its service (confirmation Pending); a journey with no stop states must be impossible. The seeder does this in one transaction.
3. R3: seeders are idempotent. Running them twice changes nothing (look up by natural keys: `train_number`, `code`, `(service_id, operating_date)`; the operator assignment by journey and operator).
4. R4 (D-71, D-72): the migration does not create `order_reference` or write to any outbox for the caterer.
5. R5: the app applies migrations at startup **only** when `Database:ApplyMigrationsOnStartup` is true, and loads the demo seed **only** when `Seeding:DemoData` is true. Both default to false. Compose sets both to true. The cluster and prod never set them. The code does not guess from the environment name. In the cluster, migrations will be a deliberate step defined in the `cluster-databases` spec.
6. R6: connection strings come from configuration (`ConnectionStrings:Smartrail` for the runtime role, `ConnectionStrings:SmartrailMigrations` for the admin user) and environment variables. The repo holds only the development-only Compose values (CLAUDE.md rule). Never log a connection string.
7. R7: a migration never works around a missing prerequisite. If `smartrail_app` does not exist when the migration runs, it fails with a clear error (decision).
8. R8: startup relies on the Postgres healthcheck and `depends_on: service_healthy`. If the database still cannot be reached when migrations are on, the API exits with an error and Compose restarts it. There is no custom retry loop (removed after review).
9. R9: a table created later by the admin user is usable by `smartrail_app` automatically (default privileges). A later slice that needs a stricter rule, for example another append-only table, revokes explicitly in its own migration and tests it.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- `Infrastructure/Persistence/SmartrailDbContext.cs`, `Configurations/*Configuration.cs` (one per entity), `Migrations/` (generated), `Seeding/ReferenceData.cs` (fixed GUIDs and values used by `HasData`), `Seeding/DemoSeeder.cs`, `Seeding/DemoIds.cs` (the two fixed operator GUIDs).
- Entities live in Domain (plain classes, no EF attributes); mapping is in Infrastructure configurations only. The inward dependency rule holds: Domain knows nothing about EF Core.
- Application defines repository interfaces per feature (for example `IJourneyRepository`); Infrastructure implements them. This spec adds the context and the first implementations needed by `operator-report-live-status`, not a generic repository.
- Registration in `AddInfrastructure`, only when `ConnectionStrings:Smartrail` is set: `AddDbContext` with Npgsql, the readiness check (database reachable, no pending migration), the migration-and-seed hosted step. `TimeProvider.System` is already registered.
- One `SmartrailDbContext` type. The runtime registration builds it from `ConnectionStrings:Smartrail`; the migration step builds an instance of the same type from `ConnectionStrings:SmartrailMigrations`, so the runtime instance never holds admin rights.
- Packages (check current versions when adding): `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Npgsql.OpenTelemetry`; tests: `Testcontainers.PostgreSql`. Tool: `dotnet-ef` pinned in `.config/dotnet-tools.json` (`dotnet tool restore`). New packages must pass the CI vulnerable-package check.
- Telemetry: enable the Npgsql instrumentation (and EF Core's if available) so database spans appear in Tempo; no new custom metric.
### Compose and tooling changes (proposed paths)
- `smartrail-platform/docker/postgres/init/01-roles-and-databases.sql`: creates role `smartrail_app` (LOGIN, development-only password), role `keycloak` (LOGIN, development-only password) and database `keycloak` owned by `keycloak`. `keycloak` has no rights on `smartrail` and `smartrail_app` none on `keycloak`.
- `smartrail-platform/docker/postgres/init/02-smartrail-privileges.sql`: the default privileges for `smartrail_app` in the `smartrail` database. Default privileges are per database, so this file is separate; the test fixture runs `01` once per container and `02` in every fresh test database, so tests use the same SQL as Compose.
- `docker-compose.yml`: mount the init folder into `/docker-entrypoint-initdb.d`; Postgres healthcheck (`pg_isready`); the API gets `depends_on: postgres: condition: service_healthy`, the two connection strings and the two flags from R5. The Postgres image stays pinned to a version of 13 or later (currently `postgres:17`).
### Seed data
**Reference layer (every environment, inside `0001_initial` through `HasData` with fixed GUIDs):**
- Languages: `en` (default), `si`, `ta`.
- Line `MAIN` "Main Line" with the operations desk phone (FR-29). **Colombo Fort is the line origin** (decision), so the sequence and distances run from Colombo Fort.
- Stations (sequence, distance from the origin): Colombo Fort (1, 0.0 km), Maradana (2, 2.0), Ragama (3, 13.0), Gampaha (4, 28.0), Veyangoda (5, 38.0). **The distances are rounded placeholders, to verify against a real timetable** (deferred decision D1). Each station has Sinhala and Tamil names in `station_translation` and platforms `1` and `2`, so a platform change (FR-57) can be demonstrated.
- Service **8058**: Slow, Regional, `Inbound` (toward Colombo Fort, the line's first station), runs on weekdays, with stops in order and Gampaha as a designated arrival stop (`arrival_report_required = true`, D-08):

| Seq | Station | Arrival | Departure |
|-----|---------|---------|-----------|
| 1 | Veyangoda | (none) | 08:20 |
| 2 | Gampaha | 08:30 | 08:31 |
| 3 | Ragama | 08:42 | 08:43 |
| 4 | Maradana | 08:50 | 08:51 |
| 5 | Colombo Fort | 08:55 | (none) |

  Veyangoda 08:20 and Gampaha 08:30 come from the discovery document (section 7). The other times, and Maradana as a stop, are proposals the owner accepted on 2026-10-13 (the discovery timeline lists Veyangoda, Gampaha, Ragama and Colombo Fort only).
- Service **8061**, the express example (FR-15): Express, Regional, `Inbound`, weekdays, Veyangoda dep 07:50, Gampaha arr 08:00 dep 08:01, Colombo Fort arr 08:30. It skips Ragama and Maradana. Placeholder number and times, reference data only: no journey for it in the demo seed.
**Demo layer (only when `Seeding:DemoData` is true; Compose sets it, the cluster does not):**
- Journey: 8058 for the operating date given by `TimeProvider` (today in Asia/Colombo), status `Scheduled`, version 0, created whatever the weekday (deferred decision D2).
- Operator assignment for "Nimal": **valid for the whole operating day**, from 00:00 to 24:00 Asia/Colombo time on that date (`valid_from` = start of the day, `valid_to` = start of the next day), so manual testing at any time of day is never refused as "not assigned". `operator_user_id` = the fixed GUID in `Seeding/DemoIds.cs`; `Seeding:DemoOperatorSubject` may override it. The future Keycloak realm export gives the same user the same ID. Tests that need a narrow window create their own assignment.
- A second operator with a second fixed GUID and no assignment, for the "not assigned" test.
- `journey_stop_state` rows for every stop, confirmation Pending, estimates = scheduled times converted to UTC.

## 8) Edge Cases handled in this spec
| EC | Handling |
|----|----------|
| EC-132 | Trigger refuses UPDATE, DELETE and TRUNCATE on `train_event`; `smartrail_app` has no such rights; integration tests prove both |
| (AC-E02-01 (1)) | Seeder creates stop states with the journey; test asserts count equals the service's stops |
| Idempotent seed | Second run changes nothing |
| Manual testing at 14:00 | Demo assignment covers the whole operating day, so reports are not refused as "not assigned" |
| Database down at start | Healthcheck normally prevents it; if it still happens the API exits and Compose restarts it. `/health/ready` is 503 while a configured database is unreachable |
| Pending migration | `/health/ready` is 503 until applied (only where a database is configured) |
| No database configured (cluster today) | No database registration, no database check; `/health/ready` is 200 and the status endpoint works as before |
| Old Compose volume without the init scripts | `smartrail_app` missing, migration fails with a clear error; fix with `docker compose down -v` |
| Tests cannot clean up by TRUNCATE | Each test class uses its own fresh database (section 10.3) |

## 9) Acceptance Criteria
- [x] On an empty Postgres, applying `0001_initial` creates exactly the 13 tables in section 5 and no others (plus EF's `__EFMigrationsHistory`).
- [x] `dotnet ef migrations script` produces SQL with the unique indexes, CHECK constraints, the trigger and the revokes.
- [x] The Compose Postgres image is pinned to PostgreSQL 13 or later (currently `postgres:17`), because `gen_random_uuid()` is built in only from 13; the Testcontainers fixture uses the same image tag.
- [x] Reference seed gives 3 languages, 1 line, 5 stations with Sinhala and Tamil translations and 2 platforms each, services 8058 (5 stops) and 8061 (3 stops, no Ragama or Maradana).
- [x] With `Seeding:DemoData` on, today's journey 8058 exists with status Scheduled, one operator assignment covering the whole operating day and 5 Pending stop states; running the seeder twice adds nothing. With the flag off nothing demo is created.
- [x] An UPDATE or DELETE on `train_event` fails with the trigger's error as the admin user and as `smartrail_app`; TRUNCATE fails too; `smartrail_app` can SELECT and INSERT.
- [x] `smartrail_app` can SELECT, INSERT, UPDATE and DELETE on an ordinary table (for example `journey`), including a table the admin user creates after the init script ran, and can only SELECT on `__EFMigrationsHistory`.
- [x] A migration run without the `smartrail_app` role fails with a clear error.
- [x] After `docker compose down -v` and `up`, pgAdmin shows databases `smartrail` and `keycloak`, and `keycloak` has no SmartRail tables. (Verified 2026-10-13 against the real Compose stack, via psql instead of pgAdmin.)
- [x] `/health/ready` is 200 with the database up and migrated, 503 with it down or behind on migrations, and 200 with no connection string configured.
- [ ] The cluster apps (dev, test, staging) stay Synced and Healthy after this merges. (Checked so far: the same image with no database settings stays Ready, 200 on ready, live and status. Confirm in Argo CD after the merge.)
- [x] CLAUDE.md lists the `dotnet ef` commands (restore tool, add migration, update database, script, remove last migration) and the two flags.
- [ ] CI is green: build with `-warnaserror`, vulnerable-package check, and the Testcontainers database tests. (Local: build with warnings as errors, vulnerable-package scan and all 47 tests pass. Confirm on the pull request.)

## 10) Test Plan
### 10.1 Fixture data
The same seeders, with a fixed clock (2026-10-06 08:20, a Tuesday) and the demo flag on. The expected rows are the tables in section 7. With that clock the demo assignment (whole day) contains 08:20, so tests that expect "assigned" pass; tests for "not assigned" use the second operator.
### 10.2 Unit tests
Mapping conversions (string enums), the stop-time to UTC conversion for Asia/Colombo (including 08:20 local = 02:50 UTC), the start and end of the operating day as UTC instants, seeder idempotence logic where it can be tested without a database, the flag defaults (both off).
### 10.3 Integration tests (WebApplicationFactory + Postgres in Testcontainers)
Docker must be running. One Postgres container per test run; **each test class gets its own freshly created database** (the TRUNCATE trigger rules out cleaning between tests). The fixture runs `01-roles-and-databases.sql` once per container and `02-smartrail-privileges.sql` in each new database, as Compose does. Tests: apply the migration to an empty database; seed twice and count rows; trigger blocks UPDATE, DELETE and TRUNCATE; the same through the `smartrail_app` connection; `smartrail_app` privileges as in section 9 (ordinary table, a table created later, `__EFMigrationsHistory`); unique constraint on `(journey_id, client_event_id)`; the composite platform key rejects a planned platform from another station; migration without the role fails; the health endpoints with and without a connection string. The existing `StatusEndpointTests` must keep passing without a database.

## 11) References
- [smartrail-schema.sql](smartrail-schema.sql), ADR-08, ADR-09, ADR-10 and ADR-11 (discovery document, sections 16 and 18), ADR-15, CLAUDE.md, `operator-report-live-status.md` (the next spec).
- Cited but not in the repo: `smartrail-er-diagram.md`, `smartrail-refined-requirements.md`, `smartrail-acceptance-criteria.md`, `smartrail-edge-cases.md`. The AC and EC numbers above were checked against them by an independent reviewer on 2026-10-13 (AC-E04-02 (5), EC-132, AC-E02-01, FR-15); they could not be checked by Claude here.
- Docs: EF Core migrations, Npgsql provider, Testcontainers for .NET (check current versions when you add the packages).

## 12) Open Questions and Decisions
### Open questions
None. (Required empty before approval.)
### Decisions made on 2026-10-13
| # | Decision | Recorded in |
|---|----------|-------------|
| Scope | 13 core tables; later slices add their own tables; ticketing and `order_reference` never in these migrations; cluster databases and Keycloak setup out of scope | sections 2, 5 |
| Controller table | `controller_assignment` moved out to the slice that builds FR-125 (review point 2; owner chose to move it) | section 2 |
| Schema file | Owner added `smartrail-schema.sql` to `_specs/` | sections 1, 5 |
| Health check | Database check only when `ConnectionStrings:Smartrail` is set | sections 4, 6 |
| Switches | Explicit flags `Database:ApplyMigrationsOnStartup` and `Seeding:DemoData`, both default off; Compose turns them on | R5 |
| Compose setup | Init scripts create `keycloak` database and role, the `smartrail_app` role and its default privileges; one-time `docker compose down -v` | sections 3, 7 |
| Q1 | `EFCore.NamingConventions`, `public` schema | section 5 |
| Q3 | Role `smartrail_app` created now; admin user runs migrations; default privileges plus explicit revokes on `train_event` and `__EFMigrationsHistory`; a missing role fails loudly (simplified from per-migration grants after review point 7) | sections 5, 6 |
| Q4 | Reference seed inside the migration (`HasData`, fixed GUIDs) | section 7 |
| Q5 | Maradana is a stop of 8058 with the proposed times | section 7 |
| Q6 | Fixed GUID constants for the demo operators, shared with the future Keycloak realm export | section 7 |
| Demo assignment | Whole operating day (00:00 to 24:00 Asia/Colombo), so manual testing works at any hour (review point 1) | section 7 |
| Time zone | Asia/Colombo is the operating time zone, added as a new assumption | sections 1, 5 |
| Startup | Postgres healthcheck and `service_healthy`; if the database is still unreachable the API exits and Compose restarts it; no retry loop (review point 6) | R8 |
| DbContext | One `SmartrailDbContext` type, built from either connection string (review point 8) | section 7 |
| Test isolation | One container, a new database per test class | section 10.3 |
| Demo status | `Scheduled` (a valid value in the schema's CHECK) | section 7 |
| EF tool | Local tool manifest | section 7 |
| Keycloak database | Owned by a separate `keycloak` role | section 7 |
| Line origin | Colombo Fort; 8058 is `Inbound` | section 7 |
| Express | Service 8061 as proposed | section 7 |
| Seed scope | Only 8058 and 8061 now | section 2 |
| Review points 3, 4, 5 | Postgres 13+ acceptance criterion; AC-E02-01 (1) marked partial; midnight-crossing journeys added to Non-Goals | sections 1, 2, 9 |
### Deferred decisions
| # | Decision | Fallback chosen now | Revisit when |
|---|----------|---------------------|--------------|
| D1 | Real distances and the express times | The rounded placeholders in section 7 | Before the demo, or when a real timetable is available |
| D2 | Daily journey generation (7-day horizon) and whether the demo journey is created on weekends | Only the demo journey exists, created every day whatever `runs_on` says | The journey-generation hosted service is built |
### Consequences for other specs (not edited here)
- `operator-report-live-status.md` section 10.1 says "Nimal assigned 08:00 to 10:00". That is fine for tests that create their own assignment, but its demo and manual-testing text should now say "assigned for the whole operating day". Its Q4 (real stop times) is answered by section 7 above.
- Decision owner: tharindu. Approval needed before the migration starts.
