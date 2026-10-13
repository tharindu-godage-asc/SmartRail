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
  - Not in the repo yet: the user-story document with the full acceptance criteria (the discovery doc refers to it), and a standalone FR/NFR list (FR-xx, NFR-xx). Specs must quote the acceptance criteria they use. If a spec needs one that is missing, stop and ask.
- `smartrail-platform/` the .NET platform. Everything below is relative to it:
  - `src/` (Domain, Application, Infrastructure, Api), `tests/` (UnitTests, IntegrationTests), `SmartRail.sln`
  - `docs/` ADRs (ADR-15 delivery, ADR-16 mobile), `workflows.md`, `branch-protection.md`
  - `deploy/` Kustomize base and overlays, Argo CD apps, kind config, in-cluster collector
  - `observability/` collector, Prometheus, Tempo and Grafana config; `scripts/` `bootstrap.sh`, `bootstrap-cluster.sh`
  - `docker/postgres/init/` SQL that Compose and the integration tests both run: roles, the `keycloak` database, default privileges
  - `docker/keycloak/` the Keycloak realm file (source of truth); `scripts/get-token.sh`, `scripts/smoke-auth.sh`
- `.github/` workflows and Dependabot config (repo root, not under `smartrail-platform/`).
- `tools/stakeholder-sim/` the stakeholder simulation tool (Node).

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

- Identity (Keycloak, spec `_specs/keycloak-identity.md`). Compose runs Keycloak 26.7.5 in development mode: console http://localhost:8180 (user `admin`, password `admin-dev-only`), realm `smartrail`, database `keycloak`. Development-only passwords throughout.
  - The realm file `docker/keycloak/realm-smartrail.json` is the source of truth (roles, clients, demo users). It is imported only when the realm does not exist, so after editing it run `docker compose down -v` and `up`. The demo user IDs equal `DemoIds` in `Seeding/DemoIds.cs`; a test checks it.
  - Demo users: `nimal` (operator, assigned to the demo journey), `operator2` (operator, never assigned), `jayasinghe` (controller), and the `smartrail-caterer` client (client credentials).
  - Get a token: `scripts/get-token.sh nimal` (also `operator2`, `jayasinghe`, `caterer`). Example: `curl -H "Authorization: Bearer $(scripts/get-token.sh nimal)" http://localhost:8080/api/v1/whoami`. The `smartrail-dev-token` client behind it is Compose-only and must never be in another realm.
  - Check the real Keycloak by hand before merging identity changes: `scripts/smoke-auth.sh` (not in CI). The 403 cases are proved by the integration tests, which use their own test token issuer.
  - Export the live realm to compare with the file (the file is hand-kept and much smaller than an export): `docker compose exec -T keycloak /opt/keycloak/bin/kc.sh export --dir /tmp/export --realm smartrail --users realm_file --http-port=18080 --http-management-port=19000`, then `docker compose cp keycloak:/tmp/export/smartrail-realm.json .`. The extra ports avoid a clash with the running server.
  - **The issuer rule:** a token's `iss` is the address Keycloak was reached at. Compose fixes it with `KC_HOSTNAME=http://localhost:8180`, the API accepts only that issuer (`Authentication:Authority`), and fetches keys from `Authentication:MetadataAddress` (`http://keycloak:8080/...`, thanks to `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`). Always fetch tokens from `localhost:8180`. The Android emulator reaches the host as `10.0.2.2`; that is for the sign-in spike.
  - API settings (section `Authentication`): `Authority`, `Audience`, `MetadataAddress`, `RequireHttpsMetadata`, `ClockSkewSeconds` (30). With none set (the cluster today) protected endpoints answer 401 and public ones work; setting only some stops startup with "half configured". `Diagnostics:WhoAmI` (off by default, on in Compose) maps `GET /api/v1/whoami`.
  - **Secure by default:** every endpoint requires a signed-in user unless it is marked `.AllowAnonymous()`. The public list is `/api/v1/status`, `/health/live`, `/health/ready` (plus future passenger reads); a test fails if anything else is anonymous. Policies `Operator`, `Controller`, `Caterer` come from the flat `roles` claim.

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
