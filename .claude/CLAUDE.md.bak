# SmartRail

SmartRail is a public railway passenger and operations platform (individual assessment, 29 Sep to 20 Oct 2026). It is an **information system, not a safety system**: it never controls trains or detects collisions.

## Stack
- Backend: .NET 10, modular monolith, feature folders, Clean Architecture (Domain, Application, Infrastructure, Api)
- Web (controller dashboard, platform displays): React
- Mobile (passenger app, operator app): Flutter, tested on an Android emulator
- Data: Postgres, EF Core migrations. Identity: Keycloak (ADR-08)
- Delivery: Docker, GitHub Actions, Argo CD on a local kind cluster, environments dev, test, staging, prod (ADR-15)
- Observability: OpenTelemetry to Prometheus, Loki, Tempo, Grafana

## Where things are
- `_specs/` one spec per feature slice. Start from `_specs/_template.md`. Read the spec before coding.
- `docs/` ADRs and requirements. TODO: list the path of the requirements, assumptions, acceptance criteria and edge-case files here.
- `src/`, `tests/`, `deploy/`, `observability/`, `scripts/`

## Commands
- Build: `dotnet build -warnaserror`
- Test: `dotnet test`
- Run local stack: `docker compose up --build` (API on http://localhost:8080, Grafana on http://localhost:3000)
- Migrations: TODO add the exact `dotnet ef` commands once the first migration exists

## Architecture rules
- Dependencies point inward: Api and Infrastructure may use Application; Application may use Domain; Domain uses nothing.
- One folder per feature (for example `Features/Reports/SubmitReport`) containing its endpoint, handler, validator and tests. Features do not reference each other's internals.
- Validate at the edge. Return RFC 9457 ProblemDetails for errors. No business logic in endpoints.
- Every external call goes through an interface in Application and an implementation in Infrastructure.
- Time comes from `TimeProvider`, never `DateTime.Now`.
- Every feature emits traces and metrics through OpenTelemetry with the `deployment.environment` attribute.

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
