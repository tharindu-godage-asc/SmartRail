# Feature Spec: Keycloak identity (sign-in, roles, JWT validation in the API)

branch: claude/feature/keycloak-identity
spec: _specs/02-keycloak-identity.md
owner: tharindu
date: 2026-10-13
status: ready, approved 2026-10-13 (owner approval given in chat, including R9 and the issuer fallback)

---

## 0) Summary
**One-liner:** Run Keycloak in Docker Compose with a repeatable realm (roles, clients, demo users with fixed IDs), and make the API require a valid token and the right role on every staff endpoint.
**Why now:** `03-operator-report-live-status` cannot start without it: the report endpoint needs a real Operator token, and the demo operators in the seed (`DemoIds`) must exist as real Keycloak users with the same IDs. It also gives the security evidence the brief asks for (401 and 403 tests).
**Who is this for:** Operator, Controller, Caterer (system client), System Administrator (uses the Keycloak console). Passengers stay anonymous (FR-05).
**Success looks like:**
- `docker compose up` brings up Keycloak on its own `keycloak` database with the `smartrail` realm imported, with no manual clicking.
- A token for Nimal (Operator) is accepted by `whoami`; the same token on a controller-only endpoint gets 403 (proved in the integration tests); no token gets 401.
- The existing public endpoints (`/api/v1/status`, health) still work without a token, and the cluster apps are unaffected.

## 1) Traceability
- Requirements: FR-01, FR-02, FR-06, FR-07, NFR-10, NFR-11
- Acceptance criteria: AC-E01-01 (1, 2: token lifetimes and no detail on bad credentials; 3, 4 belong to the mobile client), AC-E01-02 (2: API side), AC-E01-03 (1, 3), AC-E01-04 (1: public endpoints stay anonymous)
- Edge cases: EC-26 (no token: 401); expired or wrong-issuer token: 401 (added here as a test, not a new EC)
- Decisions: ADR-08 (Keycloak, realm import, clients), ADR-12 (caterer client credentials), ADR-10 (own database, same server), D-27 (no custom admin UI), D-28 (Keycloak knows the role; SmartRail knows the assignment)
- Design: none (no screens). The dashboard sign-in screens are in the hi-fi canvas and are built later.

## 2) Goals and Non-Goals
### Goals
- Keycloak container in Compose on host port **8180**, using the empty `keycloak` database and `keycloak` role created by `01-database-foundation`.
- A realm file in the repo (`smartrail-platform/docker/keycloak/realm-smartrail.json`) imported on start, so any developer gets the same users, roles and clients.
- All five fixed realm roles (`passenger`, `operator`, `controller`, `caterer`, `system-admin`), so the realm matches the fixed role list in CLAUDE.md. Fixed IDs for the demo users, equal to the GUIDs in `Seeding/DemoIds.cs`.
- Token lifetimes from NFR-11: access token 15 minutes, refresh token 8 hours.
- API: JWT bearer validation (issuer, audience, signature, lifetime), a flat `roles` claim, policies `Operator`, `Controller` and `Caterer`, and a **secure by default** rule: every endpoint requires an authenticated user unless it is explicitly marked public.
- Auth failures are logged without the token and counted.
- A dev helper script to fetch tokens, and a smoke script to check the real Keycloak by hand.
### Non-Goals (this spec)
- Mobile or web sign-in screens, token storage and refresh in the apps (Flutter, React). ADR-08 names mobile sign-in as the main integration risk: a **separate timeboxed Android-emulator sign-in spike (its own small spec) starts right after this spec, before slice 1** (decision Q7).
- The assignment check (FR-03): it stays in the slice-1 handler (D-28).
- Keycloak in the Kubernetes environments (dev, test, staging, prod). It needs the `cluster-databases` spec first, and its own realm file without the dev-only client (deferred decision D1).
- Users or roles beyond the demo set, passenger accounts (ticketing is a Should), social login, MFA, email flows, password reset screens.
- A custom admin interface (FR-06: the Keycloak console is the admin interface).
- The simulator's own client (FR-105): added with the simulator slice.
- Production hardening of Keycloak (`start` instead of `start-dev`, TLS, proper hostname, secrets): noted in section 7, not built.
- A Compose job in CI. CI has none today, so the real-Keycloak check stays a manual script (decision Q6).

## 3) User Workflow
### Primary workflow (developer)
1. `docker compose up --build`. Keycloak waits for Postgres to be healthy, creates its tables in the `keycloak` database, and imports the realm. First start takes a while (expect 30 to 60 seconds and roughly 0.5 to 1 GB of memory; check on your machine).
2. Open the Keycloak console (`http://localhost:8180`, development admin from Compose) and check realm `smartrail`: roles, clients, users.
3. Run `scripts/get-token.sh nimal` and call `GET /api/v1/whoami` with the token.
### Secondary workflows
- Reset everything: `docker compose down -v` and up again; the realm is re-imported.
- Change a user or role: edit the realm file (the source of truth) or use the console, then export it back to the file (the export command goes into CLAUDE.md).
- A System Administrator creates a new operator in the console: no SmartRail change is needed (FR-06).
- Check the real Keycloak by hand before merging: `scripts/smoke-auth.sh`.

## 4) API Surface
Behaviour changes, plus one flagged diagnostic endpoint:
- Every endpoint requires an authenticated user by default (fallback authorisation policy). Public, explicitly marked, on one allow-list: `GET /api/v1/status` (the existing service status; FR-05, decision 2026-10-13), `GET /api/v1/journeys/{id}/status` and other passenger read endpoints when they exist, `GET /health/live`, `GET /health/ready`.
- Policies: `Operator` (role `operator`), `Controller` (role `controller`), `Caterer` (role `caterer`). Role names are constants in one place. `passenger` and `system-admin` exist in the realm but no policy uses them yet.
- Errors (ProblemDetails, RFC 9457): 401 for no, invalid, expired or wrong-audience token; 403 for a valid token without the role. No detail about why a token was rejected goes in the response body.
- `GET /api/v1/whoami` (any authenticated user) returns `{ subject, roles }`. It exists **only when `Diagnostics:WhoAmI` is true** (default false; Compose turns it on, via `Diagnostics__WhoAmI`); otherwise it is not mapped. There is no controller-only ping in production code: the integration tests register their own throw-away endpoints to prove 403 (decision Q5).

## 5) Data Contract
- No SmartRail tables. Keycloak owns its own database (`keycloak`), created empty by `01-database-foundation`; SmartRail migrations never touch it and `smartrail_app` has no rights on it.
- The Keycloak subject (`sub`, a GUID) is what SmartRail stores as `operator_user_id`, `assigned_by` and `created_by`.
### Realm contents (realm `smartrail`, Compose realm file)
| Item | Value |
|------|-------|
| Roles | Realm roles `passenger`, `operator`, `controller`, `caterer`, `system-admin` (decision Q2: all five fixed roles). The System Administrator works in the Keycloak console; passengers are anonymous, so those two roles are not required by any endpoint yet |
| Users (dev only) | `nimal` (operator, ID = `DemoIds.Nimal`), `operator2` (operator, never assigned, ID = `DemoIds.UnassignedOperator`), `jayasinghe` (controller, ID = a new `DemoIds.Controller`, proposed `d0d0d0d0-0000-4000-8000-000000000003`). Development-only passwords in the realm file, marked as dev-only |
| Client `smartrail-api` | The token audience. Not used to sign in. An audience mapper adds `aud = smartrail-api` to tokens issued to the other clients |
| Client `smartrail-operator-app` | Public, Authorization Code + PKCE, no client secret. Redirect URI for the Flutter app (custom scheme), set properly in the Android spike |
| Client `smartrail-ops-web` | Public, Authorization Code + PKCE, redirect `http://localhost:5173/*` (Vite default, decision Q4; correct it when the dashboard exists) |
| Client `smartrail-caterer` | Confidential, client credentials only, service account with role `caterer`, development-only secret (FR-07, ADR-12) |
| Client `smartrail-dev-token` | **Compose realm only:** public client with direct access grants so `scripts/get-token.sh` can fetch tokens with curl (decision Q3). It must never be in test, staging or prod; any later cluster realm file is a separate file without it |
| Token lifetimes | Access 900 s, SSO session max 28 800 s (NFR-11). Kept in dev too, so expiry is exercised |
| Claims | A protocol mapper writes realm roles into a flat claim `roles` in the access token (user tokens and the caterer service-account token) |
| Credentials in the repo | Development-only (CLAUDE.md rule). Real environments inject secrets; none are reused |

## 6) Business Rules
1. R1 (NFR-10): secure by default. A new endpoint is protected unless someone writes `AllowAnonymous` on purpose. A test fails if an endpoint is anonymous and not on the public allow-list (section 4).
2. R2 (FR-02): authorisation is judged on the server from the token's `roles` claim, never from what the client app shows.
3. R3: validation checks signature (keys fetched from Keycloak), issuer, audience `smartrail-api`, and lifetime with at most 30 seconds of clock skew.
4. R4 (D-28): Keycloak decides who the user is and their role. It does not decide which journey they may report on; that check runs in the slice-1 handler.
5. R5 (AC-E01-01 2): error responses never say whether the username or the password was wrong (Keycloak's own login page handles this; the API only returns 401).
6. R6: secure logging: log subject (if present), endpoint, outcome and reason code; never the token, never the Authorization header.
7. R7: when no authentication authority is configured (the cluster today), protected endpoints answer 401 and public endpoints work. The app must not crash and must stay Ready.
8. R8: settings that must not be hard-coded in C#: authority URL, key-fetch (metadata) address, accepted issuer, audience, clock skew (configuration section `Authentication`); and the flag `Diagnostics:WhoAmI`.
9. R9 (proposed addition, confirm on review): a half-configured authority fails startup with a clear message (for example an authority without an audience), instead of silently accepting or rejecting everything. A fully absent configuration is R7, not an error.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- API: `Api/Authentication/` (registration extension, role constants, claims mapping), policies in one place; no business logic in the endpoint. `MapInboundClaims = false` and `RoleClaimType = "roles"` so claim names are the ones in the token. `Features/Diagnostics/WhoAmI` for the flagged endpoint.
- Packages: `Microsoft.AspNetCore.Authentication.JwtBearer` (check the current version for .NET 10 and the CI vulnerable-package check).
- Telemetry: counter `smartrail.auth.rejected` with tags `reason` (`no_token`, `invalid_token`, `forbidden`) and `deployment.environment`; no per-user tags.
- `Seeding/DemoIds.cs` gets a third fixed GUID, `Controller`, for `jayasinghe`.
### The issuer problem (read before building)
Tokens carry the address Keycloak was reached at as `iss`. A token fetched at `http://localhost:8180/realms/smartrail` has a different issuer from the one the API sees inside Compose (`http://keycloak:8080/...`). Plan: set a fixed public hostname in Compose (`KC_HOSTNAME=http://localhost:8180`), accept only that issuer (`ValidIssuer`), have the API fetch the discovery document from the internal address (`MetadataAddress=http://keycloak:8080/realms/smartrail/.well-known/openid-configuration`), and set `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true` so the discovery document returned on the internal address also lists the internal key URL (without it, the keys URL would say `localhost:8180`, unreachable from the API container). `RequireHttpsMetadata=false` in Compose only. This is to be proven during the build, not assumed. Fallback if it cannot be made to work (decision pending confirmation, section 12): use `host.docker.internal:8180` as the one public hostname for both the host and the containers. Write the final rule in CLAUDE.md. The same issue returns in the Android emulator (it reaches the host as `10.0.2.2`), which is part of the spike.
### Compose and tooling (proposed paths)
- `docker-compose.yml`: service `keycloak` (pin a current stable Keycloak tag, checked at build time), `start-dev --import-realm`, `KC_DB=postgres` with the `keycloak` database and role, host port `8180:8080`, health enabled with a Compose healthcheck on the management port, realm file mounted at the import path; the API gets the `Authentication` settings and `Diagnostics__WhoAmI` and `depends_on` Keycloak **started** (not healthy: the API fetches the discovery document on first use, so a slow Keycloak start does not stop the API).
- Development admin account for the Keycloak console from environment variables (development-only value).
- `scripts/get-token.sh <user>`: fetches a token from `smartrail-dev-token` (dev only).
- `scripts/smoke-auth.sh`: fetches tokens for `nimal`, `jayasinghe` and the caterer client from the running Compose stack and checks `whoami`, the claims (`sub`, `roles`, `aud`, 15-minute lifetime) and the no-token 401. The 403 behaviour is proved by the integration tests, because no controller-only endpoint exists in production code yet.
- Production notes only: `start` mode with TLS and a real hostname, no dev client, no direct access grants, secrets from Kubernetes secrets.

## 8) Edge Cases handled in this spec
| Case | Handling |
|------|----------|
| EC-26 No token | 401, no event created |
| Expired token | 401 (clock skew 30 s) |
| Token for another realm or audience | 401 |
| Valid token, wrong role (Operator on a controller endpoint) | 403 (AC-E01-03 3) |
| Operator opens the operations web app | The API refuses controller endpoints with 403; the dashboard shows the "No dashboard access" screen (built with the dashboard) |
| Keycloak unreachable | Already-issued tokens still validate from the cached keys until they expire; new sign-ins fail at Keycloak. The API does not crash |
| No authority configured (cluster today) | Protected endpoints 401, public endpoints and `/health/ready` unaffected (R7) |
| Half-configured authority | Startup fails with a clear message (R9) |
| `/api/v1/status` | Public: works with and without a token |
| `Diagnostics:WhoAmI` off | `/api/v1/whoami` is not mapped (404) |
| Realm re-imported over an existing database | Import skips an existing realm; reset with `docker compose down -v` (document this) |
| Client app has a wrong redirect URI | Keycloak refuses the login; fix in the realm file |

## 9) Acceptance Criteria
- [x] After `docker compose down -v` and `up`, Keycloak is healthy on port 8180, uses the `keycloak` database, and realm `smartrail` exists with five roles, the clients above and the three demo users. (Verified 2026-10-13 after a full reset: Keycloak healthy in about 90 s, realm imported, 100 tables in the keycloak database, 14 in smartrail.)
- [x] A test reads the realm file and checks: the user IDs equal the GUIDs in `DemoIds.cs`, the five roles, the client list, token lifetimes 900 s and 28 800 s, and that the dev-token client exists only in the Compose realm file.
- [x] A token for `nimal` has `sub` = his ID, `roles` containing `operator`, `aud` containing `smartrail-api`, and a 15-minute lifetime (checked by `scripts/smoke-auth.sh`).
- [x] `GET /api/v1/whoami` returns 401 with no token and 200 with Nimal's token (his subject and roles), and is absent when `Diagnostics:WhoAmI` is off.
- [x] In the integration tests, a controller-only test endpoint returns 403 with an operator token and 200 with a controller token.
- [x] A client-credentials token from `smartrail-caterer` carries role `caterer` (smoke script) and is refused (403) on operator and controller endpoints (integration tests). (Real caterer token checked by smoke-auth.sh; the 403 side by the integration tests.)
- [x] An expired token, a wrong-audience token, a wrong-issuer token and a wrong-signature token each give 401.
- [x] `/api/v1/status` and `/health/*` work with no token.
- [x] A test enumerates all endpoints and fails if an unmarked endpoint is anonymous.
- [x] No log line, trace or response contains a token or an Authorization header.
- [x] `smartrail.auth.rejected` is visible with the three reasons. (All three reasons seen in Grafana. On the real stack only no_token and invalid_token can occur, because no role-protected endpoint exists in production code yet; forbidden came from the integration test runs, which export as environment "Development".)
- [ ] The cluster apps (no authority configured) stay Synced and Healthy: the image with no `Authentication` settings is Ready and `/api/v1/status` still returns 200. (Checked so far: the image with no Authentication or database settings is Ready, status 200, whoami 401. Confirm in Argo CD after the merge.)
- [x] CLAUDE.md documents the issuer rule, `get-token.sh`, `smoke-auth.sh`, the realm export command and the reset procedure.
- [ ] CI is green: build with `-warnaserror`, vulnerable-package check, tests. (Local: Release build with warnings as errors, vulnerable-package scan, 24 unit and 70 integration tests all pass. Confirm on the pull request.)

## 10) Test Plan
### 10.1 Fixture data
Fixed clock, demo users with the `DemoIds` GUIDs, test tokens as below.
### 10.2 Unit tests
Claims mapping (flat `roles` claim, role constants), policy rules, the "no authority configured" registration path and the half-configured failure, redaction of tokens in logs, and the realm-file checks listed in section 9 (plain JSON, no Keycloak needed).
### 10.3 Integration tests (WebApplicationFactory)
Use a **test token issuer** (a locally generated signing key and the same validation settings) so CI does not need a Keycloak container. The tests register their own throw-away endpoints (operator-only, controller-only, caterer-only, anonymous) to prove the policies. Cases: no token, valid operator, valid controller, caterer client token, expired, wrong issuer, wrong audience, wrong signature, `/api/v1/status` and health anonymous, the endpoint enumeration test, the metric counts, `whoami` with the flag on and off. The existing `StatusEndpointTests` keep passing without a token.
### 10.4 Smoke test against real Keycloak (script, not in CI)
`scripts/smoke-auth.sh` as described in section 7. Run it once by hand before merging. CI has no Compose job (decision Q6), so it is not automated.

## 11) References
- ADR-08 (`adr-08-identity-provider.md`), ADR-10, ADR-12, discovery sections 16 and 18, `01-database-foundation.md` (the `keycloak` database and `DemoIds`), `03-operator-report-live-status.md` (the next spec), CLAUDE.md.
- Keycloak documentation: realm import, client types, protocol mappers, hostname settings including the backchannel option (check the current version's pages when you build).

## 12) Open Questions and Decisions
### Open questions
None. (Required empty before approval.)
### Decisions made on 2026-10-13
| # | Decision | Recorded in |
|---|----------|-------------|
| Q1 | Keycloak is its own spec, not part of slice 1 (the owner started this spec) | section 0 |
| Port | Keycloak on host port 8180 (8081 is taken by the kind cluster) | sections 2, 7 |
| Public status | `GET /api/v1/status` is public, on the explicit allow-list | section 4 |
| Q2 | All five fixed roles exist in the realm | section 5 |
| Q3 | Dev-only `smartrail-dev-token` client in the Compose realm only | section 5 |
| Q4 | React dashboard redirect `http://localhost:5173/*` | section 5 |
| Q5 | Test-only endpoints in tests; `whoami` behind explicit flag `Diagnostics:WhoAmI`; no ping endpoint in production code | section 4 |
| Realm tests | Unit test on the realm JSON plus a manual smoke script; no Keycloak container in CI | sections 9, 10 |
| Q6 | No Compose job in CI, so the real-Keycloak check stays manual | section 2 |
| Q7 | The timeboxed Android sign-in spike runs right after this spec, before slice 1 | section 2 |
### Deferred decisions
| # | Decision | Fallback chosen now | Revisit when |
|---|----------|---------------------|--------------|
| D1 | Realm file for the Kubernetes environments (no dev-token client, real secrets, real hostname) | None built: Keycloak is not in the cluster | The `cluster-databases` spec and the later Keycloak-in-cluster work |
| D2 | Keycloak production hardening (`start`, TLS, hostname, secrets) | Development mode (`start-dev`) only, documented as dev-only | Before any deployment outside the local machine |
### Proposed additions to confirm on review
- R9 (a half-configured authority fails startup): added so a misconfigured environment fails loudly instead of silently misbehaving, in line with the database spec's "fail loudly" rule. Delete it if you do not want it.
- The issuer fallback (use `host.docker.internal:8180` as the single public hostname if `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` does not work as expected): a technical fallback for the build; confirm or change it.
- The real-stack smoke test cannot prove 403 (no controller-only endpoint exists in production code); 403 is proved in the integration tests only. This follows from decision Q5.
### Consequences for other specs (not edited here)
- `03-operator-report-live-status.md` uses the `Operator` policy from this spec and test tokens in its tests; its "Keycloak, Operator role" authorisation rows already match. `01-database-foundation.md`'s `DemoIds` gains `Controller` (a code change made in this spec's build).
- Decision owner: tharindu. Approval needed before the build starts.
