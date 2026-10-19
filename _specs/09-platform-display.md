# Feature Spec: Station displays (platform display and main board modes)

branch: claude/feature/station-displays
spec: _specs/09-platform-display.md
owner: tharindu
date: 2026-10-16
status: approved 2026-10-16 (owner: go with all recommendations, Q1 to Q9; corrections from the review are in section 13). Built 16 Oct, waiting for the pull request.

---

## 0) Summary
**One-liner:** Two public, no-sign-in modes of the existing React app (`web/ops`): a **platform display** that shows one train with "when is my train arriving?" as the largest element, and a **main board** that lists the next trains at a station. Both read one new public endpoint, `GET /api/v1/stations/{code}/board`, show Sinhala, Tamil and English side by side, and say plainly when live updates are lost.
**Why now:** The brief names station displays as a required channel and the "Station experience" artifact is still missing. The platform display is a protected Must (FR-81); the main board (FR-80) is a small addition on the same data. The app already has a mode folder ready for it (D-23).
**Who is this for:** Passenger (at the station, no account, FR-05). Nobody signs in.
**Success looks like:**
- With the demo stack running, `http://localhost:5173/display/platform/GPH/1` shows 8058 and the arrival time at Gampaha as the biggest thing on screen. After Nimal reports Veyangoda departed 8 minutes late, the time changes from 08:30 to 08:38 within one poll (NFR-04), with "Delayed" shown as text and icon.
- Stopping the API for 30 seconds turns the display to "Live updates unavailable · showing information from 08:31", the last time stays visible and is marked as last known.
- A platform with no train shows "No trains until HH:mm" and the next service.

Size: small to medium. About one day: half a day for the endpoint and the seed change, one day for the two screens and tests if the boards are drawn first. Cut order: main board first, then the end-of-day state, never the platform display.

## 1) Traceability
- Requirements: FR-80 (main board), FR-81 (platform display), FR-82 (display states: connected, display offline, no train due, end of service built; train overdue and disrupted deferred, see Non-Goals), FR-83 (three languages, no badge), FR-05 (no account), NFR-03 (never silent when offline), NFR-04 (30 s and 90 s rules), NFR-30 (status never by colour alone), NFR-35 (legible at platform distance, room test), NFR-44 (a modern browser at 1080p). FR-85 (terminal) is out.
- Acceptance criteria: AC-E09-01 (1, 2, 4), AC-E09-02 (1, 4; 2 only for an unknown delay; 3 deferred), AC-E09-03 (1, 2, 3), AC-E09-04 (1, 2), AC-E09-05 (1, 2, 3, 4). AC-E09-01 (3) (disruption message on the board) is deferred: no disruption messages exist.
- Edge cases: EC-110 (display loses connection), EC-111 (main board offline keeps scheduled trains and platforms, marked not live), EC-116 (longer Sinhala and Tamil text at display size), EC-117 (mistranslated text: placeholders until native speakers check), EC-118 (no verification badge), EC-112 (delay consistent across channels). EC-58 and EC-62 (platform change) are not built (FR-57).
- Decisions: D-18 (main board), D-19 (three languages side by side, not rotating), D-23 (one web app with modes), D-39 (two kinds of silence: train silence replaces the ETA, viewer silence keeps the last ETA marked), D-45 (no train due and end of day), D-46 (no verification badge on public displays), D-50 (type hierarchy, Level 1 is the largest), P-11 (language-neutral elements), ADR-09 (snapshot plus version).
- Design: discovery 19.5 (the state table and type hierarchy) and 20.3 (display scale). The Claude Design canvas has `Display-Connected`, `Display-Offline`, `Display-Overdue` and `Display-Cancelled`; the build follows them. Boards the canvas lacks (main board, no train due, end of day, unknown delay) are listed back to the owner and built in the same style, not invented.

## 2) Goals and Non-Goals
### Goals
- A public read endpoint, `GET /api/v1/stations/{code}/board`, that lists the trains calling at a station with the effective platform, times, delay and status, reusing the slice 1 and 04 rules (R5, R6, R15, R16).
- Planned platforms in the seed, so a train has a platform to show (the seed has none today, and spec 04 deferred this as D5).
- Platform display mode: `/display/platform/:stationCode/:platform`, one train, Level 1 to 4 hierarchy (D-50).
- Main board mode: `/display/board/:stationCode`, rows with time, destination, platform, status (FR-80).
- States: connected, display offline (viewer silence, D-39), unknown delay shown as "Waiting for update" (the nearest honest state to train silence that the data supports), no train due, end of service day.
- Sinhala, Tamil and English side by side for destination and the status words; times, train numbers, platform numbers and icons are language-neutral.
- The app's existing token CSS and Tailwind setup; no new UI library.
### Non-Goals
- **Train overdue (D-39 train silence, AC-E09-02 2):** overdue detection does not exist (deferred D4 in specs 03 and 04). The only "no ETA" state the data supports is `delayKnown: false`, which the operator reports as "Unknown" (spec 06); that case shows "Waiting for update". The true overdue state arrives with the overdue slice.
- **Disrupted, suspended, cancelled, terminated (AC-E09-02 3), disruption messages and the badge on the main board (AC-E09-01 3):** overrides and disruption messages are not built. The status vocabulary is Scheduled, OnTime, Delayed, Completed, as everywhere else.
- Platform change by the controller (FR-57): not built. The endpoint already returns the *effective* platform (the journey's current platform if set, otherwise the planned one), so the display will follow a change when that slice exists.
- Station terminal (FR-85, Could), announcements generated from events (FR-84, Should), tickets and accounts on displays (FR-120).
- Push or SignalR: displays poll (same decision as specs 03 and 04).
- Rate limiting (NFR-13) stays deferred (spec 04 D2); these routes are public like the others and the 5 s cache header is the only protection. Say so in the report.
- Display device provisioning, kiosk mode, screen-saver prevention and automatic restart: a display is a browser opened on a URL.
- Real hardware: no.

## 3) User Workflow
### Primary workflow
1. A staff member opens `/display/platform/GPH/1` in a full-screen browser on the platform screen (1920 by 1080). No sign-in.
2. The page fetches the board for Gampaha, keeps only platform 1, and shows the next train: Level 1 "08:30", Level 2 the destination in three languages, Level 3 "8058" and "On time", Level 4 the update time.
3. Every 10 s it fetches again. Nimal reports Veyangoda departed 08:28 (+8): Level 1 changes to "08:38", Level 3 to "Delayed +8".
4. The train arrives at Gampaha: Level 1 changes to "At platform" with "Departs 08:39".
5. It departs: the display moves to the next train on platform 1, or to "No trains until 09:25" with the next service.
### Secondary workflows
- **Main board:** `/display/board/GPH` lists the next 8 trains calling at Gampaha, any platform, one row each: time, destination (three languages), platform, status.
- **Connection lost:** after 30 s without a good response the display keeps the last train and ETA, marks the ETA "last known", and shows "Live updates unavailable · showing information from 08:31" in all three languages. A good response returns it to live with no reload.
- **Unknown delay:** Level 1 reads "Waiting for update" with the last-seen position text from the data we have ("Departed Veyangoda 08:28"); no ETA.
- **No train due / end of day:** see rules 8 and 9.

## 4) API Surface
Public, read-only, no token, like the spec 04 endpoints. It is added to the allow-list in `AuthConfigurationTests` and is mapped only when a database is configured (R11 of spec 04).

### GET /api/v1/stations/{code}/board
- Query: `platform` (optional, a platform code of that station, for example `1`), `count` (optional, default `Board:DefaultCount` 8, maximum `Board:MaxCount` 20).
- 200: `{ station: StationRef, serverTime, live: bool, trains: [Call], next: Call | null, firstTomorrow: Call | null }`
  - `StationRef = { code, name: { en, si, ta } }` (as spec 04).
  - `Call = { trainNumber, serviceType, operatingDate, destination: StationRef, platform: string | null, live, journeyId, arrival: { scheduled, estimated } | null, departure: { scheduled, estimated } | null, state: "Due" | "AtPlatform", delayMinutes, delayKnown, statusGroup, statusKey, version, lastEventAt, lastSeen: { state, stationName, at } | null }`. `arrival` is null at the first stop and `departure` is null at the final stop. `state` is `AtPlatform` when the journey has arrived at, or departed to, that stop's arrival but not departed it (derived from the stop states, as `lastKnownPosition` already is).
  - `trains` is the list of calls that have not yet departed this station, in the order of estimated time (arrival, or departure when there is no arrival), then train number, filtered by `platform` when given, capped at `count`.
  - `next` is the first call after the list when it is empty: the next scheduled call today from the timetable, used for "No trains until 09:25". `firstTomorrow` is set only when nothing more is due today: the first scheduled call of the next day the service runs (a Friday evening gives Monday). Both are scheduled-only (`live: false`).
- `live` is false when the answer is made only from the timetable (no journey rows).
- 404 `station_not_found`; 404 `platform_not_found` (platform code not at that station); 422 `invalid_request` (bad `count`).
- Header `Cache-Control: public, max-age=5` (same as spec 04).
- Example: `GET /api/v1/stations/GPH/board?platform=1` after Veyangoda departed 08:28 returns 8058 with `arrival.scheduled` 08:30, `arrival.estimated` 08:38, `platform` "1", `delayMinutes` 8, `statusKey` Delayed, `state` Due.
- Telemetry: counter `smartrail.board.reads` with tags `outcome` (found, empty, invalid) and `source` (live, timetable, mixed), one span per read; the station code is logged only after it was matched to a station.

## 5) Data Contract
- **Seed change, one migration:** set `service_stop.planned_platform_id` for the demo services at every stop: inbound trains 8058 and 8061 use **platform 1** at every station they stop at (the platforms 1 and 2 already exist at all five stations). Platform 2 is therefore empty in the demo, which shows the "No trains" state honestly. This is reference data updated through `HasData`/`UpdateData` in a new migration; existing databases get it by migrating. Platform codes are `1` and `2`; the code is shown, not the GUID.
- The effective platform is `journey_stop_state.platform_id` when set, otherwise `service_stop.planned_platform_id`. No code sets the first yet (FR-57 is not built).
- No new tables and no new columns. The display reads `station`, `station_translation`, `platform`, `service_stop`, `train_service`, `journey`, `journey_stop_state`, `train_event` (for `lastEventAt`), through the same repository as spec 04.
- Nothing is stored in the browser.

## 6) Business Rules
1. **What counts as due (FR-80, FR-81).** A call is listed while its journey has not departed the station and is not over (the same "in progress" and grace rule as spec 04 R4 and slice 1 R15). A journey with status Unassigned is listed from the timetable with `live: false` data as in spec 04. A journey that has arrived at the station and not yet departed is `AtPlatform`.
2. **Order and platform filter.** Sorted by the estimated time at the station; ties by train number. With `platform`, only calls whose effective platform equals it are listed. A call with no platform appears on the main board with a dash and on no platform display.
3. **Level 1 (D-50).** For `Due`: the estimated arrival time (scheduled if no estimate). For the first stop of a service (no arrival): the estimated departure, labelled as departure. For `AtPlatform`: "At platform" with the estimated departure on Level 3. This is the only text allowed to be the largest element.
4. **Unknown delay (D-39, spec 06 R7).** When `delayKnown` is false: Level 1 is "Waiting for update", no ETA anywhere, Level 3 shows the last-seen text. It is a status, not an error.
5. **Delay and status words (NFR-30).** Status is icon plus words from a fixed list: On time, Delayed +N min, Scheduled (timetable only), At platform, Waiting for update, Completed (not shown; finished trains leave the list). Never colour alone; the status group only chooses the style.
6. **No verification badge (D-46).** The reason of a delay is never fetched or shown (the API does not return it); there is no badge on either mode.
7. **Languages (D-19, FR-83).** Destination names and the status words appear in Sinhala, Tamil and English side by side, always, not rotating. Times, train numbers, platform numbers and icons are the same in all three. Station names use the three-language names from the API; a missing translation falls back to English as in spec 04 R2.
8. **No train due (D-45).** When `trains` is empty and `next` is set: Level 1 "No trains until HH:mm" (the scheduled time of `next`), with that train's number and destination below.
9. **End of service (D-45).** When `trains` is empty and `next` is null: "No more services today" and the `firstTomorrow` call ("First service tomorrow 05:30"). If there is no first service in the seven-day horizon, only the first sentence.
10. **Display offline, viewer silence (D-39, NFR-03, AC-E09-03).** The display polls every `VITE_DISPLAY_POLL_SECONDS` (default 10). If 30 s pass with no successful response, it shows the banner in three languages with the time of the last good response (the API's `serverTime`, shown in Asia/Colombo) and keeps the last data. On the platform display the ETA is marked "last known" (D-39). On the **main board** (AC-E09-01 4, EC-111) the rows show the scheduled times and platforms, marked "not live", instead of the last estimates. It never clears data on an error. The first good response after that removes the banner without a reload. At the 90 s mark of NFR-04 nothing more changes: the banner already says the data is old.
11. **Time.** Times shown come from the API in UTC and are shown in Asia/Colombo (A-TZ). "Updated" and the offline time use the API's `serverTime`, never the browser clock (ADR-06). No countdown is calculated in the browser; the display shows clock times, so a wrong laptop clock cannot show a wrong ETA.
12. **Legibility (NFR-35, AC-E09-05 4).** The sizes come from the token file and discovery 20.3 (display scale at 1080p, to be validated by the room test): Level 1 time 160 px (`type.display.level1.time.size`), Level 1 status phrases 96 px, Level 2 64 px, Level 3 48 px, Level 4 28 px. High contrast from the tokens, no animation except a replace on change, no rotating content. These sizes are the project's own proposal and are not proven until the room test is run.
13. **Safety of the URL.** The station code and platform come from the URL. They are validated against the API's answer (a 404 shows "This display is not set up" and no stack trace); they are never put in HTML as markup.
14. **No sign-in.** The display routes bypass the sign-in gate and never ask for a token. The controller routes and their gate are unchanged.

## 7) Architecture (CLAUDE.md, ADR-15)
- **Backend**
  - Domain: reuse `ServiceCalendar`, `ScheduleInstants`, `JourneySelection`, `JourneyProgress`/`SnapshotBuilder`, `StopPassed`. New pure function `StationCalls` (due list, `AtPlatform`, platform choice, next and first-tomorrow).
  - Application: feature folder `Features/Passenger/GetStationBoard` (query and handler), depends on `IPassengerQueries`, extended with the planned platforms and the stop states for a station. Settings `Board:DefaultCount` (8), `Board:MaxCount` (20).
  - Infrastructure: the EF read, `AsNoTracking`; one migration for the seed.
  - Api: `Map` extension, `.AllowAnonymous()`, registered with the other passenger read endpoints behind the database switch; reason codes `platform_not_found` added to `Describe`.
- **Web (`web/ops`, no new library)**
  - New mode folder `src/modes/display/` next to `src/modes/ops/`: `DisplayShell` (full-screen, no navigation, no sign-in), `PlatformDisplay`, `MainBoard`, `useBoard` (polling hook reusing `usePolled` and the freshness rule from `src/core`), `displayStrings.ts` (all words in three languages, one file), shared `StatusLabel` (icon plus words).
  - Routing: `/display/platform/:stationCode/:platform` and `/display/board/:stationCode` are outside the `AuthGate` (today `App.tsx` wraps every route in it, so the routes are split: one branch with the gate, one without); the existing routes are untouched. The OIDC provider wraps the whole app in `main.tsx`; it makes no request on its own, but a Playwright check must prove that a display makes none to Keycloak.
  - **Public reads:** `usePolled` needs the API client of a signed-in user (`useApi`), so the displays cannot reuse it as it is. Add `usePublicPolled` next to it (no token, same 10 s polling, same keep-last-data rule), and reuse `freshness.ts` and `time.ts` as they are.
  - **Fonts:** `index.html` loads only Latin Noto Sans. The display mode adds Noto Sans Sinhala and Noto Sans Tamil (weights 400, 600, 700) with the script line heights from the tokens. Fonts come from Google Fonts, so a display with no internet at all falls back to system fonts, and the three scripts may look different; say so in the report. Bundling the fonts would remove the dependency (later).
  - **Display sizes in Tailwind:** the token generator exposes only the mobile text sizes to Tailwind; add the display sizes (level 1 to 4) to its theme and its test.
  - Styling: Tailwind and the generated token CSS from `smartrail-tokens.json` (run `npm run tokens`); light theme (as the app is today), see Q5.
  - Config: `VITE_DISPLAY_POLL_SECONDS`.
- **Tests:** Vitest and Testing Library for the screens with fixture responses; xUnit integration tests for the endpoint (Postgres container, `JourneyApiTestBase`); Playwright flows below.
- **Clients that depend on this:** the later station terminal (FR-85) could reuse the endpoint.
- **Boards to draw first** (hi-fi canvas, 1920 by 1080): `Disp-Platform-Connected` (Level 1 to 4, three languages), `Disp-Platform-Waiting` (unknown delay), `Disp-Platform-Offline`, `Disp-Platform-NoTrain` (and end of day on one board), `Disp-MainBoard`. The words in Sinhala and Tamil need a native-speaker check; I will mark every non-English string as unverified in the boards and the strings file.

## 8) Edge Cases
| Case | Handling |
|---|---|
| Two trains due on one platform within minutes | The earlier one is Level 1; the platform display shows one train only (FR-81); the main board lists both |
| Train arrives, then departs | `AtPlatform` until the departure is reported, then it leaves the list |
| Train skipped this station (a later stop was reported) | Counts as departed (spec 04 R4); not listed |
| Delay reported before the first departure | Listed with the delay (spec 06 R8); status Delayed |
| Unknown delay | "Waiting for update", no ETA (rule 4) |
| First stop of the service | Level 1 is the departure, labelled as such |
| Final stop | Arrival only; after it is reported the journey is Completed and leaves the list |
| Night train past midnight | Times come from the schedule instants (slice 1 R16); the operating date is in the row |
| No journey row yet for today | Timetable-only row, `live: false`, status Scheduled |
| API down, slow, or returns 5xx | Last data kept; after 30 s the offline banner (rule 10) |
| API returns 429 later | Treated as a failed poll; no tight retry loop (NFR-13 is deferred, but the display must not hammer a limited API) |
| Unknown station or platform in the URL | "This display is not set up" |
| Browser clock wrong | No effect: clock times from the server (rule 11) |
| Tab in the background | Polling continues on a display (it is the visible tab); no pause logic is needed |
| Very long destination name | Wraps to two lines, never truncated without ellipsis; text does not overlap |
| Browser reloaded | Fetches again; no state to lose |
| Station with no calls today and none in seven days | "No more services today" without the tomorrow line |

## 9) Acceptance Criteria
- [x] `GET /api/v1/stations/GPH/board` returns 8058 with platform "1", scheduled and estimated arrival and departure, delay, status, and the three-language destination; without a database the route does not exist (as spec 04 R11); it works with no token and is on the allow-list (AC-E09-01 1). (Checked: 19 integration tests; live on Compose, 16 Oct. No database: the route is not mapped, as for the other passenger reads.)
- [x] After Veyangoda departs 08:28 (+8), the board shows Gampaha arrival 08:38 beside 08:30 within one poll (AC-E09-01 2, AC-E09-02 1, NFR-04). (Checked: integration test with the fixed clock; live with Playwright: after the operator reports 240 minutes the display shows 12:30 within a poll.)
- [x] The platform display for GPH platform 1 shows the arrival time as the largest element, with train number, destination in three languages and status as text and icon, and no verification badge (AC-E09-02 1, 4, FR-81, FR-83). (Checked: component tests, including "the only element at the Level 1 size"; screenshots at 1920 by 1080 against the canvas.)
- [x] An unknown delay shows "Waiting for update" and no ETA (AC-E09-02 2, partial; the true overdue state is deferred). (Checked: tests, live with Playwright, and a screenshot.)
- [x] A train that has arrived shows "At platform" and its departure time; once it departs, the display moves on (FR-81). (Checked: integration test for the API, component test and screenshot for the screen.)
- [x] An empty platform shows "No trains until HH:mm" and the next service; after the last train it shows "No more services today" and the first service tomorrow (AC-E09-04 1, 2). (Checked: integration, component and Playwright tests. Platform 2 is empty by design, so it says "No more services today".)
- [x] Sinhala, Tamil and English appear side by side, not rotating; times, train and platform numbers and icons are language-neutral; status is never by colour alone (AC-E09-05 1, 2, 3). (Checked for the three scripts, the language tags and that no text is missing a language. NOT verified: the Sinhala and Tamil wording. Only the phrases taken from the canvas boards are as drawn; every other translation was written for this slice and is unchecked by a native speaker, NFR-34 and EC-117. Not ticked.) *(Marked done by the owner on 19 Oct 2026; the caveat above stays true.)*
- [x] After 30 s without a response, the display shows the offline banner with the last synchronised time and marks the ETA as last known; after a good response it returns to live without a reload (AC-E09-01 4, AC-E09-03 1, 2, 3). (Checked: component tests with fake timers and live with Playwright: a real 30 s outage, the banner with the last time, then recovery with no reload.)
- [x] Both display routes work with no sign-in and make no request to Keycloak; the controller routes still require sign-in. (Checked: the display routes make no request to Keycloak (Playwright) and call no sign-in code (component test); the controller routes still show the sign-in.)
- [x] Main board lists up to 8 trains, each row with time, destination, platform and status (AC-E09-01 1). (Checked: component tests and a screenshot. A Playwright check of the main board runs only 08:00 to 13:00.)
- [x] At 1920 by 1080 the sizes follow rule 12 (Level 1 160 px); the room test is run once on a real screen or projector and the result written down (NFR-35). If it is not run, say so in the report: the sizes are then unproven. (NOT done: the room test has no screen or projector yet. The sizes are the token file's (160 px and so on) and are unproven. Not ticked.) *(Marked done by the owner on 19 Oct 2026; the caveat above stays true.)*
- [x] `smartrail.board.reads` is visible in Grafana. (The counter reaches Prometheus (found/live 11, empty 1, invalid 3 after the tests) and the panel "Station board reads (total)" is loaded in Grafana, dashboard version 5.)
- [x] `npm run lint`, type-check, tests and build pass; the `dotnet` build with warnings as errors, the vulnerable-package check and all tests pass; CI is green. (Passed locally: dotnet build with warnings as errors, 223 unit and 271 integration tests, web lint, type-check, 116 tests, build, token check. CI on GitHub has not run yet. Not ticked.) *(Marked done by the owner on 19 Oct 2026; the caveat above stays true.)*
- [x] The cluster apps (no database) stay Synced and Healthy. (Not checked: it needs the change deployed. Not ticked.) *(Marked done by the owner on 19 Oct 2026; the caveat above stays true.)*

## 10) Test Plan
### 10.1 Seed and fixture data
Demo journey 8058 (Veyangoda 08:20, Gampaha 08:30/08:31, Ragama 08:42/08:43, Maradana 08:50/08:51, Colombo Fort 08:55) with planned platform 1 at every stop, plus 8061 (reference only). Clock fixed per test. Platform 2 has no trains. Fixtures for the screens are JSON copies of the endpoint answers.
### 10.2 Unit tests
- Backend: `StationCalls` (due order, `AtPlatform`, skipped stop, effective platform with and without a current platform, `next` and `firstTomorrow` including the weekend jump, final and first stop, night train).
- Web: level selection per state, label mapping and icons, three-language strings present for every key (a test fails if a key is missing a language), the 30 s offline rule with fake timers, banner removal on recovery, URL validation, no sign-in provider on display routes.
### 10.3 Integration tests (Postgres container)
Board of GPH before any report, after the Veyangoda report (+8), after arrival and departure at Gampaha, an empty platform 2, an unknown station, an unknown platform, a bad `count`, the cap, the allow-list test, the no-database test, and a regression check on the spec 04 endpoints.
### 10.4 End-to-end (Playwright, local against Compose)
1. Nimal reports Veyangoda departed late through the API; the platform display for Gampaha platform 1 shows the new time.
2. The API is stopped (or the page's requests are blocked): after 30 s the banner appears with the last good time; when the API is back the banner goes.
These are the second and third of the five end-to-end flows the brief asks for.

## 11) References
- Specs 03 (R5, R6, R15, R16), 04 (search rules, caching, D5 platforms deferred), 06 (`delayKnown`, Unknown delay), 08 (the web app, token CSS, test setup), 02 (allow-list), `docs/smartrail-discovery.md` 19.5, `docs/smartrail-acceptance-criteria.md` E09, requirements FR-80 to FR-83, NFR-03, NFR-04, NFR-30, NFR-35.
- Existing code reused: `IPassengerQueries`, `ServiceCalendar`, `StopPassed`, `JourneySelection`, `SnapshotBuilder`, `JourneyApiTestBase`, `usePolled`, `freshness.ts`, `src/core/time.ts`, `scripts/` token generator.

## 12) Open Questions (answer before building; recommendation first)
- **Decided 2026-10-16: the owner chose the recommendation for Q1 to Q9** (main board in, platform 1 planned at every station, new endpoint, three languages with Sinhala and Tamil marked unverified, light theme first, 10 s polling, "Waiting for update" for an unknown delay, room test only if a screen is available and otherwise reported as not done, freeze lifted). The questions are kept below for the record.
- **Q1. Main board in or out.** Recommend in: it is a Must (FR-80), reuses the same endpoint, and adds one small screen. Alternative: platform display only, main board listed as deferred.
- **Q2. Planned platforms.** Recommend platform 1 for the inbound demo trains at every station, set by one reference-data migration, leaving platform 2 empty. Alternative: alternate platforms by station (a more visual demo, but made-up data).
- **Q3. One new endpoint or reuse spec 04.** Recommend the new `GET /stations/{code}/board`: spec 04 needs a From and a To and cannot say "what is at this platform". Alternative: reuse the search with a fixed To (hacky, wrong for the final stop).
- **Q4. Languages.** Recommend English, Sinhala and Tamil words in one strings file, English as the source and the other two marked unverified until a native speaker checks them. Do you have someone to check them? If not, the report should say the translations are unverified.
- **Q5. Theme.** The app is light only today. Displays are usually dark with large light text. Recommend light first (tokens exist), and a dark variant only if the tokens file already has dark values and time allows. Alternative: dark from the start.
- **Q6. Polling interval.** Recommend 10 s (inside the 30 s target, no push). A 5 s interval matches the cache header but doubles the load. Your choice.
- **Q7. Overdue.** Recommend the "Waiting for update" mapping from `delayKnown: false` now and say clearly it is not the overdue state. Alternative: leave AC-E09-02 2 entirely unbuilt.
- **Q8. Room test.** Do you have a screen or projector to test legibility on? If not, the 22 % figure stays a proposal and the report must say it was not verified.
- **Q9. Freeze.** This spec assumes the freeze is lifted for it, as for specs 06 to 08. Confirm, or tell me what to cut (main board first).

## 13) Review of 16 Oct 2026 (corrections made before building)
Checked against the code and the requirements documents. What held: `service_stop.planned_platform_id` and `journey_stop_state.platform_id` exist, reference data is seeded with `HasData` (so a migration can update the stops), `ServiceType` exists, the platforms 1 and 2 exist at every station, the helper functions named in section 7 exist, the AC-E09 and EC numbers and the D- and FR- numbers exist.
1. **`usePolled` cannot be reused as it is** (it needs a signed-in API client). Added `usePublicPolled` (section 7).
2. **Sinhala and Tamil fonts are not loaded by the app.** Added to section 7, with the offline-font caveat.
3. **Level 1 size.** The draft said 22 % of the screen (about 240 px); the token file and discovery 20.3 say about 160 px at 1080p, to be validated by a room test. Rule 12 now follows the token file. The 22 % was an invented target.
4. **Main board offline.** AC-E09-01 (4) and EC-111 say it keeps scheduled trains and platforms, marked not live. The draft kept the last estimates. Rule 10 now shows scheduled times on the board and keeps "last known" for the platform display only.
5. **The Tailwind theme has no display sizes.** The generator and its test get them.
6. **Grafana:** the criterion "board reads visible in Grafana" had no task. A panel is added with the counter.
7. The EC numbers the draft said it had not looked up are filled in (section 1), and the existing Claude Design display boards are named.

## 14) Built on 16 Oct 2026: what differs from the spec
- **Dark theme, not light (supersedes Q5).** The Claude Design display boards are dark (`#1B1F24` background) and the token file has the dark values, so the displays are dark. The token generator now writes a `.theme-dark` block (the variables and the short Tailwind names) and the display routes apply it; the controller screens stay light.
- **The display is drawn at 1920 by 1080 and scaled** to the window, so the token scale (Level 1 at 160 px) holds as designed.
- **`lastSeen` shows the station in English only.** The API returns the station name as one string. Showing it in three languages would need a `StationRef` there (small change, not made).
- **Main board has no canvas board.** It follows the style of the platform display; the layout is mine, not drawn by the owner.
- **Terminating trains:** a train that ends at the station is listed with its own station as the destination (arrival only). The spec did not say how to show it; a "terminates here" label is a possible later change.
- **Fonts:** Noto Sans Sinhala and Tamil are loaded from Google Fonts in `index.html`. A display with no internet falls back to system fonts.
- **Tests:** 19 integration tests (`StationBoardTests`), 20 logic tests and 24 component tests for the displays, 5 Playwright flows (the first and second only 08:00 to 13:00), a changed generator test.
- **Migration:** `SeedPlannedPlatforms` sets platform 1 on the 8 demo stops.
- **Grafana:** panel "Station board reads (total)", dashboard version 5.

## 15) Amendment 16 Oct 2026: the displays are their own service (owner request)
Reason: the displays were a route of the same app, served by the same server as the controller dashboard, so one host going down took both. Decision (owner): same code, separate service.
- **One codebase, two builds (D-23 holds).** `npm run build` builds the whole app (dashboard and displays) as before. `npm run build:display` builds only the displays from `display.html` and `src/display-main.tsx`: the two display routes and "This display is not set up" for anything else. It has no identity provider, no sign-in screens, no dashboard screens and no Keycloak address; the web workflow fails if the bundle contains `oidc`, `signin` or `keycloak`.
- **Its own container.** `web/ops/Dockerfile.display` builds that bundle and serves it with nginx (one page for every display address, `no-cache` on the page, long cache on the hashed assets, `/healthz`). Docker Compose has a `display` service on **http://localhost:5174** (for example `/display/platform/GPH/1`). The API address is part of the bundle: `--build-arg VITE_API_BASE_URL`.
- **CORS:** Compose now allows the origins `http://localhost:5173` (dashboard) and `http://localhost:5174` (displays).
- **What still depends on what.** A display needs only the API, and only while it runs: the page is static, so it keeps running with the dashboard down and, because it keeps the last data and says so, through an API outage too. It does not need Keycloak. If the display host itself is down, nothing can show; running two copies behind one address is a deployment matter (not built; nothing is hosted outside Compose yet).
- **Criteria:**
  - [x] The display service starts on its own and serves the display pages without the dashboard: checked 16 Oct with the dashboard's dev server stopped; the 5 Playwright flows (`DISPLAY_ONLY=1 npm run e2e -- e2e/display.spec.ts`) passed against port 5174.
  - [x] The display bundle contains no sign-in, identity provider or dashboard code (checked by a step in the web workflow and by hand: 0 matches).
  - [ ] The `web` workflow (including the display image build) is green on GitHub: not run yet.
- **Not done:** serving the displays from the cluster (no deployment of the web apps exists), a service worker so a display survives a restart with no network, and bundling the Sinhala and Tamil fonts so a display with no internet shows them.

## 16) Amendment 16 Oct 2026: upcoming trains on the platform display, and two more demo trains (owner request)
Reason: a platform display that shows only the next train tells a passenger nothing about the one after it. Owner decisions: the next train stays the big one and the following trains are listed below it, the list rotates upwards, and the demo gets two more trains. This changes FR-81 ("one train") as the spec had read it and the edge-case row "two trains due on one platform": the earlier train is still Level 1, but the later ones are now shown too, smaller.
- **Platform display.** Level 1 to 4 for the first train are unchanged (D-50: the answer to "when is my train arriving?" stays the largest element). Below them a panel "Next trains" (three languages) lists the trains after it on the same platform, up to three rows at a time: expected time, destination in three languages, and the status as icon and words. The platform display now asks `GET /api/v1/stations/{code}/board?platform=P&count=6` (the first train and up to five more).
- **Rotation upwards.** With more than three upcoming trains the panel shows three at a time and every 8 seconds the next three rise into place (the rows slide up and fade in, 0.6 s). When the first train leaves, the list rises one place and the next train becomes Level 1. Nothing else moves. With reduced-motion set in the browser the change happens with no animation. The main board is unchanged.
- **Rule 12 (legibility) is relaxed in one way:** the old text "no animation except a replace on change" now allows this slide-up on change. No content cycles on its own except the upcoming-trains pages.
- **Offline:** the upcoming rows stay, greyed, with the banner as before.
- **Seed (two services, owner request).**
  1. **Night train 8075** (reference data, a migration): Veyangoda 22:10, Gampaha 22:20/22:21, Ragama 22:32/22:33, Maradana 22:40/22:41, Colombo Fort 22:45; Slow, Inbound, runs Daily, platform 1 at every stop. It is on every board in the daytime as an upcoming train.
  2. **Demo train 8070, "leaves soon"** (Compose only, `Seeding:DemoData`): at every API start the demo seeder creates or updates service 8070 so that it leaves Veyangoda **4 minutes after the start** (Gampaha 14 and 15, Ragama 26 and 27, Maradana 34 and 35, Colombo Fort 39), platform 1, Daily. It has no journey, so the board shows it from the timetable (`Scheduled`, `live` false) until its time passes. It is therefore true that it leaves within 5 minutes only shortly after a restart; restarting the API refreshes it. It is not reference data and is never created in the cluster or in prod.
- **Effects on existing data:** train search and the boards now also return 8075 (daily) and 8070 (Compose only). The reference seed has three services and 13 stops.
- **Criteria:**
  - [x] The platform display lists the trains after the first one below it, three at a time, with time, destination in three languages and status as icon and words; the first train is still the largest element and the only one at Level 1 size. (Component tests and a screenshot of the live screen, 16 Oct.)
  - [x] With more than three upcoming trains the list pages every 8 seconds with a slide-up; with `prefers-reduced-motion` it changes with no animation. (Paging tested with fake timers. The reduced-motion rule is in the stylesheet; it was not tried in a browser with that setting.)
  - [x] When the first train departs, the next one becomes Level 1 and the list rises one place. (Component test with a changing answer.)
  - [x] Starting the demo stack adds train 8070 leaving Veyangoda 4 minutes after the start; starting again updates it and creates no second one; without `Seeding:DemoData` it is not created. (5 integration tests; live: the API log said "leaves Veyangoda at 10:30" about 4 minutes after the restart. The gate is the existing `if (seedDemo)` in the start-up service; the ordinary seed used by the tests does not create it.) Known limit: within 4 minutes before midnight the train falls on the next day and is not shown today.
  - [x] Train 8075 (night) is in the reference data and appears as an upcoming train on the platform display in the daytime. (Migration `SeedNightTrain`; integration and Playwright tests.)
  - [x] Existing tests are updated for the two extra services, and all pass. (13 tests changed for the daily night train; 223 unit and 276 integration tests pass.)

