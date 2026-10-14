# Feature Spec: Android sign-in spike (Flutter + Keycloak on the emulator)

branch: claude/feature/android-signin-spike
spec: _specs/android-signin-spike.md
owner: tharindu
date: 2026-10-14
status: done (spike finished 2026-10-14, result: GO with one condition; see mobile/spikes/android-signin/RESULT.md)

---

## 0) Summary
**One-liner:** A timeboxed experiment: the smallest Flutter app that signs in as Nimal against the Compose Keycloak from the Android emulator, calls `GET /api/v1/whoami` with the token, and refreshes it once. The output is a written result and a go or no-go, not production code.
**Why now:** ADR-08 names mobile sign-in as the main integration risk. The operator app (slice 1 onward) depends on it, and the feature freeze is 15 Oct. If sign-in cannot work on Android, we must learn that in hours, before operator screens are built on it.
**Who is this for:** The developer. No end user sees it. It protects the Operator role's mobile app.
**Success looks like:**
- From the `Pixel_6` emulator, Nimal signs in through Keycloak's own login page using Authorization Code with PKCE, and the app shows his subject (`d0d0d0d0-0000-4000-8000-000000000001`), the role `operator` and the token expiry. It never shows or logs the token.
- A call to `GET /api/v1/whoami` with that token returns 200 with his subject and role.
- One refresh gives a new access token, and `whoami` still works.
- Cancelling the login returns to the app in a clear state, and a wrong password stays on Keycloak's page without leaking detail.
- `mobile/spikes/android-signin/RESULT.md` records what worked, the exact settings, problems and fixes, timings, screenshots and a go or no-go, inside the 4 hour time box.

## 1) Traceability
- Requirements: FR-01, FR-02 (sign-in and roles, via `keycloak-identity.md`), NFR-11 (token lifetimes: access 15 minutes, session 8 hours)
- Acceptance criteria: AC-E01-01 (3, 4), which `keycloak-identity.md` assigns to the mobile client. The criterion text is not in the repo, so it is cited by number only and needs checking when the document is available.
- Decisions and assumptions: ADR-08 (Keycloak, mobile sign-in as the main risk; the ADR itself is not in the repo), ADR-16 (Flutter), A-11 (an Android emulator is an acceptable test device), D-28 (Keycloak knows the role, SmartRail knows the assignment), and from `keycloak-identity.md`: the issuer rule (tokens say `http://localhost:8180/realms/smartrail`) and the operator-app client's redirect URI "set properly in the Android spike".
- Design: none. One throw-away screen.

## 2) Goals and Non-Goals
### Goals
- Prove or disprove: Authorization Code + PKCE sign-in as an operator works from the Android emulator against Keycloak in Compose.
- Find the exact settings it needs (redirect scheme, library settings, cleartext rule, `adb reverse` commands) and write them down.
- Prove the signed-in call reaches the API and the API accepts the token (`whoami`).
- Prove one refresh round trip.
- Record cancel and wrong-password behaviour.
- End with a written go or no-go. If no-go, have the fallback demonstrated (section 6, S2).
### Non-Goals (this spec)
- The real operator app, its screens, navigation, state management, theme or design tokens. The spike UI is one throw-away screen. The real app is created later as `mobile/operator_app`.
- Secure token storage, offline sign-in, token revocation, and what an operator sees when the 8 hour session ends (CON-10 in the discovery doc). Tokens are kept in memory only.
- iOS, a real Android device, and HTTPS for Keycloak (see deferred decisions D1 to D3).
- Signing in as a controller. Controllers use the React dashboard and its own client (`smartrail-ops-web`).
- The passenger app. Passengers are anonymous.
- Any change to the API, the database or the other Keycloak clients.
- Production hardening of any kind.

## 3) User Workflow
### Primary workflow (developer, from `README.md` in the spike folder)
1. Start the stack: `docker compose up -d` (Keycloak on 8180, API on 8080).
2. Start the emulator: `Pixel_6` (a cold start can take several minutes).
3. Let the emulator reach the PC: `adb reverse tcp:8180 tcp:8180` and `adb reverse tcp:8080 tcp:8080`. Inside the emulator, `localhost:8180` and `localhost:8080` then reach your PC, so the issuer stays `http://localhost:8180/realms/smartrail` (decision: no extra issuer).
4. `flutter run` from `mobile/spikes/android-signin`.
5. Tap **Sign in**. A Chrome Custom Tab opens Keycloak's login page. Sign in as `nimal` (password `nimal-dev-only`, from the realm file). Keycloak redirects back to the app.
6. The app shows subject, roles and expiry. Tap **Call whoami**: it shows the API's answer.
7. Tap **Refresh**: it shows the new expiry. Tap **Call whoami** again.
8. Failure paths: tap Sign in and cancel; tap Sign in and enter a wrong password.
### Secondary workflows
- Start and stop times are written to `RESULT.md`, so the time box is checkable.
- On failure of step 5 within the time box: switch to the fallback (S2), record why, and stop.

## 4) API Surface
No new endpoints. The app uses:
- Keycloak: the discovery document `http://localhost:8180/realms/smartrail/.well-known/openid-configuration`, the authorization endpoint (in the Custom Tab) and the token endpoint, all through `adb reverse`. The client is `smartrail-operator-app` (public, Authorization Code + PKCE, no secret).
- The API: `GET /api/v1/whoami` with `Authorization: Bearer <access token>`. It exists because Compose sets `Diagnostics__WhoAmI=true`. Expected: 200 `{ subject, roles }`.

## 5) Data Contract
- No database tables, no new Keycloak users.
- Tokens live in memory in the running app only. Nothing is written to disk or logs.
- Realm file: the operator-app client currently allows the redirect URI `com.smartrail.operator:/oauth2redirect`. The spike confirms it matches the scheme the app registers. If it must change, only that client's redirect URI changes, in `docker/keycloak/realm-smartrail.json`, with the realm tests and the full suite kept green.

## 6) Business Rules
1. S1 (time box): 4 hours of working time, counted from the first emulator start, with start and stop recorded in `RESULT.md`. Time spent only waiting for downloads or an emulator boot is noted but counts, because it is real cost.
2. S2 (fail rule and fallback): if a signed-in call to `GET /api/v1/whoami` has not worked on the emulator within the time box, the spike fails. The fallback is the password grant, for the operator app in the development realm only, as a prototype-only measure. It is demonstrated against the existing Compose-only `smartrail-dev-token` client (no new password-grant client is added) and documented as deprecated and never for a real deployment.
3. S3 (no secrets shown): the screen shows subject, roles and expiry only. The token, refresh token and Authorization header are never displayed, logged or committed.
4. S4 (cleartext): cleartext HTTP is allowed only to `localhost`, only in debug builds, through a network security config. A release build stays strict. A blanket `usesCleartextTraffic` is not used.
5. S5 (one source of truth): any settings the spike discovers go back into the realm file, `CLAUDE.md` and `RESULT.md`. They are not left only in the spike code.
6. S6 (spike code is not product code): the folder is labelled as a spike. No part of it is imported by the future operator app; the operator app copies what `RESULT.md` recommends. A conscious exception: the spike has no automated tests (decision), and the evidence is the recorded run.
7. S7 (the API stays strict): the spike does not weaken any API rule. The single accepted issuer remains `http://localhost:8180/realms/smartrail`.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- `mobile/spikes/android-signin/` (new): a Flutter project, Android only.
  - `lib/main.dart`: one screen with buttons Sign in, Call whoami, Refresh, Sign out, and text lines for state.
  - `lib/auth_service.dart`: a thin wrapper over the sign-in library (configuration, sign in, refresh, claims decoding for display only).
  - `android/`: the redirect scheme `com.smartrail.operator` for the library, and `src/debug/res/xml/network_security_config.xml` allowing cleartext to `localhost` only.
  - `README.md`: the run steps in section 3. `RESULT.md`: the outcome, written during the run.
- Packages (check current versions when adding): `flutter_appauth` (Authorization Code + PKCE through the system browser), `http` for the `whoami` call.
- The folder is not part of `SmartRail.sln`, so CI's .NET build and tests are unaffected. CI has no Flutter job, so the spike is not built in CI (a Non-Goal).
- Updates outside the spike folder: `CLAUDE.md` (mobile run steps, the `adb reverse` rule) and, only if needed, the operator-app client's redirect URI in the realm file.
- Telemetry: none. The existing `smartrail.auth.rejected` metric on the API side is untouched.
### Risks the spike is meant to expose
- The emulator cannot reach Keycloak (fixed by `adb reverse`, to be proven).
- The Custom Tab cannot return to the app (redirect scheme mismatch).
- Android refuses cleartext HTTP (network security config).
- The discovery document's addresses do not match what the emulator can reach (it returns `localhost:8180` URLs because of `KC_HOSTNAME`, which `adb reverse` should satisfy).
- Heavy first build: Gradle and Android dependency downloads for the first run take time and disk space.

## 8) Edge Cases handled in this spec
| Case | Handling |
|------|----------|
| Cancel the login | The app returns to a clear signed-out state and shows that sign-in was cancelled |
| Wrong password | Stays on Keycloak's page, shows Keycloak's own generic message; the app is not involved (AC-E01-01 2 is on Keycloak's side) |
| Keycloak or API not running | The app shows a plain error line, no stack trace, no token |
| `adb reverse` forgotten | Sign-in fails with a connection error; `README.md` lists it as step 3 and `RESULT.md` records the symptom |
| Redirect scheme mismatch | Keycloak refuses with "Invalid parameter: redirect_uri"; fix in the realm file, recorded |
| Access token expires (15 minutes) | The refresh button gets a new one; automatic refresh is out of scope |
| Emulator cold start is slow | Counted in the time box and noted |
| Clock difference between emulator and PC | If sign-in or `whoami` fails with a time error, it is recorded (the API allows 30 seconds of skew) |

## 9) Acceptance Criteria
- [x] The spike app builds and installs on the `Pixel_6` emulator.
- [x] Nimal signs in through Keycloak's login page with Authorization Code + PKCE and returns to the app.
- [x] The app shows Nimal's subject `d0d0d0d0-0000-4000-8000-000000000001`, the role `operator`, and an expiry about 15 minutes ahead.
- [x] `GET /api/v1/whoami` with the app's token returns 200 with that subject and role.
- [x] One refresh returns a new expiry, and `whoami` still returns 200.
- [x] Cancelling the login leaves the app in a clear signed-out state.
- [x] A wrong password stays on Keycloak's page and shows no detail about which field was wrong.
- [x] No token, refresh token or Authorization header appears in the screen text, the app log output, or any committed file. (The device log holds 12 token-like strings from a different app on the emulator; none from this app.)
- [x] Cleartext HTTP is allowed in debug builds for `localhost` only (checked in the manifest and config; the release config has no exception).
- [x] `RESULT.md` records start and stop times, the working settings, problems and fixes, screenshots, and a go or no-go. (Stop time 10:25, 56 minutes after the 09:29 start.)
- [x] If the spike failed: the fallback was demonstrated and `RESULT.md` says so. (Not applicable: the spike succeeded, so the fallback was not needed or demonstrated.)
- [x] Existing tests still pass: build with `-warnaserror`, vulnerable-package check, and the full suite (24 unit, 70 integration), plus the realm tests if the realm file changed.
- [x] `CLAUDE.md` describes how to run the spike and the `adb reverse` rule.

## 10) Test Plan
### 10.1 Fixture data
The demo realm users from `keycloak-identity.md`: `nimal` (operator, signs in), and `jayasinghe` is not used. The API runs in Compose with `Diagnostics__WhoAmI=true`.
### 10.2 Unit tests
None for the spike code (decision, section 6 S6). If the realm file changes, the existing realm unit tests must pass.
### 10.3 Integration and manual checks
The run in section 3 is the test. Each acceptance criterion is ticked from the recorded run, with a screenshot or a log excerpt that contains no token. `scripts/smoke-auth.sh` is re-run if the realm changes, to confirm the server side is unchanged.

## 11) References
- [keycloak-identity.md](keycloak-identity.md) (realm, clients, issuer rule, `whoami`), `database-foundation.md` (`DemoIds`), ADR-16 (Flutter). ADR-08 and AC-E01-01 are cited but not in the repo.
- Documentation to check at build time: the `flutter_appauth` package notes on Android redirect scheme setup, and Android's network security config.

## 12) Open Questions and Decisions
### Open questions
None. (Required empty before approval.)
### Decisions made on 2026-10-14
| # | Decision | Recorded in |
|---|----------|-------------|
| Idea | A throw-away Flutter proof with a written go or no-go, no production code | section 0 |
| Fail rule | The spike fails if no working signed-in `whoami` call happens inside the time box | S2 |
| Time box | Half a day, about 4 hours | S1 |
| Fallback | Password grant, operator app, dev realm only, prototype only; demonstrated with the existing `smartrail-dev-token` client | S2 |
| Code location | `mobile/spikes/android-signin` in the repo, not the real operator app | section 7 |
| Library | `flutter_appauth` | section 7 |
| Emulator address | `adb reverse`; the issuer stays `localhost:8180`; no second issuer | section 3, S7 |
| Refresh | One refresh round trip is in scope | section 3 |
| Branch base | `claude/feature/android-signin-spike` starts from `claude/feature/keycloak-identity`, and its PR opens after the Keycloak PR merges into dev | header |
| Cleartext | Network security config, localhost only, debug builds only | S4 |
| Extra checks | Cancel and wrong password | section 8 |
| Result record | `RESULT.md` with screenshots and the settings | section 7 |
| Tests | No tests for the throw-away app; existing tests stay green | S6 |
### Deferred decisions
| # | Decision | Fallback chosen now | Revisit when |
|---|----------|---------------------|--------------|
| D1 | Secure token storage, session end after 8 hours, offline operators (CON-10) | Tokens in memory only in the spike | The operator app starts |
| D2 | A real Android device (no `adb reverse` without USB; needs a reachable address and likely a second accepted issuer) | Emulator only (A-11) | The final device check, if a device is borrowed |
| D3 | HTTPS for Keycloak and the Android release network rules | Debug-only cleartext to localhost | Before any deployment outside the local machine |
### Proposed additions to confirm on review
- Time-box accounting (S1): waiting time for downloads and emulator boots counts. Change it if you want to exclude it.
- The spike branch starts from the Keycloak branch, so if review changes the realm file there, the spike has to be rebased.
- The `RESULT.md` also records the working settings in the order the operator app will need them.
- Decision owner: tharindu. Approval needed before the build starts.
