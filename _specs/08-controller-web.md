# Feature Spec: Controller web app (exceptions queue and network overview)

branch: claude/feature/controller-web
spec: _specs/08-controller-web.md
owner: tharindu
date: 2026-10-15
status: approved 2026-10-15 (owner answered Q1 to Q13; Q3 changed to Tailwind)

---

## 0) Summary
**One-liner:** A React web app for Jayasinghe, the controller: sign in with Keycloak, see every journey in progress and the open exceptions, and resolve an exception. It uses the three endpoints built in spec 07 and adds no new business rules.
**Why now:** The brief asks for a working web application with meaningful railway-management functionality, and nothing exists yet. The API is ready (spec 07 B1 and B2), so this is mostly front-end work and finishes the demo story: Nimal reports a delay, Jayasinghe sees it and resolves it.
**Who is this for:** Controller. An Operator who opens the app is refused (AC-E01-02 2).
**Success looks like:**
- Jayasinghe signs in, and within 10 seconds of Nimal's delay report sees one Critical row "8058 · Gampaha · Reason awaiting verification · 3 min" and train 8058 on the overview showing the delay.
- He resolves the row with one of three actions and it disappears.
- Nimal (operator) signing in gets a "No dashboard access" screen, and the API refuses his token with 403 anyway.
- One Playwright test runs this whole story against the Compose stack (it is one of the five end-to-end flows the brief asks for).

Size: about one day for the app, plus a quarter day for the design boards and half an hour for the API change in section 4. The feature freeze (15 Oct) is lifted for this slice (owner, 15 Oct). Cut order if time runs short: polish first, then the overview, never the queue.

## 1) Traceability
- Requirements: FR-01 and FR-02 (sign-in, role enforced by the server), FR-04 (controller sees all journeys on the line), FR-50 (overview), FR-51 (queue), FR-43 (resolve is audited, done by the API), FR-63 (permanent safety notice, Must), NFR-04 (age of data shown honestly), NFR-30 (status never by colour alone), NFR-32 (native HTML and ARIA, only meaning changes announced), NFR-33 (text scaling).
- Acceptance criteria: AC-E01-02 (1, 2), AC-E05-01 (1, 3, 4, 6, 7; 2 by polling; 5 has no data yet), AC-E05-02 (3, 4, 6), AC-E01-03 (3: wrong role is refused), AC-E01-01 (3: silent token refresh, as far as the browser library allows).
- Edge cases: expired session, 401 and 403, API unreachable, an exception resolved by someone else (409 `already_resolved`), empty queue, no journey in progress.
- Decisions: D-23 (displays and terminal are the same web app in modes, so this app is structured for a later `/display` mode), D-27 (no custom admin UI: Keycloak is the admin interface), D-28, ADR-08, the stack table in the discovery document (React for the operations web app).
- Design: Claude Design canvas "SmartRail - High-fidelity screens" (https://claude.ai/artifact/JNnBUWHFGLiVQCLN6SGmZc), controller boards `Dash-*` and `Components-Dashboard`. The first draft said none existed; that was wrong. Boards used here: `Dash-Login` (app page with "Sign in with staff account"), `Dash-Login-Error` (the Keycloak page, default theme in this slice), `Dash-NoAccess` (403), `Dash-Overview`, `Dash-Exceptions`, and the shared parts in `Components-Dashboard` (StatusChip, SeverityBadge, KPI card, DataTable, Callout, Button, side menu, dialog). Layout: dark safety-notice bar on top, header (SmartRail, Operations, Connected, date and time, user), left menu, content. It is a page layout with a menu, not the master-detail of discovery 19.4. Not in the canvas and needed here: the resolve dialog for the three actions (the canvas dialog is the override one, same pattern), the "Not up to date" banner, empty states. Boards not used (later slices): `Dash-Verify`, `Dash-BulkVerify`, `Dash-Journey`, `Dash-Suspend`, `Dash-HandOver`, `Dash-Reassign`, `Dash-Timetable`, `Dash-Disruptions`, `Dash-LineClosure`, `Dash-Platform-Warning`, `Dash-Publish-Review`, `Dash-NewService-Stops`, `Dash-ServiceDetail`, `Dash-ByController`.

## 2) Goals and Non-Goals
### Goals
- Vite + React + TypeScript app in `web/ops`, running on `http://localhost:5173` (the redirect URI already in the realm file).
- Sign in with Authorization Code + PKCE against client `smartrail-ops-web`; hold the token in memory; sign out ends the Keycloak session.
- Role gate: the app shows the operations view only when the access token's `roles` claim contains `controller`. The realm maps `roles` into the access token only (`id.token.claim` false), so the app decodes the access token payload for this check (decided 2026-10-15; no realm change). It is a convenience, never a check of trust; otherwise "No dashboard access". This is a convenience; the API remains the authority (FR-02).
- Two screens in the layout of the designs (safety bar, header, left menu, content): **Overview** (tiles Running now, Open exceptions, Awaiting verification; running-journeys table) and **Exceptions** (open table with a detail panel). The menu lists only these two (decided 2026-10-15); the other five items in the designs are added by the slices that build them. Both screens are polled every 10 s.
- Resolving: select a row, the detail panel shows the exception and three buttons (Acknowledged, No action needed, Escalated); a button opens a confirm dialog in the pattern of the canvas's override dialog (decided 2026-10-15).
- Permanent safety notice on every screen (FR-63, AC-E05-01 7).
- Honest freshness: show when each panel was last updated, and a plain "Not up to date" banner when a refresh has failed or is older than 30 s.
- English only; every string in one file so Sinhala and Tamil can be added.
- Tailwind CSS with the design tokens from `smartrail-tokens.json` wired into its theme (same source as the operator app, ADR-16): a small script turns the token file into CSS variables and a Tailwind `@theme` block, so utility classes such as `bg-page`, `text-secondary`, `rounded-md` and `p-md` resolve to token values. No raw hex or pixel value in components. Light theme only.
- Tests: Vitest and Testing Library for components, one Playwright end-to-end test.
### Non-Goals
- Verifying or correcting a delay reason (FR-52, next spec), controller entry of events (FR-53), overrides, disruption messages, platform changes, assignment management, the controller-in-charge feature (FR-125).
- The flag a report slice, overdue detection, and anything the API of spec 07 does not return.
- Push or live update: the app polls (spec 07 Q5).
- Station display and terminal modes (D-23): the folder layout leaves room, nothing is built.
- Sinhala and Tamil text, dark theme, mobile layouts (the controller works at a desk; the layout must still not break at 360 px width).
- Hosting: the app runs as the Vite dev server locally. A production build runs in CI; no cluster deployment (same position as the database, which only Compose has).
- Rate limiting and any production hardening of Keycloak (deferred decisions in specs 02 and 04).

## 3) User Workflow
### Primary workflow
1. Jayasinghe opens `http://localhost:5173`. No session: the app redirects to the Keycloak sign-in page and returns signed in.
2. The page shows the safety notice, the overview (journeys in progress) and the queue (open exceptions, Critical first). The header shows the open count.
3. Nimal reports "Delayed 15 min, Technical fault" for 8058. Within 10 s a Critical row appears: "8058 · Gampaha · Reason awaiting verification · 3 min". The overview row for 8058 shows delay 15 and "Reason awaiting verification".
4. He selects the row, picks "Acknowledged" in the dialog and confirms. The row leaves the queue and the count drops.
### Secondary workflows
- **Operator signs in:** "No dashboard access" with a Sign out button (AC-E01-02 2).
- **Session expired or token refused (401):** the app signs in again once; if that fails it shows "Your session has ended. Sign in again."
- **API down:** the last data stays on screen, dimmed, with "Not up to date, last updated 14:02:10".
- **Nothing open:** "No open exceptions" and "No trains running now" (the same wording as the passenger banner).
- **Resolved by someone else:** 409 `already_resolved` shows "Already resolved by another controller" and the lists refresh.

## 4) API Surface
No new business endpoint. The app calls, with the bearer token:
- `GET /api/v1/exceptions?status=open` -> `{ items: [ { id, journeyId, trainNumber, stationName, type, severity, createdAt, ageSeconds, relatedEventId } ], openCount }`.
- `POST /api/v1/exceptions/{id}/resolve` with `{ action }` (Acknowledged, NoActionNeeded, Escalated): 204, 404 `exception_not_found`, 409 `already_resolved`, 422.
- `GET /api/v1/overview` -> `{ openExceptionCount, journeys: [ { journeyId, trainNumber, statusGroup, statusKey, delayMinutes, delayKnown, lastEvent: { type, stationName, occurredAt, source } | null, nextStop: { stationName, expectedAt } | null, secondsSinceUpdate, verification } ], serverTime }`.

**Overview gains `from` and `to` (decided 2026-10-15).** The Overview board shows a Route column ("Veyangoda → Colombo Fort"). Add `from` and `to` (station names, default language) to each overview journey: the first and last stop names, which the handler already has. One test. Platform, "Journeys today" and "Active overrides" are in the designs but not in the data, so they are left out of this slice, not faked.

**One more backend change is needed: CORS.** A browser app on `localhost:5173` calling the API on another origin is blocked unless the API answers the preflight. I found no CORS setup in the API source. Add a named CORS policy that is **off unless `Cors:AllowedOrigins` is set** (default empty, so the cluster is unchanged, like the other switches); Compose sets it to `http://localhost:5173`. It allows only `GET`, `POST` and the `Authorization` and `Content-Type` headers, no credentials (the app uses bearer tokens, not cookies), and never `*`. Test: an allowed origin gets the headers, any other origin does not, and nothing changes when the setting is empty. Check also that the realm client has `webOrigins` for the app; the file already lists `http://localhost:5173`.

The age shown in a row is `ageSeconds` from the API at fetch time, advanced locally between polls with the browser clock only for display; the server value replaces it at each poll (ADR-06: the server is the authority on time).

## 5) Data Contract
No tables and no migration. No data is stored in the browser except what the OIDC library keeps in memory. Nothing is written to local or session storage (so a reload signs in again silently through the Keycloak session cookie).

## 6) Business Rules
1. Authorisation is the API's job (FR-02). The role gate in the app only chooses which screen to show; a hand-edited client can never read data the API refuses.
2. Queue order is the API's order (Critical, then oldest). The app does not re-sort.
3. Severity is shown by icon and word as well as colour: Critical and High (NFR-30). Critical never relies on red alone.
4. Labels come from codes in one strings file: types `ReasonAwaitingVerification` ("Delay reason waiting for verification", the designs' wording; discovery 19.4 says "Reason awaiting verification", owner chose the designs, 2026-10-15), `EarlyDeparture` ("Departed before scheduled time"); verification `awaiting` ("Awaiting verification") and `none` ("—", as in the design); statusKey `Scheduled`, `OnTime`, `Delayed`, `Completed`; `delayKnown` false shows "Delay unknown". Unknown codes show the raw code, so a new type never breaks the screen.
5. Rows follow the designs' columns (Severity, Exception, Where, Raised, State) with the discovery order in the Where text, e.g. "8058 · Gampaha"; "Raised" is a local clock time with the age ("08:36 · 3 min"). A row without a station shows the train and type only.
6. The reason itself is not shown in this slice, because the API does not return it (FR-52 and D-10 come next).
7. Polling every 10 s while the tab is visible; paused when hidden; one immediate refresh after a resolve and when the tab becomes visible. No overlapping requests.
8. Freshness: each panel shows "Updated 14:02:10" (Asia/Colombo). After 30 s without a successful response the banner "Not up to date" appears with the last good time (NFR-04). Data is never cleared on error.
9. A resolve needs one deliberate confirm (dialog with the three actions and a Cancel); the button is disabled while sending so a double click sends one request.
10. Screen-reader announcements only for meaning changes: the open count rising, and a new Critical exception (NFR-32). Not on every poll.
11. The safety notice text, "SmartRail is an information system, not a safety system. It records decisions; it does not control trains.", is on every screen including sign-in-adjacent ones and cannot be dismissed (FR-63).
12. The token is never logged, put in a URL, or stored; the app sends it only to the API base address from configuration.

## 7) Architecture
- Location `web/ops`; Vite, React 18, TypeScript strict, React Router. Feature-first folders: `src/features/{auth,exceptions,overview}`, `src/core/{api,strings,tokens,time}`, and `src/modes/ops` so a later `src/modes/display` fits (D-23).
- Auth (Q4, owner: recommendation and industry standard): Authorization Code + PKCE only (no implicit flow, no client secret), `oidc-client-ts` with `react-oidc-context`, tokens held in memory only (`InMemoryWebStorage` for the user store; nothing in local or session storage), renewed silently before expiry, state and nonce checked by the library. A reload signs in again silently through the Keycloak session cookie. Authority `http://localhost:8180/realms/smartrail`; client `smartrail-ops-web`. Because the browser reaches Keycloak at `localhost:8180`, the token issuer already matches the API's accepted issuer: the Docker issuer problem in spec 02 does not arise for the web app.
- Data: TanStack Query for polling, caching the last good result and mutation state. `fetch` through one small API module that adds the token and maps ProblemDetails `reason` codes to messages.
- Styling (Q3, owner 2026-10-15): Tailwind CSS (v4, CSS-first config) with `smartrail-tokens.json` integrated into the theme. The designs' variables use the same names as the token file (`--color-bg-page`, `--color-status-*`, `--space-*`, `--radius-*`), so the generator (`tools/generate-css-tokens`, Node) writes them as `:root` variables and as Tailwind `@theme` entries, and a test checks the output against the file. No UI component library: components are small and built from the designs' parts (StatusChip, SeverityBadge, KPI card, DataTable, Button, Callout, dialog).
- Accessibility: native HTML (table for the queue and the overview, button, dialog element), visible focus, keyboard-operable, Esc closes the dialog, 44 px targets for the actions, layout works at 200 % text size.
- Config: `VITE_API_BASE_URL` (default `http://localhost:8080`), `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID`. No secrets (public client).
- Compose/CI: add `web/ops` install, lint, type-check, unit tests and build as a path-filtered workflow `.github/workflows/web.yml`, like `mobile.yml`. The Playwright test runs locally against Compose, not in CI (same position as `scripts/smoke-auth.sh`, spec 02).
- Telemetry: none in the browser in this slice. The server counters from spec 07 (`smartrail.exceptions.raised`, `smartrail.exceptions.resolved`) show the effect in Grafana.
- Designs: taken from the owner's Claude Design canvas (see section 1). Screens the canvas lacks and this spec needs (no access, empty, "Not up to date" banner, resolve dialog) are listed back to the owner before the build, not invented. The Keycloak sign-in pages are shared with the operator boards.

## 8) Edge Cases
| Case | Handling |
|---|---|
| Operator or passenger signs in | "No dashboard access", no data requests made; if they call the API anyway: 403 |
| No `roles` claim | Same as above |
| 401 from the API | One silent sign-in; if it fails, "Your session has ended" |
| 403 from the API | "You do not have access" (the role was removed after sign-in) |
| API unreachable or 5xx | Last data dimmed, "Not up to date" banner, polling continues with the 10 s timer |
| Resolve returns 409 | "Already resolved by another controller", refresh |
| Resolve returns 404 | "This exception no longer exists", refresh |
| Resolve returns 422 | Should not happen (fixed actions); show a generic error and log the reason code |
| Empty queue and no trains | Neutral empty states |
| 50 or more rows | The list scrolls inside the panel; the count in the header is the API's `openCount`, not the rows shown |
| Journey with `delayKnown` false | Shows "Delay unknown", no number |
| Unknown type or status code | The raw code is shown |
| Clock difference between laptop and server | Ages come from the server value at each poll; the local tick only moves them between polls |
| Tab hidden for an hour | Polling paused; one refresh on return |
| Reload | Session restored silently if the Keycloak session lives, otherwise sign-in |

## 9) Acceptance Criteria
- [ ] Controller sign-in shows the operations view (AC-E01-02 1); an Operator sees "No dashboard access" and the API still refuses their token (AC-E01-02 2, AC-E01-03).
- [ ] The queue lists open exceptions in the API's order, each row showing train, station, type, severity (icon and word) and age (AC-E05-02 3, 4, NFR-30).
- [ ] After Nimal's delay report with Technical fault, a Critical row appears within 10 s of the next poll and the overview row shows the delay and "Awaiting verification" (AC-E05-01 1, 6).
- [ ] The overview shows last event, delay, next stop, time since the last update and the source label with text, not colour alone (AC-E05-01 1, 3).
- [ ] The open count is always visible in the header and equals the API's `openCount` (AC-E05-01 4).
- [ ] The safety notice is on every screen and cannot be closed (AC-E05-01 7, FR-63).
- [ ] Resolving removes the row after confirmation, a second click sends one request, and a 409 is explained (AC-E05-02 6).
- [ ] Neither panel clears on an error; "Not up to date" with the last good time appears after 30 s of failures (NFR-04).
- [ ] The whole flow works by keyboard alone; focus is visible; Esc closes the dialog; the page is usable at 200 % text size and at 360 px width (NFR-32, NFR-33).
- [ ] No token appears in a URL, a log line or browser storage.
- [ ] CORS: the allowed origin works, another origin gets no CORS headers, and with `Cors:AllowedOrigins` empty nothing changes (the cluster stays Synced and Healthy).
- [ ] Playwright, against Compose: Nimal's report through the API -> Jayasinghe signs in -> sees the Critical row -> resolves -> the row is gone; and Nimal's token on the web app shows "No dashboard access".
- [ ] `npm run lint`, type-check, unit tests and build pass; the `web` workflow is green on the pull request.

## 10) Test Plan
### 10.1 Seed and fixture data
Compose stack seed: journey 8058, users `nimal` (operator), `jayasinghe` (controller), `operator2`. Unit tests use fixed JSON responses for the three endpoints. The Playwright test creates its own report through the API with a token from `scripts/get-token.sh`.
### 10.2 Unit and component tests (Vitest, Testing Library)
Row text formatting and the age display; label mapping including an unknown code; role gate (controller, operator, no claim); queue and overview rendering from fixtures; resolve flow including 409, 404 and the double-click; polling pause when hidden; freshness banner at 30 s; announcements only on count increase; the CSS token generator output.
### 10.3 Integration tests
- Backend (xUnit, existing base): the CORS policy (allowed, other origin, setting empty).
- Playwright (local, Compose): the story in section 9, plus the operator refusal.

## 11) References
- `07-exceptions-queue-controller-read.md` (the endpoints), `02-keycloak-identity.md` (client `smartrail-ops-web`, roles, issuer rule), `05-operator-app.md` (token generation, quality gates), `smartrail-tokens.json`, discovery stack table and D-23, FR-63, ADR-08, ADR-16, CLAUDE.md.
- Libraries to check at build time for current versions and the CI vulnerable-package check: `oidc-client-ts`, `react-oidc-context`, `@tanstack/react-query`, `react-router`, Vitest, Testing Library, Playwright.

## 12) Open Questions (recommendation first)
- **Q1. Decided 2026-10-15:** one web app for now, with an `ops` mode and room for `display` later (D-23).
- **Q2. Decided:** TanStack Query for polling.
- **Q3. Decided (changed from the recommendation):** Tailwind CSS with the tokens integrated into its theme; no component library.
- **Q4. Decided:** in memory only, Authorization Code + PKCE, as in section 7.
- **Q5. Design first. Decided 2026-10-15:** no new boards are drawn here; the owner's existing high-fidelity screens in Claude Design are the reference.
- **Q6. Decided:** the opt-in policy in section 4. Alternative: serve the app and the API from the same origin with a dev proxy (no API change, but it hides the real deployment problem and does not match what you would deploy).
- **Q7. Decided:** one Playwright test now (counts towards the five flows), run locally against Compose, not in CI. Alternative: add a Compose job to CI (spec 02 Q6 decided against this earlier).
- **Q10 to Q13. Decided 2026-10-15** after reading the canvas: menu shows only Overview and Exceptions; overview shows only data that exists, plus `from`/`to` (API change); resolve is detail panel plus confirm dialog; label wording follows the designs.
- **Q8. Decided:** no free-text note (spec 07 Q3).
- **Q9. Lifted freeze. Confirmed 2026-10-15:** build the full spec (queue, overview, CORS, tests, one Playwright flow). Cut order if time runs short: polish, then the overview, never the queue.
