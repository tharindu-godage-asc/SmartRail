# Feature Spec: Passenger app (Flutter), first slice

branch: claude/feature/passenger-app
spec: _specs/10-passenger-app.md
owner: tharindu
date: 2026-10-16
status: approved 2026-10-16 (owner: start; scope core first with checkpoints; Q2, Q4, Q5, Q7, Q8, Q9 as recommended)

---

## 0) Summary
**One-liner:** The Flutter passenger app for Kasuni, with no account: choose a language, pick From and To, see the trains with their live delay, open one train's live status (including "Waiting for update" and "You're offline"), follow a train on Home, and keep working from a cache when the phone is offline. Every one of the 34 passenger boards in the Claude Design canvas is accounted for in section 3: 10 work on real data, 7 work on phone-only data, 10 are built and tested with fixture data in a design gallery because the backend does not exist yet, 2 (the food menu boards) depend on spec 11, and 5 are deferred.
**Why now:** The brief requires a passenger mobile application and nothing is built. The backend this needs is already merged (spec 04: stations, find trains, line status; spec 03: train and journey status) and the design is drawn. This is the largest unbuilt required piece, so it starts now.
**Who is this for:** Passenger (anonymous, FR-05). Nobody signs in.
**Success looks like:**
- On the Android emulator Kasuni opens the app, chooses English, taps Veyangoda to Colombo Fort, sees train 8058 in the results, and opens its live status: "Arrives at Gampaha 08:30".
- Nimal reports Veyangoda departed 08:28 (+8): within one poll (10 s) the live screen shows "Arrives at Gampaha 08:38" with "Delayed 8 min", and the results show 08:38 beside "Sched. 08:30" (AC-E06-01 2, AC-E06-02 1).
- Airplane mode on: the live screen shows "You're offline · Showing info from 08:31" and keeps the last ETA marked as last known (AC-E06-03 3).
- The same flow is proved by one integration test against the Compose stack (the fourth of the five end-to-end flows).

Size: large. About 2.5 days: 0.5 day foundations, 0.75 find trains, 0.75 live status, Home and follow, 0.5 gallery and fixture screens. With three working days left before 20 Oct this is the tightest item in the plan. Cut order is in section 12 (Q10). Nothing in the first two phases may be cut.

## 1) Traceability
- Requirements (wording checked in `docs/smartrail-refined-requirements.md` v1.13): FR-65 (find trains, origin remembered), FR-66 (primary ETA at the boarding station, then the destination, one stop list), FR-67 (train silence versus viewer silence), FR-68 (home: followed train, search, service alerts), FR-69 (Sinhala, Tamil, English), FR-72 (when the information was last updated, Should), FR-05 (no account), NFR-03, NFR-04, NFR-30 (status never by colour alone), NFR-31 (contrast), NFR-32 (screen reader announcements), NFR-33 (text scaling), NFR-34. FR-75 and FR-76 (Notify me, push) are **Must and are not built** (section 2, Q4). FR-90 and FR-91 (menu) only if spec 11 is built. FR-110, FR-70, FR-71 are not built.
- Acceptance criteria: AC-E06-01 (1, 2, 4; 3 is fixture only), AC-E06-02 (1, 2, 3, 5, 6; 4 is fixture only), AC-E06-03 (1 partly, 3, 4, 7; 2; 5 not built; 6 fixture only), AC-E06-04 (1, 2, 4, 5; 3 fixture only), AC-E06-05 (1, 2, 3, 4, 5 partly, 6). AC-E07-01 and AC-E07-02 (notifications): **not met**.
- Edge cases: EC-70 (first launch, no language), EC-71 (no followed train), EC-78 (station name with no translation), EC-79 (large text or a screen reader on a status change), EC-80 (no announcement for an ETA-only change), EC-40 (Kasuni misses her train: the ETA switches to the destination, accepted), EC-41 (a stop passed with no report shows as passed), EC-43 (how old is the information: FR-72, a Should), EC-17 (delay length unknown, stated honestly). EC-72 to EC-77 (notifications) are not met (Q4). The empty and error states are in section 8.
- Decisions: D-10 (reason only after verification), D-25 (language), D-31 (push), D-38 (primary ETA rule), D-39 (two kinds of silence), D-52 (home card), D-71 (menu, ordering on the caterer's website), P-07 (a passed stop is not "missing"), ADR-06 (the server is the authority on time), ADR-09 (snapshot plus version), ADR-16 (mobile).
- Design: Claude Design canvas "SmartRail - High-fidelity screens", teal accent, 390 by 844. All 34 passenger boards are listed in section 3. The canvas on 16 Oct (version 1792037517) was read for this spec.

## 2) Goals and Non-Goals
### Goals
- Flutter project `mobile/passenger_app`, Android emulator only (NFR-44), same toolchain, lints and test style as `mobile/operator_app`.
- All 17 screens marked "Live" in section 3, on the public endpoints of specs 03 and 04.
- Language chosen first (Sinhala, Tamil, English), saved on the phone, changeable in Settings; all interface text and statuses in the chosen language (AC-E06-05 1).
- Offline behaviour: the station list works without a connection; the last live snapshot of a train is kept and shown marked "last known" (AC-E06-03 3).
- A "design gallery" screen (debug builds only) that renders every state of the 34 boards with fixture data, so the unbuilt states are demonstrable and tested.
- The two small backend additions of section 4.
- Design tokens generated from `smartrail-tokens.json` (teal accent for the passenger app), Riverpod, repository interfaces, widget and unit tests.
### Non-Goals
- **Notify me and push notifications (FR-75, FR-76, AC-E07-x): a Must that is not met.** Push needs a push provider, device tokens stored on the server and an event consumer; none exist, and the notification screens would promise something the app cannot do. The three notification boards are built as fixtures only, and the real build has no "Notify me" button (Q2, Q4). The report must say this requirement is unmet.
- Suspended, cancelled and terminated services (AC-E06-02 4, AC-E06-03 6, AC-E06-01 3): overrides are not built, so no endpoint returns these states. Built as fixtures.
- The verified reason and "reason being checked" (D-10, AC-E06-02 3 and 4): the verification slice (FR-52) is not built, the status read carries no reason. The live screen shows delay and ETA and no reason, which is exactly what AC-E06-02 3 and AC-E05-03 3 require until a controller verifies. The two boards are fixtures.
- Service alerts from Operations (FR-55, AC-E06-04 3): disruption messages are not built. The Alerts screen is live only for its "Trains you follow" part.
- Tickets, sign in, create account, account, saved cards (D-60, FR-110): ticketing is a Should built last. "My tickets" shows its designed empty state. The five sign-in and registration boards are Keycloak-hosted pages (their titles say "identity provider page"), not Flutter screens, and are deferred with ticketing.
- Food menu (FR-90, FR-91): only if spec 11 (catering) lands; otherwise fixtures (Q10).
- Saved regular journeys (FR-70), operational station information (FR-71), connections, the real overdue state (needs overdue detection), `expectedNextEventBy` out-of-date warning (AC-E06-03 5), location-based "nearest station" (the design says no location permission), iOS, tablets, dark theme, deep links.
- Rate limiting (NFR-13) stays deferred (spec 04 D2); the app must not poll faster than section 6.

## 3) Screen inventory (all 34 passenger boards)
"Live" = built on real API data. "Local" = built, state kept on the phone only. "Fixture" = built as a widget, tested with fixture data, shown in the design gallery, not reachable in the shipped flow. "Deferred" = not built.

| # | Board | Route / place | Status | Data source and notes |
|---|---|---|---|---|
| 1 | Pax-Splash | launch | Local | Logo and "Live train times you can trust"; opens Language on first run, Home afterwards |
| 2 | Pax-Language | first run, Settings | Local | Three large choices, saved on the phone; "You can change this later in Settings" |
| 3 | Pax-Onboard-1 | first run | Local | "Know when your train is coming"; Skip, Next, step dots |
| 4 | Pax-Onboard-2 | first run | Local | "Honest when we are unsure"; the status chip legend |
| 5 | Pax-Onboard-3 | first run | Local | "Follow your train"; copy rewritten so it does not promise notifications (Q2) |
| 6 | Pax-Home | Home tab, no followed train | Live | Line status banner from `GET /lines/MAIN/status`; From remembered; the design's welcome heading is shown on first visit only |
| 7 | Pax-Home-Following | Home tab, a train followed | Live | Followed train card from `GET /trains/{n}/status`; the line "Signal failure between Veyangoda and Gampaha" is a reason/message, so it is **omitted** |
| 8 | Pax-Search-First | From picker, first visit | Live | `GET /stations`, cached with ETag; works offline from the cache |
| 9 | Pax-Search | To picker / From with recent | Live | Same cache; type to narrow; recent and "your usual starting station" from the phone |
| 10 | Pax-Results | Trains tab | Live | `GET /trains?from&to`; time = estimated arrival at From, "Sched." beside it, chip, platform |
| 11 | Pax-Results-Empty | Trains tab | Live | Empty list: "No more trains today" with the first train tomorrow from `date=` tomorrow |
| 12 | Main (connected) | train detail | Live | `GET /trains/{n}/status`; Q-free rules in section 6 |
| 13 | Kasuni-Live-Overdue | train detail | Live, partly | Triggered by `delayKnown: false` only ("Waiting for update"); the true overdue state needs overdue detection (not built) |
| 14 | Kasuni-Live-Offline | train detail | Live | Phone offline: last snapshot, marked |
| 15 | Pax-LiveReasonPending | train detail | Fixture | Needs the reason state from the verification slice |
| 16 | Pax-LiveSuspended | train detail | Fixture | Needs overrides |
| 17 | Pax-LiveCancelled | train detail | Fixture | Needs overrides |
| 18 | Pax-LiveTerminated | train detail | Fixture | Needs overrides |
| 19 | Pax-NotifyPermission | from "Notify me" | Fixture | No push backend |
| 20 | Pax-NotifyOn | from "Notify me" | Fixture | No push backend |
| 21 | Pax-PushExamples | system notification | Fixture | Visual example of the three notification texts |
| 22 | Pax-Alerts | Alerts tab | Live, partly | "Trains you follow" live from the status read; the message card is a fixture; with no messages the screen shows an empty state that is not drawn yet (gap G3) |
| 23 | Pax-Settings | Account tab (signed out), Settings | Local | Language, followed trains, privacy text; the Notifications row is replaced by "Not in this version" (Q2) |
| 24 | Pax-Tickets | My tickets tab | Local | The designed empty state; its two buttons become one line "Tickets are not available in this version" (Q5) |
| 25 | Pax-Auth-Gate | after "Buy ticket" | Fixture | Ticketing deferred; not reachable |
| 26 | Pax-Account | Account tab, signed in | Fixture | Needs passenger accounts |
| 27 | Pax-SignIn | Keycloak page | Deferred | Identity provider page (theme), with ticketing |
| 28 | Pax-SignIn-Error | Keycloak page | Deferred | Same |
| 29 | Pax-Register | Keycloak page | Deferred | Same |
| 30 | Pax-Register-Error | Keycloak page | Deferred | Same |
| 31 | Pax-Register-Verify | Keycloak page | Deferred | Same |
| 32 | Food-Menu | from Home / live screen | Live if spec 11, else Fixture | Menu from the caterer feed; read-only; order on the caterer's website (D-71) |
| 33 | Food-Menu-Offline | same | Live if spec 11, else Fixture | "Menu from 07:12. It may have changed." |
| 34 | Food-Menu-Disrupted | same | Fixture | Needs cancellation/termination data |

Count (checked against the table, 34 rows): **Local 7** (rows 1 to 5, 23, 24), **Live 10** (rows 6 to 14 and 22, which is partly live), **Fixture 10** (rows 15 to 21, 25, 26, 34), **Food menu 2** (rows 32 and 33: Live if spec 11 lands, otherwise Fixture), **Deferred 5** (rows 27 to 31). 7 + 10 + 10 + 2 + 5 = 34.

**Row 12 (the connected live board) is partly not buildable.** The canvas board `Main` also shows "Reason: Signal issue", "Verified by Operations · Checked at 08:33" and a "Notify me" button. The reason and the verification line need the verification slice (FR-52, D-10) and "Notify me" needs push (Q4), so the built screen omits all three. It keeps the header ("Updated 08:42"), the title and its two other languages, the primary ETA card with the status chip, "About 10 min late. Updated 08:42." and the stop list. The overdue board `Kasuni-Live-Overdue` (row 13) matches rule 5 as drawn.

Other boards in the canvas that this spec does not build: `Components-Mobile` (the component reference the widgets follow), Brand boards (tokens), Display-* (spec 09), Kiosk-Results (terminal, Could), Dash-* (spec 08 and later), Nimal-* (spec 05).

### Gaps in the design (screens this app needs that are not drawn)
- **G1 Results offline or stale:** results shown from an old fetch with "Showing info from HH:mm".
- **G2 Live status: journey completed ("Arrived") and "no journey found today".**
- **G3 Alerts empty ("No service alerts").**
- **G4 First launch offline with no cached stations ("Connect once to load the stations").**
- **G5 Account tab signed out** (only the signed-in Account is drawn).
- **G6 Home banner when the line status cannot be loaded.**
I propose drawing G1 to G6 as hi-fi boards before the build (Q6).

## 4) API Surface
The app calls only public endpoints, with no token:
- `GET /api/v1/stations` (ETag, `If-None-Match`) -> names in en, si, ta, line sequence, `version`.
- `GET /api/v1/trains?from=&to=&date=` -> trains with `atFrom`/`atTo` arrival and departure (scheduled and estimated), `delayMinutes`, `statusGroup`, `statusKey`, `live`, `journeyId`, `lastEventAt`, `serverTime`.
- `GET /api/v1/trains/{trainNumber}/status` and `GET /api/v1/journeys/{id}/status` -> `statusKey`, `delayMinutes`, `delayKnown`, `primaryEta`, `lastKnownPosition` (state, departedAt, arrivedAt), `stops[]` (scheduled, estimated, `actualArrival`, `actualDeparture`, `confirmation`), `version`, `serverTime`.
- `GET /api/v1/lines/MAIN/status` -> `statusKey` (Normal, SomeDelayed), `journeysInProgress`, `lastEventAt`.
- (If spec 11 lands) the menu read.

**Two small additive backend changes** (both public reads; update the contract tests of specs 03 and 04):
1. **`lastEventAt` on the journey and train status snapshot** (the time the server received the latest accepted report, null before the first). The live screens show "Updated 08:42" and "Last update 08:28" and FR-72 asks for it. Spec 04 deferred it as D9; this closes D9 for the status read.
2. **`platform` on each train in the search result** (effective platform code or null), taken from the planned platform seeded in spec 09. Until that migration exists the field is null and the app shows a dash, as the cancelled row in the design does. If you drop spec 09's seed change, drop this one too.

Errors: the app treats 404 `station_not_found` or a missing train as "not found" screens, 5xx and timeouts as unreachable (rule 7), and never shows a ProblemDetails body to the passenger.

## 5) Data Contract
- No backend tables, no migration.
- On the phone (plain JSON in `shared_preferences`, nothing sensitive, no account): chosen language, onboarding done, From station, recent stations, followed trains `[ {trainNumber, fromCode, toCode} ]`, the stations list with its ETag, the last status snapshot per followed or recently opened train (with its `serverTime`). No identifier of the phone is sent or stored (Settings privacy text says so; the design's wording about a device used to send alerts is replaced because no alerts exist).
- Demo data: the existing seed (8058, stations VYG, GPH, RGM, MDA, CF).

## 6) Business Rules
1. **Language first (AC-E06-04 5, FR-69).** On first launch the splash is followed by language choice before anything else. All interface text, statuses and station names use the chosen language; a missing translation falls back to English.
2. **Times (ADR-06, A-TZ).** The API sends UTC; the app shows Asia/Colombo (fixed offset +05:30, no daylight saving). "Updated" and "Info from" use the API's `serverTime` or `lastEventAt`, never the phone clock. The app calculates no ETA; it only picks which one to show.
3. **Primary ETA (D-38, FR-66, AC-E06-02 1, 2).** The boarding station is the From the passenger chose (for a followed train, its saved `fromCode`; if unknown, the first stop). While the train has not departed that stop, the primary ETA is the estimated arrival there (its estimated departure if it is the first stop), labelled "Arrives at Gampaha". Once the From stop has an actual departure, or a later stop is reported, it switches to the arrival at the destination ("Arrives at Colombo Fort"). One stop list shows progress and ETA together (AC-E06-02 6).
4. **Statuses use icon and words, never colour alone (NFR-30).** Fixed list: On time; Delayed N min; Scheduled (a timetable-only train, marked not live); Waiting for update; Arrived (statusKey Completed). Chips follow the design (normal blue outline, warning thick outline, unknown dashed). Cancelled, Suspended and Terminated exist only in the fixtures.
5. **Delay known or not (D-39, spec 06 R7).** When `delayKnown` is false the ETA is replaced by "Waiting for update", the stop ETAs show a dash, and the text reads "No news yet. Last update 08:28. Last seen: departed Veyangoda at 08:28." This is the only train-silence state the data can produce; the real overdue timer is not built.
6. **A passed stop (AC-E06-02 5, P-07).** A stop marked Inferred shows "Passed" and no "missing confirmation" wording.
7. **Two kinds of silence (FR-67, AC-E06-03 3, 4, 7).** The app is "offline" when the phone has no connection or when 30 seconds of polls have failed. If the phone has a connection but the server does not answer: "Live updates unavailable". In both cases the last snapshot stays, the ETA is marked "Last known", and the banner shows "Info from HH:mm" (the snapshot's `serverTime`). The first good response removes the banner without a restart. The offline state is visible within 30 seconds (NFR-03).
8. **Polling.** Live status every 10 s and Results/Home every 30 s, only while the app is in the foreground and the screen is open; paused in the background; one refresh on resume and on pull-to-refresh; no overlapping requests; no retry loop faster than the interval (NFR-13 is deferred, the app must not hammer the API).
9. **Remembered origin (AC-E06-01 4).** The From station is saved on the phone, no location permission. Opening the app again preselects it.
10. **Follow (AC-E06-04 1, 2).** "Follow this train" is a local bookmark (no account, no server): it puts the train on Home as the first card with its ETA, status and freshness, and in Settings and Alerts. With none followed, search is first and no empty card appears. The follow is removed when the journey is Completed or 24 hours old.
11. **Stations offline (FR-65).** The list is fetched with its ETag at launch, replaced only when the version changes, and used from the cache when offline. With no cache and no network, the first-launch offline screen (G4) is shown; nothing else blocks.
12. **Accessibility (AC-E06-05, NFR-32, NFR-33).** The screen reader reads train, destination, ETA, status, in that order. Only meaning changes are announced: status key changes (on time to delayed, delayed to waiting, to arrived), going offline or back, and (fixtures) disruptions with priority. An ETA moving from 08:40 to 08:41 announces nothing (tested). Text scales to 200 % without clipping a time or a status; touch targets are at least 44 px (56 px primary buttons).
13. **No reason, no badge until verified (D-10).** Delay and ETA only; no reason line is shown or invented.
14. **No dead ends in the shipped flow (Q2).** Buy ticket, Notify me and Food menu entry points are not shown unless the feature works.
15. **No sensitive data.** No account, no phone identifier, no location; logs contain station codes only after validation, never raw input.

## 7) Architecture (follow ADR-16 and the operator app)
- `mobile/passenger_app`: feature-first folders `lib/features/{onboarding,stations,search,live,home,follow,alerts,settings,tickets,gallery}/{data,domain,presentation}` and `lib/core/{api,config,l10n,theme,time,widgets}`.
- State: Riverpod (plain providers and `Notifier`, as the operator app); repository interfaces for stations, trains, status, line status and local preferences, implemented over `dio` and `shared_preferences`; fake repositories in tests.
- Navigation: `go_router` with the bottom bar of the design (Trains, My tickets, Home raised in the middle, Alerts, Account).
- l10n: Flutter gen-l10n ARB files `app_en.arb`, `app_si.arb`, `app_ta.arb`; all text through them; a test fails if a key is missing in a language (as the display spec). Noto Sans, Noto Sans Sinhala and Noto Sans Tamil bundled as assets so the script renders offline (Q9); line heights per script from the tokens.
- Theme: Dart theme generated from `smartrail-tokens.json` by the existing `tool/generate_tokens.dart` approach, teal accent, light only, components following `Components-Mobile`.
- Connectivity: `connectivity_plus` plus the 30 s failed-poll rule (rule 7).
- Gallery: `lib/features/gallery` lists the 34 boards and opens each state from fixtures; compiled only in debug builds.
- Config: `--dart-define=API_BASE_URL` (default `http://10.0.2.2:8080`, the emulator host); debug-only cleartext allowance as in the operator app.
- Quality: `flutter analyze --fatal-infos` clean, `flutter test` green, a `mobile` CI workflow path-filtered to `mobile/passenger_app` (the existing `mobile.yml` extended or copied).
- Telemetry: none in the app (as spec 05 Q7); the server counters (`smartrail.trains.searches`, line and status reads) show the effect in Grafana.
- Backend: two additive fields (section 4) in the existing handlers and snapshot builder, with tests.

## 8) Edge Cases
| Case | Handling |
|---|---|
| First launch, no network, no cached stations | G4: "Connect once to load the stations", Retry; language and onboarding still work |
| Stations cached, no network | Pickers work from the cache; Results show "You're offline" (G1) |
| No trains left today | Results-Empty with the first train tomorrow (`date` = tomorrow) |
| Opposite direction (the seed has inbound trains only) | Empty list; the text is neutral ("No trains found") because the app cannot tell it from "no trains today" (spec 04 D8) |
| Unknown train number or no journey today | G2 "No journey found for this train today" |
| Train arrived (Completed) | G2 "Arrived"; the follow is removed |
| Delay Unknown | "Waiting for update" (rule 5) |
| Phone offline on the live screen | Offline banner, last known ETA marked (rule 7) |
| Online but API down | "Live updates unavailable", same data kept |
| Slow API, repeated failures | No faster polling; banner after 30 s |
| Night train past midnight | Times come from the API's instants; the date line uses the operating date |
| Followed train not found | The Home card shows "No journey found" with Remove |
| Language changed while a screen is open | Rebuilds in place; no restart |
| Text scale 200 % | No clipped time or status |
| Screen reader | Order and announcements as rule 12 |
| App killed and reopened | Language, From, follows and caches restored |
| Rotated phone | Portrait only |

## 9) Acceptance Criteria
- [ ] First launch asks for a language before anything else; the choice persists; all interface text, statuses and station names appear in it (AC-E06-04 5, AC-E06-05 1).
- [ ] From and To lists come from `GET /stations` in line order and work offline from the cache; From is remembered with no location permission (AC-E06-01 4).
- [ ] Veyangoda to Colombo Fort shows 8058 with scheduled time, estimated time, status chip and platform or dash (AC-E06-01 1).
- [ ] With 8058 running +8, the results show 08:38 beside "Sched. 08:30" (AC-E06-01 2).
- [ ] The live screen shows "Arrives at Gampaha 08:38" with "Delayed 8 min" and no reason; after the train departs Gampaha it shows the arrival at Colombo Fort (AC-E06-02 1, 2, 3).
- [ ] A passed stop shows "Passed" with no missing-confirmation wording; one stop list holds progress and ETA (AC-E06-02 5, 6).
- [ ] An Unknown delay shows "Waiting for update", dashes for stop ETAs, and the last update and last seen text (AC-E06-03 1, partial).
- [ ] Offline within 30 s: banner "You're offline · Showing info from HH:mm", the last ETA kept and marked; "Live updates unavailable" when only the server is unreachable; the first good response restores live data (AC-E06-03 3, 4, 7).
- [ ] The Home card appears for a followed train with ETA, status and freshness; with none, search is first and no empty card shows (AC-E06-04 1, 2, 4).
- [ ] Settings changes the language immediately and lists followed trains with a way to remove them.
- [ ] Statuses are distinguishable without colour; text at 200 % does not clip; a screen reader reads the screen in the required order and announces only meaning changes (an ETA tick from 08:40 to 08:41 announces nothing) (AC-E06-05 2 to 5).
- [ ] The design gallery (debug) opens every state of the 34 boards from fixtures; each fixture screen has a widget test.
- [ ] The two backend additions are tested; the existing tests of specs 03 and 04 still pass.
- [ ] No dead-end buttons in the shipped flow (rule 14).
- [ ] `flutter analyze --fatal-infos` clean, `flutter test` green, the `mobile` workflow green, and the end-to-end run on the emulator against Compose recorded with screenshots.
- [ ] The report states plainly that FR-75/FR-76 (push) are not met, that suspended, cancelled, terminated, verified reason and alerts are fixtures only, and that Sinhala and Tamil text is unverified (until checked).

## 10) Test Plan
### 10.1 Seed and fixture data
The Compose seed (8058, 8061, VYG, GPH, RGM, MDA, CF) and JSON fixtures for every state: delayed, on time, scheduled-only, delay unknown, completed, not found, plus the fixture-only states for the gallery. A fake clock for polling and the 30 s rule.
### 10.2 Unit tests
Primary ETA selection (before departure, after departure, first stop, skipped stop, last stop); chip mapping per status; time formatting Asia/Colombo including a night train; the offline rule (no connection, 30 s of failures, recovery); follow expiry; the meaning-change detector (status changes announce, ETA ticks do not); ARB completeness (every key in en, si, ta); station fallback to English.
### 10.3 Widget tests
Each Live and Local screen with fake repositories, in English and once in Sinhala and Tamil for line height and clipping; text scale 2.0; semantics order of the live screen; every fixture screen renders without overflow.
### 10.4 End to end (emulator against Compose)
Flow 4 of the five: Nimal reports Veyangoda departed 08:28 through the API; the app search shows 08:38 and the live screen shows the new ETA; stopping the network shows the offline banner. Run as an `integration_test` where practical, otherwise manually with screenshots recorded in the PR (as spec 05).

## 11) References
- Specs 03 (status rules R5, R6, R15, R16), 04 (endpoints, caching, D5, D8, D9), 05 (project setup, tokens, test style), 06 (`delayKnown`), 09 (platforms), 11 (menu, if built); `docs/smartrail-acceptance-criteria.md` E06 and E07; FR-65 to FR-76, FR-90, FR-91; ADR-16; discovery 19.1 and 19.2.
- Existing code reused: `mobile/operator_app` (theme generator, `Clock`, `strings`, queue-free parts of the API client, test helpers), `smartrail-tokens.json`.

## 12) Open Questions (recommendation first)
- **Q0. Resolved in review (16 Oct): the counts in section 3 were wrong.** Corrected there and in section 0 from the table itself.
- **Q1. Scope split.** Recommend the Live / Local / Fixture / Deferred split of section 3. Alternative: build only rows 1 to 14 and drop the gallery (saves about half a day, loses the demonstrable unbuilt states).
- **Q2. Entry points for unbuilt features.** Recommend hiding Buy ticket, Notify me and Food menu in the shipped flow and showing them only in the gallery, so there are no dead ends (rule 14). Alternative: show them as designed with a "not in this version" notice (closer to the design, but a button that does nothing useful).
- **Q3. Translations.** Recommend English as the source, Sinhala and Tamil written for the same keys and marked unverified until a native speaker checks them. Do you have someone? If not, the report must say so. Alternative: English only plus the three-language station names and language names already in the design (fails AC-E06-05 1, which is a Must).
- **Q4. Notifications (Must, unmet).** Recommend leaving FR-75 and FR-76 unmet and saying so, with the three boards as fixtures. Alternative: a small push slice (a push provider, token storage, an event consumer) that I estimate at a day and a half; I do not think it fits.
- **Q5. Tabs and the empty tabs.** Recommend keeping the five-tab bar as drawn (consistency), with My tickets showing its designed empty state and Account (signed out) showing the Settings content. That changes two designed screens slightly; say if you would rather drop the two tabs.
- **Q6. Missing boards G1 to G6.** Recommend I draw them before the build so the app has something to match. They are small.
- **Q7. Local storage.** Recommend `shared_preferences` with JSON (no database needed, nothing relational). Alternative: `drift` as in the operator app (more setup, no benefit here).
- **Q8. The two backend additions** (`lastEventAt` on the status snapshot, `platform` on search results). Recommend yes; both are additive and small.
- **Q9. Fonts.** Recommend bundling Noto Sans Sinhala and Tamil as assets (works offline, adds a few MB). Alternative: the platform font (Sinhala and Tamil may render poorly on some Android builds).
- **Q10. Time and cut order.** This assumes the freeze is lifted for it and that you can give it about 2.5 days. Cut in this order: the gallery beyond the six live-state fixtures, Settings extras, the Alerts screen, then the Home followed card. Never cut language choice, search, results, live status, and the offline state. Confirm, or tell me what the real time budget is.

## 13) Review of 16 Oct 2026 (before approval)
Checked against the code, `docs/`, discovery and the canvas (version 1792037517). What held: the endpoints and fields in section 4 exist as described (`atFrom`/`atTo`, `delayKnown`, `primaryEta`, `lastKnownPosition`, `stops[]` with `confirmation`, `serverTime`, line status); AC-E06-01 to 05 say what the spec quotes; FR-65 to FR-72, FR-75, FR-76, FR-90, FR-91, NFR-31, NFR-34, D-10, D-25, D-31, D-38, D-39, D-52 and P-07 exist; spec 04 D5, D8 and D9 are the deferred items named; the canvas has exactly 34 passenger boards (28 `Pax-*`, `Main`, two `Kasuni-Live-*`, three `Food-Menu*`); `mobile/operator_app` has the toolchain, `go_router`, `connectivity_plus`, `intl`, `clock` and the token generator the spec reuses; the token file has the teal passenger accent.
1. **The counts in sections 0 and 3 were wrong** (the draft said so in Q0). Corrected.
2. **Row 12 cannot be built as drawn:** the board shows a verified reason and a Notify me button. The note in section 3 says what is left out.
3. **Dependency on spec 09 (section 4, change 2).** The planned platforms are seeded by spec 09, which is built and committed on `claude/feature/station-displays` but not yet pushed or merged. The `platform` field in search results works only after that is on `dev`. Until then the field is null and the app shows a dash. The `lastEventAt` change (change 1) does not depend on it.
4. **D-71 is quoted from `CLAUDE.md`** (scope decisions of 12 Oct); it is not in the discovery document. It is only used to say the menu is read-only.
5. **The edge-case numbers** the draft said it had not looked up are filled in (section 1).
6. **Time.** Today is Fri 16 Oct and the deadline is Tue 20 Oct, so the 2.5 working days of this spec are most of what is left, with the end-to-end checks and the report still to do. Section 12 Q10 says what to cut; the owner decides the scope before building starts.
7. **Not checked:** the exact wording of the Sinhala and Tamil text in the boards (copied from the canvas where it exists; everything else is unverified, Q3), and that the canvas boards in section 3 render as the spec describes (only `Main` and `Kasuni-Live-Overdue` were opened for this review).

## 14) Owner decisions of 16 Oct 2026
- **Scope and time (Q1, Q10): core first, with checkpoints.** Phase 1 (nothing in it is cut): language choice, onboarding, stations and search, results, live status with the offline and "Waiting for update" states, Home with a followed train, Settings, the local-only screens, and the two backend additions. Phase 2, only if time remains: the fixture screens and the design gallery. A report is given after phase 1 and the owner decides whether to go on. Cut order inside phase 2 is as in Q10.
- **Order: spec 09 is merged first.** `claude/feature/station-displays` was pushed on 16 Oct; the owner merges its pull request into `dev`, and this spec's branch (`claude/feature/passenger-app`) starts from the updated `dev`, so the `platform` field of change 2 has data.
- **Missing screens G1 to G6 (Q6): built in the same style without new boards.** They follow `Components-Mobile` and the existing boards, and the six are listed in the report for the owner's review.
- **Translations (Q3): unverified placeholders.** English is the source; Sinhala and Tamil are marked unverified in the ARB files and in the report until a native speaker checks them.
- **Recommendations not yet answered by the owner, assumed as written and to be confirmed at approval:** Q2 (hide entry points for unbuilt features in the shipped flow), Q4 (push stays unmet, said plainly), Q5 (keep the five tabs), Q7 (`shared_preferences`), Q8 (the two backend additions), Q9 (bundle the Sinhala and Tamil fonts).

