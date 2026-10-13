# Feature Spec: Keycloak identity (sign-in, roles, JWT validation in the API)

branch: claude/feature/keycloak-identity
spec: _specs/keycloak-identity.md
owner: tharindu
date: 2026-10-13
status: draft (for owner review; items marked ❓ need your decision)

---

## 0) Summary
**One-liner:** Run Keycloak in Docker Compose with a repeatable realm (roles, clients, demo users with fixed IDs), and make the API require a valid token and the right role on every staff endpoint.
**Why now:** `operator-report-live-status` cannot start without it: the report endpoint needs a real Operator token, and the demo operators in the seed (`DemoIds`) must exist as real Keycloak users with the same IDs. It also gives the security evidence the brief asks for (401 and 403 tests).
**Who is this for:** Operator, Controller, Caterer (system client), System Administrator (uses the Keycloak console). Passengers stay anonymous (FR-05).
**Success looks like:**
- `docker compose up` brings up Keycloak on its own `keycloak` database with the `smartrail` realm imported, with no manual clicking.
- A token for Nimal (Operator) is accepted by a test endpoint; the same token on a controller-only endpoint gets 403; no token gets 401.
- The existing public endpoints (live status, health) still work without a token.

## 1) Traceability
- Requirements: FR-01, FR-02, FR-06, FR-07, NFR-10, NFR-11
- Acceptance criteria: AC-E01-01 (1, 2: token lifetimes and no detail on bad credentials; 3, 4 belong to the mobile client), AC-E01-02 (2: API side), AC-E01-03 (1, 3), AC-E01-04 (1: public endpoints stay anonymous)
- Edge cases: EC-26 (no token: 401); expired or wrong-issuer token: 401 (added here as a test, not a new EC)
- Decisions: ADR-08 (Keycloak, realm import, clients), ADR-12 (caterer client credentials), ADR-10 (own database, same server), D-27 (no custom admin UI), D-28 (Keycloak knows the role; SmartRail knows the assignment)
- Design: none (no screens). The dashboard sign-in screens are in the hi-fi canvas and are built later.

## 2) Goals and Non-Goals
### Goals
- Keycloak container in Compose, using the empty `keycloak` database and `keycloak` role created by `database-foundation`.
- A realm file in the repo (`smartrail-platform/docker/keycloak/realm-smartrail.json`) imported on start, so any developer or CI run gets the same users, roles and clients.
- Realm roles `operator`, `controller`, `caterer`; fixed IDs for the demo users, equal to the GUIDs in `Seeding/DemoIds.cs`.
- Token lifetimes from NFR-11: access token 15 minutes, refresh token 8 hours.
- API: JWT bearer validation (issuer, audience, signature, lifetime), a flat `roles` claim, policies `Operator` and `Controller`, and a **secure by default** rule: every endpoint requires an authenticated user unless it is explicitly marked public.
- Auth failures are logged without the token and counted.
### Non-Goals (this spec)
- Mobile or web sign-in screens, token storage and refresh in the apps (Flutter, React). ADR-08 names mobile sign-in as the main integration risk, with a fallback trigger. Do a short, timeboxed Android-emulator sign-in spike right after this spec (separate, not part of it).
- The assignment check (FR-03): it stays in the slice-1 handler (D-28).
- Keycloak in the Kubernetes environments (dev, test, staging, prod). It needs the `cluster-databases` spec first.
- Users or roles beyond the demo set, passenger accounts (ticketing is a Should), social login, MFA, email flows, password reset screens.
- A custom admin interface (FR-06: the Keycloak console is the admin interface).
- The simulator's own client (FR-105): added with the simulator slice.
- Production hardening of Keycloak (`start` instead of `start-dev`, TLS, proper hostname, secrets): noted in section 7, not built.

## 3) User Workflow
### Primary workflow (developer)
1. `docker compose up --build`. Keycloak waits for Postgres to be healthy, creates its tables in the `keycloak` database, and imports the realm.
2. Open the Keycloak console (`http://localhost:8081`, development admin from Compose) and check realm `smartrail`: roles, clients, users.
3. Get a token for Nimal (dev helper, section 7) and call a protected endpoint with it.
### Secondary workflows
- Reset everything: `docker compose down -v` and up again; the realm is re-imported.
- Change a user or role: edit the realm file (the source of truth) or use the console, then export it back to the file (document the export command in CLAUDE.md).
- A System Administrator creates a new operator in the console: no SmartRail change is needed (FR-06).

## 4) API Surface
No new business endpoints. Behaviour changes:
- Every endpoint requires an authenticated user by default (fallback authorisation policy). Public, explicitly marked: `GET /api/v1/journeys/{id}/status`, other passenger read endpoints when they exist, `GET /health/live`, `GET /health/ready`.
- Policies: `Operator` (role `operator`), `Controller` (role `controller`), `Caterer` (role `caterer`). Role names are constants in one place.
- Errors (ProblemDetails, RFC 9457): 401 for no, invalid, expired or wrong-audience token; 403 for a valid token without the role. No detail about why a token was rejected goes in the response body.
- A tiny diagnostic endpoint for the tests and the first demo: `GET /api/v1/whoami` (any authenticated user) returns `{ subject, roles }`; `GET /api/v1/ops/ping` (Controller only) returns 204. Both are removed or kept behind a Development flag once real endpoints exist (❓ Q5).

## 5) Data Contract
- No SmartRail tables. Keycloak owns its own database (`keycloak`), created empty by `database-foundation`; SmartRail migrations never touch it and `smartrail_app` has no rights on it.
- The Keycloak subject (`sub`, a GUID) is what SmartRail stores as `operator_user_id`, `controller_user_id`, `assigned_by` and `created_by`.
### Realm contents (realm `smartrail`)
| Item | Value |
|------|-------|
| Roles | `operator`, `controller`, `caterer` (realm roles). `system-admin` and `passenger` are not created: no endpoint uses them (the System Administrator works in the console, passengers are anonymous) ❓ Q2 |
| Users (dev only) | `nimal` (operator), `operator2` (operator, never assigned), `jayasinghe` (controller). IDs = the GUIDs in `DemoIds.cs`; development-only passwords in the realm file, marked as dev-only |
| Client `smartrail-api` | The token audience. Not used to sign in. An audience mapper adds `aud = smartrail-api` to tokens issued to the app clients |
| Client `smartrail-operator-app` | Public, Authorization Code + PKCE, no client secret. Redirect URI for the Flutter app (custom scheme), set properly in the mobile sign-in spike |
| Client `smartrail-ops-web` | Public, Authorization Code + PKCE, redirect `http://localhost:<port>/*` ❓ Q4 |
| Client `smartrail-caterer` | Confidential, client credentials only, service account with role `caterer`, development-only secret (FR-07, ADR-12) |
| Client `smartrail-dev-token` | **Dev only:** public client with direct access grants enabled so you can fetch tokens with curl or an HTTP file. Never in test, staging or prod. ❓ Q3 |
| Token lifetimes | Access 900 s, SSO session max 28 800 s (NFR-11). Keep these values even in dev so expiry is tested |
| Claims | A protocol mapper writes realm roles into a flat claim `roles` in the access token |
| Credentials in the repo | Development-only (CLAUDE.md rule). Real environments inject secrets; none are reused |

## 6) Business Rules
1. R1 (NFR-10): secure by default. A new endpoint is protected unless someone writes `AllowAnonymous` on purpose. A test fails if an endpoint is anonymous and not on the public allow-list (section 4).
2. R2 (FR-02): authorisation is judged on the server from the token's `roles` claim, never from what the client app shows.
3. R3: validation checks signature (keys fetched from Keycloak's discovery document), issuer, audience `smartrail-api`, and lifetime with at most 30 seconds of clock skew.
4. R4 (D-28): Keycloak decides who the user is and their role. It does not decide which journey they may report on; that check runs in the slice-1 handler.
5. R5 (AC-E01-01 2): error responses never say whether the username or the password was wrong (Keycloak's own login page handles this; the API only returns 401).
6. R6: secure logging: log subject (if present), endpoint, outcome and reason code; never the token, never the Authorization header.
7. R7: when no authentication authority is configured (the cluster today), protected endpoints answer 401 and public endpoints work. The app must not crash and must stay Ready.
8. R8: Keycloak settings that must not be hard-coded in C#: authority URL, audience, accepted issuer, clock skew (configuration section `Authentication`).

## 7) Architecture (follow CLAUDE.md and ADR-15)
- API: `Api/Authentication/` (registration extension, role constants, claims mapping), policies in one place; no business logic in the endpoint. `MapInboundClaims = false` and `RoleClaimType = "roles"` so claim names are the ones in the token.
- Packages: `Microsoft.AspNetCore.Authentication.JwtBearer` (check the current version for .NET 10 and the CI vulnerable-package check).
- Telemetry: counter `smartrail.auth.rejected` with tags `reason` (`no_token`, `invalid_token`, `forbidden`) and `deployment.environment`; no per-user tags.
### The issuer problem (read before building)
Tokens carry the address Keycloak was reached at as `iss`. A token fetched at `http://localhost:8081/realms/smartrail` has a different issuer from the one the API sees inside Compose (`http://keycloak:8080/...`), and validation then fails with an issuer error. Fix it once: set a fixed public hostname for Keycloak in Compose (`KC_HOSTNAME`, the `localhost:8081` form), have the API fetch keys from the internal address (`MetadataAddress`) but accept only the public issuer (`ValidIssuer`). Write this in CLAUDE.md. The same issue returns in the Android emulator (it reaches the host as `10.0.2.2`), which is a reason for the mobile spike.
### Compose and tooling (proposed paths)
- `docker-compose.yml`: service `keycloak` (pin a current stable Keycloak tag, check at build time), `start-dev --import-realm`, `KC_DB=postgres` with the `keycloak` database and role, health enabled, realm file mounted at the import path, a Compose healthcheck (Keycloak's management port); the API gets the `Authentication` settings and `depends_on` Keycloak started (not healthy: the API fetches the discovery document on first use, so slow Keycloak start does not stop the API).
- Development admin account for the Keycloak console from environment variables (dev-only value).
- `scripts/get-token.sh <user>`: fetches a token from `smartrail-dev-token` for local testing (dev only).
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
| No authority configured (cluster today) | Protected endpoints 401, public endpoints and `/health/ready` unaffected |
| Realm re-imported over an existing database | Import skips an existing realm; reset with `docker compose down -v` (document this) |
| Client app has a wrong redirect URI | Keycloak refuses the login; fix in the realm file |

## 9) Acceptance Criteria
- [ ] After `docker compose down -v` and `up`, Keycloak is healthy, uses the `keycloak` database, and realm `smartrail` exists with roles, clients and the three demo users.
- [ ] The user IDs in the realm equal the GUIDs in `DemoIds.cs` (a test compares them).
- [ ] A token for `nimal` has `sub` = his ID, `roles` containing `operator`, `aud` containing `smartrail-api`, and a 15-minute lifetime.
- [ ] `GET /api/v1/whoami` returns 401 with no token, 200 with Nimal's token (his subject and roles).
- [ ] `GET /api/v1/ops/ping` returns 403 with Nimal's token and 204 with Jayasinghe's.
- [ ] A client-credentials token from `smartrail-caterer` carries role `caterer` and is refused (403) on Operator and Controller endpoints.
- [ ] An expired token and a token with the wrong audience each give 401.
- [ ] The live-status endpoint and `/health/*` work with no token.
- [ ] A test enumerates all endpoints and fails if an unmarked endpoint is anonymous.
- [ ] No log line, trace or response contains a token or an Authorization header.
- [ ] `smartrail.auth.rejected` is visible with the three reasons.
- [ ] The cluster apps (no authority configured) stay Synced and Healthy.
- [ ] CLAUDE.md documents the issuer rule, `get-token.sh`, the realm export command and the reset procedure.
- [ ] CI is green: build with `-warnaserror`, vulnerable-package check, tests.

## 10) Test Plan
### 10.1 Fixture data
Fixed clock, demo users with the `DemoIds` GUIDs, test tokens as below.
### 10.2 Unit tests
Claims mapping (flat `roles` claim, role constants), policy rules, the "no authority configured" registration path, redaction of tokens in logs.
### 10.3 Integration tests (WebApplicationFactory)
Use a **test token issuer** (a locally generated signing key and the same validation settings) so CI does not need a Keycloak container. Cases: no token, valid operator, valid controller, caterer client token, expired, wrong issuer, wrong audience, wrong signature, public endpoints anonymous, the endpoint enumeration test, the metric counts.
### 10.4 Smoke test against real Keycloak (script, not in the fast CI)
`scripts/smoke-auth.sh`: fetch tokens for `nimal` and `jayasinghe` and the caterer client from the running Compose stack and call `whoami` and `ops/ping`. Run it once by hand before merging, and in CI only if a Compose job already exists (❓ Q6).

## 11) References
- ADR-08 (`adr-08-identity-provider.md`), ADR-10, ADR-12, discovery sections 16 and 18, `database-foundation.md` (the `keycloak` database and `DemoIds`), `operator-report-live-status.md` (the next spec), CLAUDE.md.
- Keycloak documentation: realm import, client types, protocol mappers, hostname settings (check the current version's pages when you build).

## 12) Open Questions
- ❓ Q1: Keep Keycloak as its own spec (recommended) rather than part of slice 1?
- ❓ Q2: Create only `operator`, `controller`, `caterer` now (recommended), or also `system-admin` and `passenger` so all five roles appear in the realm?
- ❓ Q3: Include the dev-only `smartrail-dev-token` client with direct access grants (recommended for fast local testing, never outside Compose)?
- ❓ Q4: Which port will the React dashboard use in development (for the redirect URI)?
- ❓ Q5: Keep `whoami` and `ops/ping` after the first real endpoints exist, or remove them (recommended: keep `whoami` behind a Development flag, remove `ops/ping`)?
- ❓ Q6: Is there a Compose job in CI yet? If not, the real-Keycloak smoke test stays manual.
- ❓ Q7: Do the Android-emulator sign-in spike (timeboxed, ADR-08's fallback trigger) right after this spec, before slice 1 reaches the operator app?
- Decision owner: tharindu, before the build starts.
