# Feature Spec: Passenger finds trains between stations

branch: claude/feature/passenger-find-trains
spec: _specs/passenger-find-trains.md
owner: tharindu
date: 2026-10-14
status: in review (revised 2026-10-14 after the owner's review; waiting for the owner to read it and approve. On approval set: ready, approved YYYY-MM-DD)

---

## 0) Summary
**One-liner:** Three public read endpoints that let the passenger app's Home and Trains screens work with no account: the stations in three languages, trains between a From and a To station for a chosen date, and a real status for the line banner.
**Why now:** The discovery document lists "Find trains between stations" as a Must (story, section 19.1 "Search: From remembered on device, no location permission / To"), and slice 1 only answers for a train number the passenger does not know. The Home screen banner "Main Line running normally · Updated 08:42" is today a hard-coded placeholder.
**Who is this for:** Passenger (no account, FR-05). Station displays reuse the same data later.
**Success looks like:**
- `GET /api/v1/stations` returns the five Main Line stations in line order with English, Sinhala and Tamil names and a version, and answers 304 when the client already has that version.
- `GET /api/v1/trains?from=VYG&to=CF` on the demo stack returns train 8058 with its scheduled and estimated times at both stations, its delay and status, and it follows a report made through slice 1.
- A date up to 7 days ahead returns the trains scheduled that day from the timetable, marked as not live.
- `GET /api/v1/lines/MAIN/status` says Normal, or "some trains delayed" when a journey in progress is delayed.
- All acceptance criteria pass as automated tests, and `smartrail.trains.searches` is visible in Grafana.

## 1) Traceability
The requirement numbers below come from the owner's review of the requirements file **v1.13**. That file is not in the repo yet (the owner will add it under `docs/`), so Claude could not check them against their source; the wording of each is the owner's.
- Requirements: **FR-65** (find trains between two stations; the origin is remembered on the device: this feature), **FR-14 and FR-69** (station names in three languages; the passenger chooses the language), **FR-05** (passengers need no account), **NFR-04** (30 second update target; the data returned here is current to the last report), **NFR-13** (every endpoint is rate-limited, 429 with `Retry-After`): **a Must that this spec deliberately defers (D2). Nothing in this feature, or in any endpoint built so far, is rate-limited.**
- Discovery document: section 19.1 (home screen, search), 19.2 (live status; only the delay and status parts), 11.3 (three languages), 6.5 (confidence and staleness: not built here), 14 and MoSCoW ("Find trains between stations": Must), product rules in `CLAUDE.md` (status never colour alone, old data is labelled with its time).
- Decisions and assumptions: A-TZ (Asia/Colombo), A-07 (configurable thresholds), ADR-06 (the server is the authority on time), ADR-09 (version in every snapshot), D-71 and D-72 (no ordering, no caterer events), and the rules of slice 1 reused here: R5 and R6 (delay and status), R15 (in progress and the grace period), R16 (stop times across midnight).
- Design: passenger app Home and Trains screens (From, To, "Find trains", the station list).

## 2) Goals and Non-Goals
### Goals
- Expose the stations with names in three languages and a version, so the phone can cache the list and work offline.
- Find trains between two stations for a date, from live journeys where they exist and from the timetable where they do not.
- Give each result the live estimates, delay and status the passenger needs, using the rules of slice 1.
- Replace the hard-coded line banner with a status computed from the journeys in progress.
- Keep every endpoint public, read-only and cheap.
### Non-Goals (this spec)
- Any screen. Remembering From on the phone, the offline cache, language selection and the date picker control are client work.
- Buy ticket, Food menu (D-71), Alerts and push, "Notify me", the followed-train card, "Trains at my station", settings.
- Overdue, confidence and staleness (section 6.5), and the statuses Unknown and Disrupted: results carry only Normal and Warning, as in slice 1.
- Journey planning beyond direct trains: no connections, no changes, no routes across two services.
- Platforms in results (the seed plans none), distances, fares.
- Public holidays and special timetables (deferred decision D1).
- **Rate limiting of every endpoint (NFR-13, a Must): deliberately deferred (deferred decision D2).** No endpoint is rate-limited yet, including these three, and they return no 429 or `Retry-After`.
- Creating journeys for future dates: journey generation is a separate deferred feature. This spec answers future dates from the timetable.
- The cluster: the new routes exist only where a database is configured, as in slice 1 (R14 of that spec).

## 3) User Workflow
### Primary workflow
1. The app opens and calls `GET /api/v1/stations` (sending the version it has cached; the answer is 304 when nothing changed). The station list works offline from the cache.
2. The passenger chooses From (remembered on the phone) and To, and optionally a date within 7 days.
3. The app calls `GET /api/v1/trains?from=VYG&to=CF` (with `date=` when chosen). It shows each train with its times, delay and status.
4. For a live train the app can open the full status by `journeyId` (`GET /api/v1/journeys/{id}/status`, slice 1).
5. The Home banner calls `GET /api/v1/lines/MAIN/status`. It shows "Main Line running normally", "some trains are delayed", or, when `journeysInProgress` is 0, "No trains running now", in the user's language. Its "Updated" age comes from `lastEventAt` (how fresh the data is), not from `serverTime`.
### Secondary workflows
- A search with no match shows "no trains" from an empty list, not an error.
- A date with no journey yet shows the scheduled trains, marked as scheduled only (not live).
- A search for an overnight train still running shows it, with its operating date.

## 4) API Surface
All three are public (no token) and read-only. Errors are RFC 9457 ProblemDetails with a short title and a stable `reason` code. They are added to the public allow-list in `AuthConfigurationTests`, and exist only when `ConnectionStrings:Smartrail` is set.

### GET /api/v1/stations
- 200: `{ version, stations: [{ code, name: { en, si, ta }, lines: [{ code, sequence }] }] }`, in **line order, which starts at Colombo Fort** (the line origin, sequence 1: Colombo Fort, Maradana, Ragama, Gampaha, Veyangoda). The server orders the list; a client may re-sort it for display (see R13). `en` is the station's own name; Sinhala and Tamil come from the translation tables and, if one is ever missing, fall back to English (the seed has all five stations in all three languages, FR-14).
- `version` is a hash of everything above. The same value is sent as the `ETag` header (R1). A request whose `If-None-Match` matches it gets 304 with no body; the match follows R12 (several values, weak `W/` values and `*` are understood).
- Header `Cache-Control: no-cache` (the client must revalidate; revalidation is cheap).

### GET /api/v1/trains?from={code}&to={code}&date={yyyy-MM-dd}
- Parameters: `from` and `to` are station codes (case-insensitive, for example `VYG`, `CF`); `date` is an operating date in Asia/Colombo and is optional (default today).
- 200: `{ from: StationRef, to: StationRef, date, serverTime, trains: [Train] }`, where `StationRef = { code, name: { en, si, ta } }`.
- `Train`: `{ trainNumber, serviceType: "Slow"|"Express", operatingDate, destination: StationRef, live, journeyId, departure: { scheduled, estimated }, arrival: { scheduled, estimated }, delayMinutes, statusGroup, statusKey, version, lastEventAt }`.
  - Times are UTC instants. For a train that crosses midnight they are on the right calendar day (R16 of slice 1).
  - `live: true` for a journey that exists; `journeyId` is its ID and `estimated` times, `delayMinutes` (0 when nothing was reported yet), `statusGroup` (`Normal` or `Warning`), `statusKey` (`Scheduled`, `OnTime` or `Delayed`) `version` and `lastEventAt` (the time the server received the journey's latest report, or null when nothing has been reported yet) come from it.
  - `live: false` for a timetable-only train: `journeyId`, both `estimated` times, `delayMinutes`, `version` and `lastEventAt` are null; `statusKey` is `Scheduled` and `statusGroup` is `Normal`.
  - `destination` is the service's final stop.
- Sorted by the estimated departure from From (the scheduled one when there is no estimate), then by train number. At most `Search:MaxResults` (50) trains are returned.
- 200 with `trains: []` when nothing matches (also for the opposite direction, because the seeded trains run one way).
- 404 `station_not_found` when `from` or `to` is not a station code. 422: `from` or `to` missing (`invalid_request`), `from` equals `to` (`same_station`), `date` is not a valid `yyyy-MM-dd` (`invalid_date`), before today or after today plus `Search:MaxDaysAhead` (7) (`date_out_of_range`).
- Header `Cache-Control: no-store`.

### GET /api/v1/lines/{code}/status
- 200: `{ lineCode, lineName: { en, si, ta }, statusGroup, statusKey, journeysInProgress, delayedJourneys, lastEventAt, serverTime }`.
- `statusKey` is `Normal` when no journey in progress is delayed, otherwise `SomeDelayed` (group `Warning`). No English text is returned; the app words it in the chosen language. `serverTime` is when the answer was computed; **`lastEventAt` is how fresh the data is**: the latest time the server received a report from any journey in progress, or null when none is in progress or none has reported. The banner must show age from `lastEventAt`, never present `serverTime` as "Updated" (a line whose last report was hours ago would otherwise read "Updated just now"). **Zero trains:** `journeysInProgress: 0` keeps `statusKey: Normal`, but the client must word that case differently ("No trains running now"), not "running normally".
- 404 `line_not_found` for an unknown line code.
- Header `Cache-Control: no-store`. The existing `GET /api/v1/status` is left as it is.

## 5) Data Contract
No schema change, no migration. All data already exists: `station`, `station_translation`, `line`, `line_translation`, `line_station`, `train_service`, `service_stop`, `journey`, `journey_stop_state` (see [smartrail-schema.sql](smartrail-schema.sql)). The API role already reads them.
- `train_service.runs_on` is free text; this spec gives it a meaning (R5). Values other than Daily, Weekdays and Weekends are unsupported (R6).
- Station and line identity in the API is the `code` column (unique).
- Seed data: unchanged. The demo journey for 8058 and the reference data are enough; tests create the rest.

## 6) Business Rules
1. R1 (offline cache): `version` is a hash of the station codes, names in three languages and line sequences, in a stable order, so it changes exactly when the list changes. Equal content gives an equal version across restarts. The `ETag` is that version in quotes.
2. R2 (names, FR-14): the English name is the entity's own `name`; Sinhala and Tamil come from the translation tables; a missing translation falls back to English as a safety net only. The seed must hold Sinhala and Tamil for every station (all five today), and a test fails if a seeded station lacks either.
3. R3 (search validity): `from` and `to` must be different stations and both must exist. A service qualifies for a search only when both stops belong to it and From comes before To in its stop sequence (so the express 8061, which skips Ragama and Maradana, does not appear for a search that involves them, and a train never appears in the wrong direction).
4. R4 (which trains are returned): for the operating date D (default today, `OperatingTime.Today`), per qualifying service:
   1. **Live, D is today.** Journeys with an operating date from two days ago to today are candidates (a journey takes at most 24 hours, as in R15 of slice 1). A journey is returned when it is not `Completed`, is not over (its expected end plus `Journeys:VisibleAfterEndMinutes` has not passed), and has **not yet departed From**: the stop state of From is not Reported, and no later stop is Reported. A journey with status `Unassigned` is included. Slice 1's stop states and R16 give the times.
   2. **Live, D is a later date.** A journey row for D is returned as live (no "over" or "departed" test is needed).
   3. **Timetable-only.** A service that runs on D's weekday (R5) and has no journey row for D is returned with `live: false` and scheduled times only. For today it is returned only when its scheduled departure from From is not in the past.
   A service with a journey row for D is never also returned from the timetable.
5. R5 (calendar): `ServiceCalendar` decides whether a service runs on a date from `runs_on`, case-insensitive and trimmed: `Daily` every day; `Weekdays` Monday to Friday; `Weekends` Saturday and Sunday, using the weekday of the operating date. Holidays and special timetables are not modelled (deferred decision D1), and a timetable-only result is always marked `live: false` so a client can say so.
6. R6 (unsupported calendar): a service whose `runs_on` is none of those values never appears from the timetable and a warning is logged (service train number only); its journey rows still appear, because a journey is a fact.
7. R7 (limits): `date` defaults to today; a date before today or after today plus `Search:MaxDaysAhead` (default 7) is refused with 422. At most `Search:MaxResults` (default 50) trains are returned, after sorting.
8. R8 (times and status): estimates, delay and status of a live result are those of slice 1 (R5 and R6 there), using the stored stop states, with the scheduled time plus the journey's delay when a stop has no stored estimate. Arrival is at To; departure is from From. Results carry no more status groups than slice 1 can produce. `lastEventAt` of a live result is the latest `received_at` among that journey's events (null if none).
9. R9 (line status): journeys in progress are the journeys of that line, with operating dates from two days ago to today, that are not Completed, have started (first scheduled departure not later than now) and are not over (R15 definition, including the grace period). `delayedJourneys` counts those whose status group is Warning (delay above `Journeys:OnTimeToleranceMinutes`). `statusKey` is `Normal` when that count is 0, else `SomeDelayed`. A line with no journey in progress is `Normal` with `journeysInProgress: 0`. `lastEventAt` is the latest `received_at` among the `train_event` rows of those journeys (null if none).
10. R10 (public and cheap): no token is needed and a bad token on these routes never blocks them. Queries read a handful of rows; there is no per-user data.
11. R11 (no database): the routes are mapped only when a database is configured (as slice 1's R14).

## 7) Architecture (follow CLAUDE.md and ADR-15)
Feature folders, no cross-feature references:
- `Domain`: `ServiceCalendar` (R5, R6) and the "has the journey departed this stop" test (`StopPassed`) as pure functions, unit-tested. `ScheduleInstants`, `JourneySelection` and `JourneyProgress` from slice 1 are reused.
12. R12 (`If-None-Match`, R1): the header may hold several entity tags, weak ones (`W/"abc"`) and `*`, and proxies add them. The answer is 304 when `*` is present or when any listed tag equals the current ETag after removing `W/` (weak comparison); otherwise 200 with the full body and the ETag. A malformed header is ignored (200).
13. R13 (station order, FR-65): the API orders stations by line sequence, so the list **starts at Colombo Fort**, the line origin. This is the owner's decision of 2026-10-14. The hi-fi From list shows Veyangoda first, which is a design the app or the design must follow or reorder on the client; the API does not reorder it.
14. R14 (one grace period): "over" in R4 and R9 is the same rule as R15 of slice 1 and reads the same setting, `Journeys:VisibleAfterEndMinutes` (default 15, defined once in `JourneySettings`). Slice 1 and this feature can never disagree: changing the value changes both. Both specs say 15; the 60 minute value was replaced on 2026-10-14 and no copy of it remains.
- `Application/Features/Stations/GetStations`: builds the response and the version hash (R1). `Features/Trains/FindTrains`: the rules R3, R4, R7, R8, sorting and the cap. `Features/Lines/GetLineStatus`: R9. All depend on an `IPassengerQueries` interface (stations with translations, lines, services with their stops and calendar text, journeys with stop states for a date range).
- `Infrastructure`: an EF Core implementation of `IPassengerQueries`, read-only (`AsNoTracking`).
- `Api`: `Features/Stations`, `Features/Trains`, `Features/Lines` each with one `Map` extension, `.AllowAnonymous()`, registered with the slice-1 journey features behind the database switch. The ETag and 304 are handled in the stations endpoint.
- Configuration: `Search:MaxDaysAhead` (7), `Search:MaxResults` (50); reuses `Journeys:OnTimeToleranceMinutes` (2) and the one shared `Journeys:VisibleAfterEndMinutes` (15, defined once in `JourneySettings` and used by slice 1 and this feature, R14).
- Telemetry: counter `smartrail.trains.searches` (tags `outcome` = found, empty or invalid; `source` = live, timetable or mixed; `deployment.environment` from the resource), a span per search, and a log field `from` and `to` as station codes only (validated against the station list before logging, never raw input).
- Tests: the allow-list test grows by the three routes; the "no database" test asserts they are absent.
- Clients that will depend on this: the Flutter passenger app, station displays.

## 8) Edge Cases handled in this spec
| Case | Handling |
|------|----------|
| No train matches | 200 with an empty list |
| The opposite direction (for example Gampaha to Veyangoda; the seed has Inbound trains only) | 200 with an empty list (R3). **The app cannot tell this from "no trains today"**, so the empty-state text must be neutral ("No trains found for this search"), not "no more trains today". Telling the cases apart is deferred (D8) |
| Unknown station code | 404 `station_not_found` |
| `from` equals `to` | 422 `same_station` |
| Missing or invalid `date`, past date, more than 7 days ahead | 422 |
| Express 8061 and a search through Ragama or Maradana | 8061 is not returned (R3); VYG to GPH or VYG to CF returns both 8058 and 8061 |
| A train that already left From | Not returned (R4.1) |
| A skipped From stop (a later stop was reported) | Counts as departed; not returned |
| A finished or Completed journey | Not returned |
| An Unassigned journey | Returned (the timetable still runs) |
| An overnight train still running after midnight | Returned for today's search, with its own operating date |
| Overnight train in a timetable-only answer | Stop times are placed on the right calendar days (R16) |
| A service with no journey row for D, today | From the timetable, only while its scheduled departure from From is not in the past |
| A service with unsupported `runs_on` | Not in timetable-only results; warning logged (R6) |
| Station has no Sinhala or Tamil name | English fallback (R2), a safety net only: the seed has both for all five stations and a test guards it |
| Client sends the current version, several versions, a weak `W/"…"` version, or `*` | 304 with no body when any of them matches (R12) |
| Line with no journey in progress (for example 2 a.m.) | `journeysInProgress: 0`, `statusKey: Normal`, `lastEventAt` null; the client says "No trains running now", not "running normally" |
| Line whose last report was hours ago | `lastEventAt` shows it, so the banner can show the age; `serverTime` is not "updated" |
| Many results | Capped at `Search:MaxResults`, after sorting |
| No database configured (cluster today) | The routes do not exist (R11) |
| A search at 23:59 across midnight | `date` default is evaluated by the server clock when the request arrives |

## 9) Acceptance Criteria
- [ ] `GET /api/v1/stations` returns the five stations **starting at Colombo Fort**, in line order (Colombo Fort, Maradana, Ragama, Gampaha, Veyangoda), each with English, Sinhala and Tamil names, line sequences and a version (FR-14, FR-69, FR-65).
- [ ] The same request with `If-None-Match` equal to that version returns 304 with no body; so do a list of several tags that contains it, a weak `W/"…"` form of it, and `*`; a list that does not contain it returns 200. The version is the same after a restart and changes when a station name changes.
- [ ] Every seeded station has a Sinhala and a Tamil name (a test fails if one is missing); a station with a missing translation would return its English name.
- [ ] `GET /api/v1/trains?from=VYG&to=CF` on the seed (clock 08:10 on the seeded Tuesday) returns 8058 and 8061 with the scheduled times at both stations, destination Colombo Fort, `live: true`, and the estimated times equal to the scheduled ones.
- [ ] After slice 1 records Veyangoda departing 08:28 (8 minutes late), a search from Gampaha to Colombo Fort shows 8058 with departure estimated 08:39 and arrival estimated 09:03, delay 8, `Warning`, `Delayed`.
- [ ] After the Veyangoda report, a search from Veyangoda to Colombo Fort no longer returns 8058 (it left From); a search from Gampaha still does until Gampaha is reported.
- [ ] A search from Ragama to Colombo Fort does not return 8061 (it does not stop at Ragama).
- [ ] A search from Gampaha to Veyangoda returns 200 with an empty list.
- [ ] Unknown station code: 404. `from` equals `to`: 422. Missing parameter: 422. Invalid, past and 8-days-ahead dates: 422; a date 7 days ahead is accepted.
- [ ] A `Completed` journey, a journey whose expected end plus 15 minutes has passed, and a journey that left From are not returned; an `Unassigned` journey is returned.
- [ ] For the next Monday (no journey row), the search returns 8058 and 8061 from the timetable with `live: false`, null `journeyId`, null estimates, `Scheduled` and `Normal`. For the next Saturday it returns an empty list (the seed runs on weekdays).
- [ ] Today, a service with no journey row is returned from the timetable only while its scheduled departure from From is not in the past.
- [ ] A service with `runs_on` text that is not supported never appears from the timetable, and a warning is logged.
- [ ] A timetable-only overnight service (23:50, 00:20, 08:00) returns its stop times on the right calendar days.
- [ ] A night train still running at 00:10 on the next date is returned for today's search with its own operating date.
- [ ] Results are sorted by the estimated departure from From, then train number, and are capped at `Search:MaxResults`.
- [ ] `GET /api/v1/lines/MAIN/status` is `Normal` with `journeysInProgress: 0` and `lastEventAt: null` when none is running (the client words that case "No trains running now"), `Normal` when journeys run on time, and `SomeDelayed` with `delayedJourneys: 1` when one is delayed (delay above 2 minutes). An unknown line is 404.
- [ ] `lastEventAt` of the line is the latest report time among the journeys in progress and does not move when nothing new is reported, while `serverTime` does. A journey with no report gives null. The same field on a live train result is that journey's latest report time; on a timetable-only train it is null.
- [ ] All three routes work with no token and with a bad token, are on the allow-list (the allow-list test sees exactly these new routes), and do not exist when no database is configured.
- [ ] `smartrail.trains.searches` is visible in Grafana with the outcomes found and empty.
- [ ] Existing tests still pass (62 unit, 119 integration at the time of writing), including `GET /api/v1/status`.
- [ ] The slice-1 train lookup and this feature read the same `Journeys:VisibleAfterEndMinutes`: a test changes it once and both change (R14).
- [ ] Nothing here returns 429 or `Retry-After`: NFR-13 is deliberately deferred (D2), and this is stated in the report and the Google Sheet.
- [ ] CI is green: build with `-warnaserror`, vulnerable-package check, and all tests.

## 10) Test Plan
### 10.1 Seed and fixture data
The slice-1 test base (one fresh database per test, a fake clock set per test, real tokens only where a route needs them; these routes need none). Train 8058 and 8061 from the reference seed; stations VYG, GPH, RGM, MDA, CF. Tests add: a journey for 8061, journeys with other statuses and dates, extra services with calendars (Daily, Weekends, an unsupported text) and the night service of slice 1's tests. The clock is set to the time each test needs.
### 10.2 Unit tests (Domain and Application, no database)
`ServiceCalendar` (every weekday against Daily, Weekdays, Weekends, spacing and case, unsupported text); `StopPassed`; the version hash (stable, order-independent where it should be, changes with any name); the `If-None-Match` matcher (one tag, several, weak `W/`, `*`, malformed, no header); the seed guard (every station has si and ta); `lastEventAt` as the latest of several journeys; sorting and the cap; the date range rules at their edges (today, today plus 7, today plus 8, yesterday).
### 10.3 Integration tests (WebApplicationFactory plus Postgres in Testcontainers)
The acceptance criteria above, plus: 304 handling (single, several, weak and `*` values), the freshness field staying put while `serverTime` moves, every error code, the allow-list and no-database tests, a bad token on a public route, the metric outcomes, and a regression check that `GET /api/v1/status` is unchanged.

## 11) References
- `operator-report-live-status.md` (R5, R6, R15, R16 and the journey data), `database-foundation.md` (tables, seed), `keycloak-identity.md` (allow-list), [smartrail-schema.sql](smartrail-schema.sql), discovery sections 19.1, 19.2, 11.3 and 6.5, CLAUDE.md.
- Existing code reused: `ScheduleInstants`, `JourneySelection`, `JourneyProgress`, `SnapshotBuilder`, `OperatingTime`, the slice-1 test base.
- To be added by the owner under `docs/`: the requirements, acceptance-criteria and edge-case documents; their numbers are not used here.

## 12) Open Questions and Decisions
### Open questions
None. (Required empty before approval.)
### Decisions made on 2026-10-14
| # | Decision | Recorded in |
|---|----------|-------------|
| Idea | Stations, find trains A to B, and a real line status; API only, public | section 0 |
| Result window | Trains still to reach From, plus any now at From | R4 |
| Languages | All three names in every station object; the client chooses | section 4, R2 |
| Line status rule | Normal unless a journey in progress is Delayed | R9 |
| Empty and invalid | 200 with an empty list; 404 unknown station; 422 same station | section 4 |
| Station version | A content hash, also the ETag | R1 |
| Result fields | Journey, train, type, destination, times at From and To, delay, status | section 4 |
| Qualifying journeys | Not Completed, not over, not yet departed From; Unassigned included | R4 |
| Rate limits (NFR-13) | Deferred on purpose: NFR-13 is a Must, and nothing is rate-limited yet | D2 |
| Line status route | New `GET /api/v1/lines/{code}/status`, no English text; `/api/v1/status` unchanged | section 4 |
| No database | The new routes exist only with a database | R11 |
| Select a date | A `date` parameter, default today, today to 7 days ahead (the owner's request) | section 4, R7 |
| Future dates | Answered from the timetable when no journey exists, marked `live: false` | R4 |
| Today with no journey row | From the timetable while the scheduled departure from From is not in the past | R4 |
| Unsupported `runs_on` | Never shown from the timetable; warning logged | R6 |
| Holidays | Deferred; weekday rule only; timetable-only results are marked | D1 |

### Decisions made on 2026-10-14, from the owner's review
| # | Decision | Recorded in |
|---|----------|-------------|
| Station order | The API lists stations in line order, **starting at Colombo Fort** (owner: "must start from Colombo Fort"); the hi-fi From list starts at Veyangoda and must follow or reorder | R13 |
| One grace period | `Journeys:VisibleAfterEndMinutes` (15) is defined once and shared with slice 1; no 60 minute copy remains | R14 |
| Honest freshness | `lastEventAt` on the line status and on each live train result; `serverTime` is not "updated" | section 4, R8, R9 |
| Zero trains | `statusKey` stays `Normal`; the client words it "No trains running now" | section 4 |
| NFR-13 | A Must, deliberately deferred; nothing is rate-limited yet; said in the spec, the report and the Sheet | section 1, D2 |
| Real numbers | FR-65, FR-14, FR-69, FR-05, NFR-04, NFR-13 from the owner's v1.13 review (file not in the repo) | section 1 |
| `If-None-Match` | Several, weak and `*` values are understood; one test | R12 |
| Translations | The seed has Sinhala and Tamil for all five stations; a test guards it; English is only a safety net | R2 |
### Deferred decisions
| # | Decision | Fallback chosen now | Revisit when |
|---|----------|---------------------|--------------|
| D1 | Public holidays and special timetables | `runs_on` weekday rule only; timetable-only results are `live: false` so the app can say they are scheduled, not confirmed | A calendar exists (journey generation or assignment management) |
| D2 | **NFR-13 (a Must): every endpoint rate-limited, 429 with `Retry-After`.** Deliberately deferred, not forgotten | No rate limiting anywhere today, in this feature or in any earlier one; acceptable only while the app runs on the owner's machine | Once, for every route, before any deployment outside the owner's machine (and before the demo if it is shown outside it). The owner's report and the Google Sheet must say that nothing is rate-limited yet |
| D3 | Live push of search results (SignalR) | Clients poll | Slice 2 |
| D4 | Overdue, confidence, Unknown and Disrupted statuses in results and in the banner | Only Normal and Warning | The overdue and overrides slices |
| D5 | Planned platform in results | Not returned | When planned platforms are seeded |
| D6 | Connections and journeys that need a change | Direct trains only | Not planned |
| D7 | Search without a database in the cluster | Routes absent | The `cluster-databases` spec |
| D8 | Telling "no train in this direction" apart from "no trains today" in an empty result | Both are an empty list; the empty-state copy must be neutral | When the app's empty states are designed, or when outbound trains are seeded |
| D9 | `lastEventAt` on the slice-1 journey status snapshot | Not added there; this feature adds it to line status and to train results only | The staleness slice (section 6.5), which needs it everywhere |
### Proposed additions to confirm on review
- Route and parameter names: `GET /api/v1/trains?from=&to=&date=` (a query on the trains collection; it does not clash with `/api/v1/trains/{trainNumber}/status`) and `GET /api/v1/lines/{code}/status`.
- Station and line identity in the API is the `code` column; codes are matched case-insensitively. The station object carries `lines` (code and sequence) so a second line does not change the shape.
- The cap of 50 results and the header choices: `Cache-Control: no-cache` plus an ETag for stations, `no-store` for the other two.
- `delayMinutes` is 0 for a live journey with no report, and null for a timetable-only train.
- Station codes in logs are used only after they have been matched to a station (never the raw query value), because CodeQL flags logging of request input.
- The version hash is a SHA-256 of a canonical text, shortened to 16 hex characters.
- Decision owner: tharindu. Approval needed before the build starts. The build starts after slice 1 (`operator-report-live-status`) has been merged into `dev`, on a branch from `dev`.
