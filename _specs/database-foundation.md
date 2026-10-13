# Feature Spec: Database foundation (EF Core, first migration, seed data)

branch: claude/feature/database-foundation
spec: _specs/database-foundation.md
owner: tharindu
date: 2026-10-13
status: draft (for owner review; items marked ❓ need your decision)

---

## 0) Summary
**One-liner:** Connect the API to Postgres with EF Core, create the first migration for the tables slice 1 needs, and seed the reference data and the Train 8058 demo journey.
**Why now:** The API does not use Postgres yet (the database is empty). Every later feature spec reads or writes these tables, so this comes first (spec order: this one, then `operator-report-live-status`).
**Who is this for:** The developer (you) and every later feature. No end user sees it.
**Success looks like:** `docker compose up` starts Postgres and the API; the migration applies cleanly on an empty database; pgAdmin shows the slice-1 tables filled with seed data; the append-only rule on `train_event` is proven by an automated test; the API has a health check that reports the database state.

## 1) Traceability
- Requirements: NFR-17 (events append-only), FR-43 (audit trail), FR-10, FR-15 (reference data: stations, platforms, express stopping pattern), FR-125 (controller assignment table, created now, used later), NFR-10 (database role least privilege)
- Acceptance criteria: AC-E04-02 (5) (database refuses UPDATE and DELETE on events), AC-E02-01 (seed prevents journeys with no stop states)
- Edge cases: EC-132 (UPDATE/DELETE on `train_event` refused)
- Decisions and assumptions: ADR-09 (journey version), ADR-11 (journey owns operational data), D-35 (client event ID), D-36 (append-only), D-71 (no `order_reference`), D-72 (no outbox rows for the caterer), D-74/D-76 (controller in charge built, senior controller not)
- Design: `smartrail-schema.sql` and `smartrail-er-diagram.md` are the review artefacts; EF Core migrations are the source of truth from now on (the schema file says so in its header)

## 2) Goals and Non-Goals
### Goals
- Add EF Core with the Npgsql provider to Infrastructure, with one `DbContext` and one entity configuration per table.
- Create one migration, `0001_initial`, for the slice-1 tables only (section 5).
- Enforce append-only on `train_event` in the database (trigger), not only in code.
- Seed reference data in every environment and demo data in dev, test and staging only.
- Wire Postgres into Docker Compose (already there) and read the connection string from configuration, never hard-coded.
- Add a readiness health check that fails when the database is unreachable or migrations are missing.
- Document the exact `dotnet ef` commands in CLAUDE.md (replace its TODO).
### Non-Goals (this spec)
- Tables for later slices: `journey_override`, `journey_exception`, `disruption_message`, `outbox_message`, `inbox_message`, `train_subscription`, `menu_item` (+ translation). Each is added by the migration of the slice that needs it.
- Ticketing tables (D-60: Should, built only after Slices 0 to 7): `fare_rate`, `passenger_account`, `saved_card`, `coach`, `seat`, `purchase`, `ticket`, `seat_reservation`.
- `order_reference` (D-71: food ordering is designed, not built) and any caterer outbox rows (D-72).
- Cluster databases for the dev, test, staging and prod namespaces (a separate decision, after slice 1 works locally).
- Production-grade backup, partitioning or performance tuning.

## 3) User Workflow
### Primary workflow (developer)
1. Start the stack with `docker compose up --build`.
2. The API starts; in dev only it applies pending migrations and runs the seeders (❓ Q2).
3. Open pgAdmin: the slice-1 tables exist and the seed rows are there.
4. Run `dotnet test`: the integration tests start their own Postgres in a container, apply the migration, and pass.
### Secondary workflows
- Reset the local database: remove the Compose volume and start again; the migration and seed rebuild everything.
- Add a column later: create a new migration (never edit `0001_initial` once it is merged).

## 4) API Surface
No business endpoints. Infrastructure endpoints only:
- `GET /health/ready`: 200 when the database answers and no migration is pending; 503 otherwise. No authentication (probe endpoint), no data in the body beyond the status.
- `GET /health/live`: 200 when the process is running (already expected by the Kubernetes probes; add it if missing).

## 5) Data Contract
### Tables created by `0001_initial` (names and columns exactly as in `smartrail-schema.sql`)
| Group | Tables |
|-------|--------|
| Languages | `language` |
| Reference | `line`, `line_translation`, `station`, `station_translation`, `platform`, `line_station`, `train_service`, `service_stop` |
| Operational | `journey`, `operator_assignment`, `controller_assignment`, `journey_stop_state`, `train_event` |
Why `controller_assignment` now: FR-125 (controller in charge) is built, and the table is small and has no dependencies beyond `journey`. The unique partial index `ux_controller_in_charge` goes in this migration. The senior-controller reassign (FR-126) is future work and needs nothing more.
### Rules the database itself enforces
- All primary keys, foreign keys, CHECKs and unique indexes from the schema file, including `UNIQUE (journey_id, client_event_id)` and `ux_language_default`.
- Append-only: a trigger `train_event_no_update_delete` raises an exception on UPDATE or DELETE of `train_event`. It also blocks TRUNCATE through a statement-level trigger.
- ❓ Q3: a runtime database role. Recommended: a role `smartrail_app` with SELECT and INSERT only on `train_event` and normal rights elsewhere; the API connects as that role; the migration is applied by a separate admin connection. This is the second layer behind the trigger (AC-E04-02 5 asks that "the application's database role cannot UPDATE or DELETE").
### Mapping decisions
- Names: tables and columns in snake_case as in the schema file. Use `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`) so entities stay PascalCase in C# (❓ Q1).
- Schema: use the default `public` schema, matching what pgAdmin shows (smartrail → Schemas → public). The `CREATE SCHEMA smartrail` line in the SQL file is a review artefact, not used by the migration (❓ Q1).
- Keys: `uuid`, generated by the database (`gen_random_uuid()`) except seed rows, which use fixed GUIDs so every environment is identical.
- Times: `timestamptz` in UTC everywhere; the stop schedule columns are `time` (local time of day) as in the schema; conversion happens in Domain using the journey's `operating_date` and the configured time zone (Asia/Colombo).
- `version` on `journey` is the optimistic-concurrency token (`IsConcurrencyToken`), incremented by the application in the same transaction as the writes (ADR-09).
- `train_event.payload` is `jsonb`; `event_type`, `source` and the status columns are stored as strings with the CHECK constraints from the schema, mapped to C# enums with `HasConversion<string>()`.
- Time zone for time-of-day: `Asia/Colombo` (UTC+05:30) is the system's one operating time zone for the MVP (❓ confirm it is in your assumptions file).

## 6) Business Rules
1. R1 (NFR-17, D-36): no code path may update or delete a `train_event` row; the trigger makes this true even if code tries.
2. R2 (AC-E02-01): seeding a journey also seeds one `journey_stop_state` row per `service_stop` of its service (status Pending); a journey with no stop states must be impossible. The seeder does this in one transaction.
3. R3: seeders are idempotent. Running them twice changes nothing (look up by the natural keys: `train_number`, `code`, `(service_id, operating_date)`).
4. R4 (D-71, D-72): the migration does not create `order_reference` or write to any outbox for the caterer.
5. R5: migrations are applied automatically only in the Development environment and by the CI/integration tests; in test, staging and prod they are applied by a deliberate step (migration bundle or job) before the API starts, never by the API on startup (❓ Q2).
6. R6: connection strings come from configuration (`ConnectionStrings:Smartrail`) and environment variables; the repo holds only the development-only Compose value (CLAUDE.md rule). Never log the connection string.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- `Infrastructure/Persistence/SmartrailDbContext.cs`, `Configurations/*Configuration.cs` (one per entity), `Migrations/` (generated), `Seeding/ReferenceSeeder.cs` and `Seeding/DemoSeeder.cs`.
- Entities live in Domain (plain classes, no EF attributes); mapping is in Infrastructure configurations only. The inward dependency rule holds: Domain knows nothing about EF Core.
- Application defines repository interfaces per feature (for example `IJourneyRepository`); Infrastructure implements them. This spec adds the context and the first implementations needed by `operator-report-live-status`, not a generic repository.
- Registration in `AddInfrastructure`: `AddDbContext` with Npgsql, `TimeProvider.System` (already registered), health check `AddDbContextCheck`.
- Telemetry: enable the Npgsql and EF Core instrumentation so database spans appear in Tempo; a counter `smartrail.db.migrations_applied` is not needed (keep it simple).
### Seed data
**Reference layer (every environment, from the migration via fixed-GUID seed rows, or `ReferenceSeeder` run by the migration job ❓ Q4):**
- Languages: `en` (default), `si`, `ta`.
- Line `MAIN` "Main Line" with the operations desk phone (FR-29).
- Stations in line order: Veyangoda, Gampaha, Ragama, Maradana, Colombo Fort, each with Sinhala and Tamil names in `station_translation`, and at least two platforms per station (`1`, `2`) so a platform change (FR-57) can be demonstrated.
- Service 8058 (Outbound/Inbound per the line's first station rule, Slow, Regional, weekdays) with stops in order, Gampaha as a designated arrival stop (`arrival_report_required = true`, D-08).
- Service for the express example (FR-15): one Express service that skips Ragama and Maradana, to prove the stopping pattern.
- Stop times: Veyangoda dep 08:20 and Gampaha arr 08:30 come from the discovery document (section 7). The remaining times are proposals: Gampaha dep 08:31, Ragama arr 08:42 dep 08:43, Maradana arr 08:50 dep 08:51, Colombo Fort arr 08:55 (❓ Q5; the discovery timeline lists Veyangoda, Gampaha, Ragama and Colombo Fort only, while the dashboard screens already show Maradana).
**Demo layer (development, test, staging only, never prod):**
- Journey: 8058 for the operating date given by `TimeProvider` (today in Asia/Colombo), status Scheduled or Running as the demo needs, version 0.
- One operator assignment for the demo operator "Nimal" (valid from 08:00 to 10:00 on that date), where `operator_user_id` must equal the Keycloak subject of the demo operator user (❓ Q6: put it in configuration `Seeding:DemoOperatorSubject`).
- A second operator with no assignment, for the "not assigned" test.
- `journey_stop_state` rows for every stop, confirmation Pending, estimates = scheduled times.
- Controller in charge: none (the journey shows "No controller in charge", FR-125).

## 8) Edge Cases handled in this spec
| EC | Handling |
|----|----------|
| EC-132 | Trigger refuses UPDATE and DELETE on `train_event`; integration test proves it |
| (AC-E02-01) | Seeder creates stop states with the journey; test asserts count equals the service's stops |
| Idempotent seed | Second run changes nothing |
| Database down | `/health/ready` returns 503; the API does not crash on start in Development, it retries |
| Pending migration | `/health/ready` returns 503 until applied |

## 9) Acceptance Criteria
- [ ] On an empty Postgres, applying `0001_initial` creates exactly the tables in section 5 and no others.
- [ ] `dotnet ef migrations script` produces SQL with the unique indexes, CHECK constraints and the trigger.
- [ ] Reference seed gives 3 languages, 1 line, 5 stations with translations and platforms, service 8058 with its stops and the express example.
- [ ] Demo seed (Development) gives today's journey 8058, the operator assignment and Pending stop states; running it twice adds nothing.
- [ ] An UPDATE or DELETE on `train_event` fails with the trigger's error (test through the application role if Q3 is accepted).
- [ ] `/health/ready` is 200 with the database up and migrated, 503 otherwise.
- [ ] CLAUDE.md lists the `dotnet ef` commands (add migration, update database, script, remove last migration) and the seed switches.
- [ ] CI is green: build with `-warnaserror`, tests including the Testcontainers database tests.

## 10) Test Plan
### 10.1 Fixture data
Use the same seeders in tests with a fixed clock (2026-10-06 08:20, a Tuesday).
### 10.2 Unit tests
Mapping conversions (string enums), the stop-time to UTC conversion for Asia/Colombo, seeder idempotence logic where it can be tested without a database.
### 10.3 Integration tests (WebApplicationFactory + Postgres in Testcontainers)
Apply the migration to an empty database; seed twice and count rows; trigger blocks UPDATE, DELETE and TRUNCATE on `train_event`; unique constraint on `(journey_id, client_event_id)`; unique partial index allows only one open `controller_assignment` per journey; the health endpoints.

## 11) References
- smartrail-schema.sql, smartrail-er-diagram.md, ADR-09 and ADR-11 (discovery document), ADR-15, CLAUDE.md, `operator-report-live-status.md` (the next spec).
- Docs: EF Core migrations, Npgsql provider (check current versions when you add the packages).

## 12) Open Questions
- ❓ Q1: snake_case naming convention package and the `public` schema as proposed? (recommended: yes, it matches pgAdmin and the SQL file)
- ❓ Q2: Apply migrations on startup in Development only, and with a separate step elsewhere? (recommended; avoids an API replica racing another in the cluster)
- ❓ Q3: Create the `smartrail_app` role now, or rely on the trigger alone in slice 1? (recommended: create it now, it is two statements and AC-E04-02 5 asks for it)
- ❓ Q4: Reference seed inside the migration (fixed GUIDs, runs once everywhere) or a `ReferenceSeeder` run by the migration job? (recommended: inside the migration for the small stable set, so no environment can miss it)
- ❓ Q5: Confirm the stop times and whether Maradana is a stop of 8058 (the discovery timeline does not list it).
- ❓ Q6: Where does the demo operator's Keycloak subject come from (a realm export with a fixed user ID, or configuration)?
- Decision owner: tharindu, before starting the migration.
