# Feature Spec: Operator app (Flutter), first slice

branch: claude/feature/operator-app
spec: _specs/05-operator-app.md
owner: tharindu
date: 2026-10-14
status: built (first slice on 14 Oct 2026; arrival, delay and completed screens added on 15 Oct 2026 after spec 06 A1 and A2 merged). Open questions answered as recommended, see section 12.

---

## 0) Summary
**One-liner:** The Flutter operator app for Nimal: sign in, see the assigned journey, report a departure, see whether it was delivered, and keep working offline.
**Why now:** Slice 1 (operator report and live status) is merged, so the API for sign-in, assigned journey and departure reports exists. The app is the only way an operator can use it, and the 15 Oct feature freeze is close.
**Who is this for:** Operator.
**Success looks like:** Nimal signs in on the Android emulator, sees train 8058 with the next stop, taps "Departed Veyangoda" and sees Delivered; the passenger status read shows the departure. With the emulator offline, the tap shows "Not sent yet" and becomes Delivered after reconnecting, with nothing lost or duplicated.

## 1) Traceability
- Requirements: FR-01, FR-03, FR-20 (**partly**: "Report delay always available" is not met, see Non-Goals), FR-21, FR-24, FR-25, FR-26, FR-31, FR-35, FR-38, NFR-05, NFR-11. FR-40 (early departure): accepted by the server with delay 0, its High exception waits for slice 2. Checked against `docs/smartrail-refined-requirements.md` v1.13 on 2026-10-14.
- Acceptance criteria used (full text in `docs/smartrail-acceptance-criteria.md`):
  - AC-E01-01 (1) (2) (3) (4); AC-E01-03 (1) (2); AC-E03-01 (1) (2) (3) (5); AC-E03-02 (1) (2) (3) (4) (8) (9) (11); AC-E03-05 (1) (2) (3) (4) (5) (6). AC-E03-02 (2) is checked end to end (the passenger status read); (8) is server behaviour, the app only has to send in occurrence order.
  - Shown in the designs but **not built** in this slice: AC-E03-01 (4) (6), AC-E03-02 (5) to (8) (10), AC-E03-03, AC-E03-04, AC-E03-06. See Non-Goals.
- Edge cases: EC-01, EC-02, EC-08 (accepted, exception in slice 2), EC-10, EC-11, EC-12, EC-13, EC-19, EC-23, EC-24, EC-26, EC-27. New in this spec (no EC number yet): process death during sign-in; a queued report older than 8 hours; no next action after the last departure.
- Decisions and assumptions: D-35, D-42, D-08 (designated arrival stations, not yet in the data), ADR-16 (mobile), ADR-08 (Keycloak). Spike result: `mobile/spikes/android-signin` (sign-in works with `flutter_appauth`; the spike itself is not merged and its code is not imported).
- Design: Claude Design canvas "SmartRail - High-fidelity screens" (operator boards, neutral palette). Built in this slice: `Nimal-Login`, `Nimal-SignIn` and `Nimal-SignIn-Error` (the Keycloak pages, default theme in this slice), `Nimal-SessionExpired`, `Nimal-NoJourney`, `Nimal-Journey-Departure` (the main screen), `Nimal-Offline`, `Nimal-Suspended` (widget only), `Nimal-PhoneFallback` (Help tab with sign out), `Nimal-SignOut-Confirm`, and the bottom bar. UI only, submit disabled: `Nimal-DelayReason`, `Nimal-DelayEstimate`. Not built (kept in the canvas for later slices): `Nimal-Journey` (arrival version), `Nimal-DelaySent`, `Nimal-DelayNotSent`, `Nimal-FlagConfirmed`, `Nimal-Reports`, `Nimal-RejectedDetail`, `Nimal-Completed`, `Nimal-NoLongerAssigned`. Brand boards: `Brand-Colour`, `Brand-Type`, `Brand-Icons`.

## 2) Goals and Non-Goals
### Goals
- Sign in with Keycloak (Authorization Code with PKCE), refresh silently, and ask to sign in again when the session is gone (AC-E01-01).
- Journey screen from `GET /api/v1/operator/journey` (stops and IDs) plus `GET /api/v1/trains/{trainNumber}/status` (which stops have departed, the current delay and the server time): header, next-stop action card, journey stop list (done, next, upcoming), "Last report" card with delivery chip.
- Sign out from the Help tab: confirm first (it says how many reports are still waiting), end the Keycloak session, keep the queue on the device, and send it after the next sign-in.
- Report "Departed <stop>" with the real occurrence time, safely retryable (same `clientEventId`).
- Delivery status chip (Delivered, Not sent yet, Rejected) with icon and text, never colour alone.
- Durable local queue with exponential backoff (5 s doubling to 5 min, with jitter), never discarding a report, surviving app restart (AC-E03-05).
- Offline banner and "Offline, last updated <time>" for the cached journey.
- Design tokens as one Dart theme file generated from, or matching, the canvas variables; light theme only (the design has no dark theme).
- Riverpod for state, repository interfaces for every external call, widget and unit tests alongside.

### Non-Goals
- ~~**Arrival reports**~~ **Built on 15 Oct 2026 (spec `06-operator-arrival-delay-reports.md`, slices A1 and A2).** The app sends `Arrived` at the stops the server marks `isArrivalStation` (Gampaha and the final stop), in the order arrive, then depart (AC-E03-03 1, 2, 3).
- ~~**Report delay**~~ **Built on 15 Oct 2026.** Both screens now send: the reason (step 1) and the TOTAL delay (step 2: +10, +15, +20, +30, +45 or Unknown, only options above the delay running), then `Nimal-DelaySent` or `Nimal-DelayNotSent` shows what happened. FR-20 and AC-E03-01 (4) are now met. The reason is never shown to passengers (the server withholds it until a controller verifies it, D-10).
- **Flag my last report** (AC-E03-06). No endpoint. The link is not shown in this slice.
- **Suspended state** (AC-E03-01 (6)). No service-state override exists yet (slice 2). The `Nimal-Suspended` screen is built as a widget and tested with fake data, but the real API never produces it.
- **Reports tab** (history): no "my reports" endpoint. The design shows a 3-tab bar (Journey, Reports, Help). In this slice the bar shows Journey and Help only; Help holds the desk phone number and sign out. (Question Q3.)
- ~~**Arrival and "Journey completed"**~~ **Built on 15 Oct 2026.** An arrival at the final stop completes the journey (the server sets `Completed`); the app then shows `Nimal-Completed` with the counts of sent, waiting and rejected reports, and Done puts it away for this run of the app.
- Rejected-by-Operations review flow (AC-E03-02 (5) to (8)): the app shows the chip "Rejected, requires Operations review" for a 409 or 422, nothing more.
- Push notifications, Sinhala and Tamil text (English only; strings go through one localisation file so the languages can be added), tablet layouts, iOS (Android emulator only), a real device build.
- Biometric lock, device attestation, certificate pinning.

## 3) User Workflow
### Primary workflow
1. App opens. No saved session: sign-in screen with one "Sign in" button, opens the Keycloak page (AC-E01-01 (1)). Invalid credentials are refused by Keycloak with its own generic message (AC-E01-01 (2)).
2. Signed in: journey loads. 404 `no assigned journey` shows an "No journey assigned" empty state.
3. Journey screen (`Nimal-Journey`): next stop and button "Departed <stop>"; tapping it records the report with the current time and shows the chip "Delivered" in "Last report".
4. After the report, only the next valid action is offered (AC-E03-02 (9)).
5. Pull to refresh and a periodic refresh (every 30 s while in the foreground, and straight after each send) reload the journey and the status read. A response with an older `version` than the one shown is ignored.
6. Sign out (Help tab): confirm, then end the session. Waiting reports stay on the phone.

### Secondary workflows
- **Offline** (`Nimal-Offline`): banner "You're offline. Reports are saved and sent later." A tap on "Departed" is queued with its occurrence time, "Not sent yet" chip with a dashed outline, and the next action moves on immediately (AC-E03-02 (11)). On reconnect the queue sends in occurrence order.
- **Session lost to process death** during sign-in: the app detects there is no result and shows the sign-in screen again, never a blank screen (finding D1 of the spike).
- **Both tokens expired offline with queued reports**: the queue is kept; the app asks to sign in, then sends (AC-E01-01 (4)).

## 4) API Surface
No new endpoints. The app calls existing ones (slice 1, spec `_specs/03-operator-report-live-status.md`):
- `GET /api/v1/operator/journey` (role operator): journey, stops with `serviceStopId`, scheduled UTC instants; 404 when none.
- `POST /api/v1/journeys/{journeyId}/events`: `{ clientEventId, eventType: "Departed", serviceStopId, occurredAt }`. 201 recorded, 200 repeat (both mean Delivered), 403 not assigned, 404 unknown journey, 409 conflict, 422 out of limits. Error body carries `reason`.
- `GET /api/v1/trains/{trainNumber}/status` (public, slice 1): `version`, `serverTime`, `delayMinutes`, and per stop `actualDeparture`. This is where the app learns which stops are done (including departures reported by someone else or from another phone), the current delay, and the server time used for the clock offset (rule 1). Poll with the same 30 s rule.
- Keycloak: realm `smartrail`, public client `smartrail-operator-app` (already in the realm file), redirect `com.smartrail.operator:/oauth2redirect`.

Error handling in the app, decided by status and `reason`: 5xx, network failures and the 409 that means "version changed, retry" keep the report queued and retry with backoff. 403, 404, 422 and the other 409 reasons (stop already has a different time, order rule, journey Unassigned or Completed, same ID with different content) mark it Rejected and stop retrying; it is never silently dropped (kept in the queue list for the Reports view later). Rejected is never blocked on or waited for: slice 1 stores nothing on these answers, so no later report depends on it. 401 triggers a token refresh, then one retry, then sign-in. The exact `reason` codes are taken from slice 1 when building (none are invented here).

## 5) Data Contract
- Backend: no tables, no migration.
- Device: a local queue of pending reports (id, journeyId, stopId, occurredAt, clientEventId, state, attempts, nextTryAt, lastReason) and a cache of the last journey with its fetched-at time. Storage choice in Q2 (SQLite via `drift` or `sqflite`, vs. a simple JSON file). Tokens in `flutter_secure_storage`.
- Demo data: existing seed (journey 8058, user `nimal`). Realm file may need the client's redirect URI checked, nothing else.

## 6) Business Rules
1. The occurrence time is taken when the button is pressed, not when it is delivered, corrected by the server offset and sent as UTC (FR-21, ADR-06, EC-11). The offset is the difference between the server's `serverTime` (or the HTTP Date header) and the device clock at the last successful read, kept on the device so it works offline. If no offset has ever been read, the raw device time is sent. A 422 for time still shows "Check your phone's date and time".
2. One `clientEventId` (a UUID) per tap, kept for every retry, so retries are duplicates the server ignores (FR-35, D-35).
3. The queue sends in occurrence order, one at a time. A report that is retrying (5xx, network) holds the ones behind it; a Rejected report never does (Q4).
4. Backoff: 5 s, doubling to a 5-minute cap, with random jitter; items are never discarded (AC-E03-05 (5)).
5. Next valid action is derived from the server journey, the status read (stops already departed) and the queued reports, so an offline tap moves the screen on (AC-E03-02 (11)).
6. Status is never colour alone: each chip has an icon and text. Touch targets at least 44 px (design: 56 px primary button).
7. All times shown are in the fixed zone Asia/Colombo (A-TZ), not the phone's zone, from the UTC instants.
8. Clock: times come from `Clock`, never `DateTime.now()` directly in logic, so tests can set time.
9. At most one pending Departed per stop in the local queue; the button is disabled while a report is being saved.

## 7) Architecture
Location: `mobile/operator_app` (new Flutter project; nothing copied from the spike).
- **Layers per feature folder** (`lib/features/<feature>/{data,domain,presentation}`), feature-first:
  - `auth`: `AuthRepository` (interface) with `AppAuthRepository` (flutter_appauth + secure storage); `authControllerProvider` (AsyncNotifier) with states signedOut / signedIn / sessionLost.
  - `journey`: `JourneyRepository` (interface) with an HTTP implementation (dio or http, Q5), `journeyControllerProvider`.
  - `reports`: `ReportQueue` (interface) with local-database implementation; `reportSenderProvider` (background loop with backoff, started while signed in); `deliveryStatus` derivation.
  - `connectivity`: `connectivity_plus` stream provider.
  - `core`: theme tokens, `Clock`, localisation strings, router.
- **State:** Riverpod (`flutter_riverpod`, with `riverpod_annotation` and code generation if you want it, Q6). Providers hold no UI code; widgets use `ConsumerWidget`; repositories are overridden in tests.
- **Navigation:** `go_router`, with a redirect guard on auth state.
- **Quality:** `flutter_lints` (or `very_good_analysis`) with warnings as errors in CI; `flutter analyze` and `flutter test` must pass.
- **Telemetry:** the mobile app emits none in this slice (OpenTelemetry for Flutter is not set up). The server side already counts events (`smartrail.events.received`, `report_to_visible_seconds`); that is the metric we check in Grafana. (Q7: acceptable?)
- **CI:** a new `mobile` job (Flutter setup, analyze, test) in the existing workflow, only on changes under `mobile/operator_app`.
- **Tokens:** ADR-16 says both apps share a token package generated from `smartrail-tokens.json` (the file exists). This slice generates one Dart theme file from that JSON with a small script instead of typing values by hand (Q10), so no deviation from ADR-16 is needed.
- **API base address:** from `--dart-define=API_BASE_URL`, default `http://10.0.2.2:8080` (emulator host), Keycloak at `http://10.0.2.2:8180`. Per the issuer rule in CLAUDE.md, the emulator needs `adb reverse` so the token issuer is `localhost:8180`; the spike's working setup is reused (no `taskAffinity`, debug-only cleartext config).

## 8) Edge Cases
- Tap twice quickly: one queue item (rule 9: one pending Departed per stop, and the card moves on at once). Two separate taps would carry different times, which the server answers with 409 (a different time for the same stop, AC-E03-02 5), not 200, so the app must not let a second one through.
- Phone clock wrong: the server rejects times outside limits (422); the app shows "Check your phone's date and time" with the Rejected chip. (Not auto-corrected.)
- Journey changes under the operator (reassigned): the server judges assignment at the time the report occurred (R1). A report made before the handover is accepted even if it syncs later; one made after gets 403 and a Rejected chip. The journey refresh then returns 404 and the app shows "No journey assigned".
- A queued report older than 8 hours (EC-10, FR-38): the server answers 422 after sign-in. The report is kept and shown as Rejected, never discarded and never resent in a loop.
- Last allowed departure sent: the card shows the end state from Non-Goals (no button).
- Process death during sign-in: see Secondary workflows.
- App killed while sending: item stays queued; the same `clientEventId` is reused, so the server answers 200.
- Token expiry mid-queue: refresh once, otherwise pause the queue and ask to sign in; nothing is lost.
- Night train: stop times after midnight show the right local time, the date header uses the journey date.

## 9) Acceptance Criteria
- [x] AC-E01-01 (1): sign in with PKCE on the emulator shows the assigned journey.
- [x] AC-E01-01 (2): wrong password is refused with a generic message (Keycloak's). Verified with this app on the emulator 15 Oct: "Invalid username or password." and nothing about which part was wrong. Testing sign-out in the same session found a crash (flutter_appauth's end-session refuses plain HTTP inside Android code); sign-out now clears the phone first and revokes the refresh token with a direct call, with a test.
- [x] AC-E01-01 (3): an expired access token is refreshed without a prompt (unit test with a fake token endpoint).
- [x] AC-E01-01 (4): reports waiting while the session ends are kept and sent after a new sign-in. Shown on the emulator on 15 Oct via sign-out: "Departed Gampaha" was queued with the API stopped, the operator signed out and back in, and it was delivered; the server recorded it at the tap time (08:50:14 Colombo), about 13 minutes before delivery. The expired-session screen was seen separately after the database reset; expiry with waiting reports is covered by the sender and widget tests.
- [x] AC-E03-01 (1), (2), (3): train number, route, stops, done and next stop; only the next action.
- [x] AC-E03-01 (5): offline shows the cached journey labelled "Offline, last updated <time>".
- [x] AC-E03-02 (1): tapping Departed records the event with that occurrence time and shows Delivered.
- [x] AC-E03-02 (3), (11): offline tap shows "Not sent yet", the next action moves on, and it sends after reconnecting.
- [x] AC-E03-02 (4): repeated send gives one event and no error.
- [x] AC-E03-02 (9): "Departed <stop>" is not offered again.
- [x] AC-E03-05 (1), (2), (3), (4): chips Delivered, Not sent yet, Rejected.
- [x] AC-E03-05 (5), (6): backoff 5 s to 5 min with jitter, never discards, queue survives a restart.
- [x] AC-E01-03 (1) (2): a call without a token gets 401 and a refresh or sign-in; a 403 for an unassigned report shows Rejected and stores nothing.
- [x] FR-21: with the phone clock 3 minutes fast, the departure is still accepted (server offset applied). Verified on the emulator 15 Oct: phone 03:53:15, server 03:50:15, the server recorded Maradana at 03:50:08. This run also found and fixed a bug: stop ids repeat every day, so the "one report per stop" check blocked today's tap because of yesterday's report; it now matches the journey as well (test `the same stop on another day is a new report`).
- [x] A 409 caused by a version conflict is retried; other 409 and 422 answers show Rejected.
- [x] Sign out on the emulator (15 Oct): the confirmation said "1 report is still waiting to send from this phone" and the app returned to the sign-in screen. The queue is kept (widget- and drift-tested). The waiting report was sent after the next sign-in (see AC-E01-01 4).
- [x] Screens match the designs (`Nimal-Login`, `Nimal-SessionExpired`, `Nimal-NoJourney`, `Nimal-Journey-Departure`, `Nimal-Offline`, `Nimal-PhoneFallback`, `Nimal-SignOut-Confirm`; `Nimal-Suspended`, `Nimal-DelayReason` and `Nimal-DelayEstimate` as tested widgets), with icon and text for status, 44 px targets, and the text scale at 200 % not clipped.
- [ ] `flutter analyze` clean and `flutter test` green (85 tests, run locally on 15 Oct). CI job `mobile` green: not run on GitHub yet.
- [x] End to end on the emulator: Departed from the app is visible in `GET /api/v1/trains/8058/status` (14 and 15 Oct), and the events metric moves in Grafana: `smartrail_events_received_total{event_type="Departed", outcome="recorded"}` read 1 through Grafana's query API. Two panels were added to SmartRail Overview ("Reports received", "Report to visible"); they showed data after the report that had been waiting was delivered.

## 10) Test Plan
### 10.1 Seed and fixture data
Compose stack seed: journey 8058 (Veyangoda 08:20, Gampaha 08:30/08:31, Ragama, Maradana, Colombo Fort), user `nimal` (assigned), `operator2` (never assigned), `jayasinghe` (controller; not an app user).
### 10.2 Unit tests
Queue ordering, backoff schedule (5, 10, 20, ... 300 s, jitter bounds), next-action derivation with queued items, delivery status mapping (201/200 Delivered, 5xx/network pending, 403/404/409/422 Rejected), token refresh logic, time conversion to local.
### 10.3 Widget and integration tests
Widget tests with fake repositories for each state: normal, offline banner, Not sent yet chip, Delivered chip, Rejected chip, suspended, no journey, session lost, delay reason and estimate lists, sign-out confirm, end state after the last departure. Golden images are optional (Q8). One manual end-to-end run on the emulator against Compose, recorded in the PR (screenshots), as for the spike.

## 11) References
- ADR-16 (mobile), ADR-08 (Keycloak), spec `_specs/03-operator-report-live-status.md`, spec `_specs/02-keycloak-identity.md`, spike findings in `mobile/spikes/android-signin`.
- Design canvas listed in section 1.

## 12) Open Questions
Answered on 2026-10-14 by taking each recommendation (the owner said to start with this spec). Change any of them and say so.
- **Q1 (scope):** departure only; arrival, delay sending, flag and suspended wait for a backend slice. *Taken as drafted.*
- **Q2:** local queue in SQLite with `drift`. *Taken.*
- **Q3:** bottom bar shows Journey and Help only. *Taken.*
- **Q4:** a Rejected report does not hold the queue; a retrying one does. *Taken.*
- **Q5:** `dio`. *Taken.*
- **Q6:** plain Riverpod providers and `Notifier` classes, no code generation for Riverpod. (drift still uses `build_runner`.) *Taken.*
- **Q7:** no mobile telemetry in this slice; the check is the server's event metrics in Grafana. *Taken.*
- **Q8:** widget tests only, no golden images. *Taken.*
- **Q9:** English only; all text is in `lib/core/strings.dart`. *Taken.*
- **Q10:** the Dart theme is generated from `smartrail-tokens.json` by `tool/generate_tokens.dart`. The file was not in the repo when the spec said it existed; the owner added it at `mobile/operator_app/smartrail-tokens.json`. It moves to a shared place when the passenger app starts.

### Decisions made while building (not in the questions above)
- `prompt=login` stays on for sign-in: the phone may be shared between operators (spike finding 4).
- The API sends no line name, so the journey header shows the date and train only; the design adds "Main Line".
- The desk phone number in the design is a placeholder; it is a build setting (`DESK_PHONE`).
- The Noto Sans font file is not bundled; the platform font is used until it is.
- Rejected uses the warning group's style (thick solid outline, icon, words); `Nimal-RejectedDetail` was not built or read.
- CI is a separate workflow `.github/workflows/mobile.yml` with a path filter, not a job in `ci.yml`.
- The language button on the sign-in design is left out (English only).

## 13) Change on 15 Oct 2026: arrival, delay and the completed screen
Requested by the owner after spec 06 A1 (arrival and Completed) and A2 (delay) were merged into `dev`. The criteria come from spec 06 section 9 ("App (after the app change)") and AC-E03-03, AC-E03-04, AC-E03-05.

**What changed in the app**
- A report is now one of three kinds: `departed`, `arrived`, `delay`. The queue stores the kind, the reason code and the total (null total means "unknown"). A delay has no stop.
- The phone database moves from version 1 to 2 (three new columns, the stop becomes optional). Reports waiting on a phone with the old app are kept (tested against a version 1 file).
- At a designated stop the next action is the arrival, then the departure. At the final stop only the arrival. A stop with only an arrival reported counts as "reported" on the server but not as left, so the app no longer reads every `Reported` stop as departed.
- The estimate screen offers +10, +15, +20, +30, +45 and Unknown, only above the delay running; with an unknown delay it says so instead of showing a number.
- The server's times: a new report is stamped after the journey's previous report by at least one second, because the server refuses two reports at the same moment.

**Criteria**
- [x] AC-E03-03 (1) (2) (3): arrive at a designated stop, no arrival elsewhere, an arrival taken offline is kept and the departure is offered at once (widget and unit tests).
- [x] AC-E03-03 (4): an arrival at the final stop shows `Nimal-Completed` with the counts.
- [x] AC-E03-04 (1) (5) (6): a delay is sent with the chosen reason and total, Unknown sends no total, and with +8 running +10 to +45 and Unknown are offered; with +25 running only +30, +45 and Unknown.
- [x] AC-E03-01 (4): "Report delay" is always available, now with a working send.
- [x] Delay confirmation: delivered, saved on the phone (offline, with the desk phone), and refused are three different screens; a saved delay changes to delivered by itself.
- [x] The database migration keeps waiting reports (test with a version 1 file).
- [x] `flutter analyze --fatal-infos` clean and `flutter test` green (see the PR for the count).
- [ ] End to end on the emulator against the Compose stack: arrival, delay and completion appear in `GET /api/v1/trains/8058/status`.
- [ ] The `mobile` CI job on GitHub.