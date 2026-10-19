# Feature Spec: Scheduling (timetable, weekly services, extra trains, cancel, amend, journey generation)

branch: claude/feature/scheduling
spec: _specs/13-scheduling.md
owner: tharindu
date: 2026-10-19
status: approved 2026-10-19 (owner: Q2 to Q10 as recommended; scope S1 + S2 backend only; branch from operator-reports-history)

---

## 0) Summary
**One-liner:** A controller manages the timetable in the operations web app: sees the weekly schedule, extra trains and cancelled services, creates a weekly service (days, stops, times, platforms) or an extra train for chosen dates, cancels a service for one date or from a date onward, and amends a service as a new version. The server validates every change (FR-11D), and a journey generator turns services into journeys at least 7 days ahead (FR-11).
**Why now:** FR-11, FR-11A, FR-11B, FR-11C and FR-11D are **Musts** and nothing for them exists. Services are seed data, `runs_on` is free text, there is no journey generator (only the demo seeder creates journeys), no service versions, no validation and no cancel. The controller dashboard boards `Dash-Timetable`, `Dash-ServiceDetail` and `Dash-NewService-Stops` are drawn and unbuilt.
**Who is this for:** Controller (Jayasinghe). Operators and passengers feel the result (journeys appear, cancelled dates disappear). No new role (D-69).
**Success looks like:**
- Jayasinghe creates weekly service 8072 (Veyangoda to Colombo Fort, express stops, times, platforms) on Mon to Fri. Journeys for the next 7 days exist within a minute, each Unassigned with a stop state per stop (AC-E02-01 1, 4). The passenger app finds 8072 for tomorrow.
- A service with stops out of line order, a time going backwards or a platform of another station is refused with the exact reason (FR-11D). A platform used by another train within 10 minutes is a warning he can override.
- He cancels 8072 for Friday 23 October with a reason. No journey runs that day, the history stays, and the timetable shows it under Cancelled.
- He amends 8052 so Gampaha leaves at 07:42 from Monday: version 2 is current, version 1 is kept, and journeys before Monday keep their old times.
- A controller token is required for every route; operator and passenger tokens get 403.

Size: **large, about 3 days** (backend 1.5, web 1.5). This does not fit in the time left before 20 October together with the other open work, so it is cut into four slices (section 12, Q1): **S1** read the timetable and generate journeys (0.75 day), **S2** create weekly and extra services with validation (1.25), **S3** cancel (0.5), **S4** amend with versions (0.75). S1 and S2 are the minimum that makes FR-11 and FR-11A true. Whatever is not built must be stated as not built in the report.

## 1) Traceability
- Requirements (wording read in `docs/smartrail-refined-requirements.md` v1.13): FR-11 (one journey per service per operating day, at least 7 days ahead, controllable clock), FR-11A (weekly schedule and extra services, edit before pressing Schedule, Every stop option), FR-11B (amend creates a version; passengers see the latest; **affected passengers notified: not built**; caterer: deferred D-72), FR-11C (cancel one date or from a date onward with a reason; never deleted; **notifies passengers and starts refunds: not built**), FR-11D (validation: not in the past, stops in line order, times increasing, platform of its own station, platform warning as advice only), FR-10 (service model), FR-14 (station names in three languages, shown in the UI), FR-57 (the warning window, configurable, 10 minutes proposed), FR-12 (assignment: **not built here**, Q4), NFR-10 (role checks), NFR-17 (audit), NFR-41 (thresholds configurable), NFR-30, NFR-33.
- Acceptance criteria: AC-E02-01 (1 to 4 for the generator; 5 depends on assignment, Q4). **FR-11A to FR-11D have no acceptance criteria in the baseline**; section 9 proposes them (Q8).
- Edge cases: none exist for scheduling in `docs/smartrail-edge-cases.md` (EC-146 mentions a cancellation reaching the caterer, which is deferred). Section 8 proposes new ones.
- Decisions: D-69 (scheduling is a controller function, no new role), D-70 (published schedules are versioned, never deleted), D-65 (refunds on railway cancellation: ticketing not built), D-72 (nothing is sent to the caterer), A-07i (horizon 7 days), A-08 (planned platforms), D-76.
- Design: `Dash-Timetable` (drawn), `Dash-ServiceDetail` (drawn), `Dash-NewService-Stops` (step 2, drawn). **Not drawn:** step 1 Details, step 3 Review and schedule, the extra-train form, the cancel dialog, the amend flow, and the Extra trains and Cancelled tab contents (G1 to G7, Q7).

## 2) Goals and Non-Goals
### Goals
- The Timetable section of the controller web app: list with the three tabs (Weekly schedule, Extra trains, Cancelled), service detail with stops and versions, a three-step new-service wizard (Details, Stops and times, Review and schedule), the extra-train form, the cancel dialog, amend.
- Controller-only API routes under `/api/v1/timetable` (section 4), with server-side validation and a dry-run `validate`.
- A journey generator (hosted service) that creates missing journeys for the next 7 days, idempotently, using the controllable clock (FR-11, AC-E02-01).
- Service versions that keep history: an amendment is a new service version; older journeys keep the version they ran under (D-70).
- An audit entry for every create, amend and cancel (who, when, what changed).
- The passenger reads (`GET /trains`, station board) honour versions, effective dates and cancellations.
### Non-Goals
- **Passenger notification of amendments and cancellations (FR-11B, FR-11C).** Push is not built (FR-75/76, spec 10). Passengers see the new times or the missing train at their next refresh. This is stated, not hidden (Q5).
- **Refunds (FR-11C, FR-117) and sending changes to the caterer (FR-95, D-72).** Not built.
- **Assigning operators to generated journeys (FR-12) and ending assignments (FR-56).** A generated journey is Unassigned and nobody can report on it until an assignment exists. Today only the demo seeder assigns (Q4). This leaves AC-E02-01 5 unmet.
- Editing a journey that has events, a running journey, or a past date. Cancelling a running or completed journey (that is an override, FR-54, not built).
- Holidays and special calendars (decision D1 in spec 04), several lines (FR-107, one line only), creating stations or platforms (reference data), a service's seat layout and unreserved capacity (ticketing, Should), bulk import, copying a service.
- Rate limiting (NFR-13 is deferred everywhere).

## 3) User Workflow
### Primary workflow: new weekly service (S2)
1. Jayasinghe opens **Timetable**, tab Weekly schedule, and presses **New weekly service**.
2. **Details:** train number (8072), category (Regional or Long-distance), type (Slow or Express), days (M to S toggles), first date it runs (default tomorrow).
3. **Stops and times:** **Every stop** fills every station on the line in line order, or **Choose stops** with a checkbox per station (first and last always included); a time and a platform per included stop, as drawn in `Dash-NewService-Stops`.
4. **Review and schedule:** a read-only summary with errors (cannot schedule) and warnings (can schedule). He may go back and edit. **Schedule** saves, generates the journeys and opens the service detail.
### Secondary workflows
- **Extra train (S2):** same wizard with dates instead of days (one or more chosen dates, each today or later). Creates one-off services; they list under Extra trains.
- **Cancel (S3):** from the service detail, **Cancel service**: choose "this date" (a date) or "from this date onward", give a reason, confirm. The service moves to Cancelled for those dates; nothing is deleted.
- **Amend (S4):** **Amend service** opens the wizard filled with the current version, with an "effective from" date (tomorrow at the earliest). Schedule saves version 2 and keeps version 1.
- **Service detail (S1):** stops with times and platforms, version history (who, when, what changed), actions.
- **Errors:** a refused change shows the reason next to the field or step; a failed save keeps the form; a 409 (train number clash, date overlap) tells him which service clashes.

## 4) API Surface
All routes: policy `Controller` (role `controller`). No token 401, other roles 403. Errors are ProblemDetails with a stable `code`.

### 4.1 `GET /api/v1/timetable/options?line=MAIN`
Stations of the line in line order with their platforms, for the forms: `[{ "code":"VYG","sequence":5,"names":{"en":"Veyangoda","si":"වේයන්ගොඩ","ta":"வேயங்கொடை"},"platforms":["1","2"] }]`.

### 4.2 `GET /api/v1/timetable/services?line=MAIN&tab=weekly|extra|cancelled`
- `weekly`: current version of each weekly service (the version in force today or starting later).
- `extra`: one-off services with a date today or later.
- `cancelled`: services with a cancellation that covers a date today or later, with its reason, range and who cancelled.
- Row: `serviceId`, `trainNumber`, `category`, `serviceType`, `fromName`, `toName`, `runsOn` (list of day codes) or `date`, `firstDeparture` (HH:mm), `stopCount`, `stopsLabel` ("Every stop (5)" or "Selected stops (4 of 5)"), `version`, `amended` (version above 1), `effectiveFrom`, `cancelledRanges`.

### 4.3 `GET /api/v1/timetable/services/{serviceId}`
Detail: header fields, `stops[]` (`sequence`, `stationCode`, names, `arrival`, `departure`, `platform`, `isFirst`, `isLast`), `versions[]` (`version`, `serviceId`, `effectiveFrom`, `effectiveTo`, `createdAt`, `createdBy` (display name), `summary` text such as "Gampaha 08:40 (was 08:38)"), `cancellations[]`. 404 `service_not_found`.

### 4.4 `POST /api/v1/timetable/validate`
Same body as 4.5. Always `200`:
```json
{ "valid": false,
  "errors":   [ { "code": "times_not_increasing", "field": "stops[2].arrival", "message": "Ragama 08:35 is before Gampaha 08:40." } ],
  "warnings": [ { "code": "platform_close", "field": "stops[1].platform", "message": "Train 8065 uses Gampaha platform 2 at 17:31 (4 minutes apart)." } ] }
```
Used by the Review step. Saves nothing.

### 4.5 `POST /api/v1/timetable/services` (create)
```json
{
  "kind": "Weekly",
  "trainNumber": "8072",
  "lineCode": "MAIN",
  "category": "Regional",
  "serviceType": "Express",
  "days": ["Mon","Tue","Wed","Thu","Fri"],
  "effectiveFrom": "2026-10-20",
  "stops": [
    { "stationCode": "VYG", "arrival": null,    "departure": "17:25", "platform": "1" },
    { "stationCode": "GPH", "arrival": "17:35", "departure": "17:36", "platform": "2" },
    { "stationCode": "CF",  "arrival": "17:58", "departure": null,    "platform": "3" }
  ],
  "acknowledgeWarnings": true
}
```
- `kind` is `Weekly` or `Extra`. A weekly service has `days` and `effectiveFrom`; an extra one has `dates` (a list, each today or later) and no days.
- Direction is **derived** from the first and last stop (toward the line's first station is Inbound, away is Outbound, FR-10); the controller never types it.
- Stops are sent in order; first and last are always present; times are `HH:mm` Asia/Colombo; `platform` is a platform code of that stop's station or null (Not set, as drawn for the last stop).
- Response `201`: `{ "serviceIds": ["..."], "trainNumber": "8072", "version": 1, "journeysGenerated": 5, "warnings": [] }`. An extra train with several dates creates one service per date and lists them all.
- Errors: `422` with the same `errors` list as 4.4 when any rule in section 6 fails; `409 train_number_in_use` (another active service with that number overlaps these dates, the body names it); `409 warnings_not_acknowledged` when there are warnings and `acknowledgeWarnings` is not true (warnings are advice only and never block, FR-11D).
- Idempotency: the same body twice produces `409 train_number_in_use` the second time; nothing is created twice.

### 4.6 `POST /api/v1/timetable/services/{serviceId}/cancel`
`{ "from": "2026-10-23", "to": "2026-10-23", "reason": "Rolling stock not available" }`; `to` null means "from this date onward". `from` is today or later; reason 1 to 200 characters. Journeys in the range that have **no events** are set to Cancelled; a journey with events (running or finished) makes the call `409 journey_has_events` and nothing changes (an override is the tool for that, FR-54). Response `200`: `{ "cancelledJourneys": 1, "range": {...} }`. Repeating the same cancel is `200` with `cancelledJourneys: 0`.

### 4.7 `POST /api/v1/timetable/services/{serviceId}/amend`
Same body as create (kind and train number cannot change), plus `effectiveFrom` (tomorrow or later) and required for weekly services only. Creates version N+1 as a new service row, ends the old version the day before, and regenerates the not-yet-started journeys from `effectiveFrom`. `409 journey_has_events` if any journey on or after `effectiveFrom` already has events. Response `201` as create.

## 5) Data Contract
**One migration `AddScheduling`** (EF Core, snake_case, default privileges already cover new tables):
- `train_service` gains: `kind varchar(10) not null default 'Weekly'` (check `Weekly`, `Extra`), `version int not null default 1` (check `>= 1`), `effective_from date not null default '2000-01-01'`, `effective_to date null`, `superseded_service_id uuid null` (fk to `train_service`, the previous version). The seed rows keep version 1, effective since 2000-01-01.
- **The unique index `ix_train_service_train_number` is dropped** (a train number now has several versions and extra dates) and replaced by `unique (train_number, version, effective_from)`. Overlap between the effective ranges of two active services with the same number is checked in the application, inside the create transaction with an advisory lock on the train number, because a range exclusion constraint needs the `btree_gist` extension, which the database image may not have (Q6).
- `service_cancellation`: `id uuid`, `service_id uuid fk`, `from_date date`, `to_date date null`, `reason varchar(200)`, `created_by varchar(64)`, `created_at timestamptz`; check `to_date is null or to_date >= from_date`. Append-only by convention (no update route).
- `service_change_log`: `id uuid`, `service_id uuid fk`, `action varchar(12)` (`Created`, `Amended`, `Cancelled`), `actor varchar(64)`, `at timestamptz`, `details jsonb` (stops before and after for an amendment, the reason for a cancel). Written in the same transaction as the change.
- `journey.status` check constraint gains `Cancelled`; `JourneyStatus` enum gains `Cancelled`.
- `runs_on` stays a short text, written normalised: `Daily` (all seven), `Weekdays` (Mon to Fri), `Weekends` (Sat, Sun), otherwise a comma list such as `Mon,Wed,Fri`. `ServiceCalendar.Runs` is extended to read a day list; unknown text still returns null and is never guessed (spec 04 R6). An extra train is a service with `kind = Extra`, `effective_from = effective_to = the date`, `runs_on = Daily`.
- Seed: existing services unchanged. Demo: no new seed data is required; the generator produces journeys from the seeded services.
- Indexes: `(line_id, kind, effective_from)` for the list; `(service_id, from_date)` on cancellations.

## 6) Business Rules
1. **Controller only (D-69, NFR-10).** Every route requires the `Controller` policy. The actor stored is the token subject.
2. **Not in the past (FR-11D).** `effectiveFrom`, every extra date and every cancel `from` must be today or later (Asia/Colombo, the controllable clock). For a service that starts today, the first departure must be later than now.
3. **Stops in line order (FR-11D).** Stops are strictly in line-sequence order, all in one direction, each station on the line, no station twice, at least two stops, first and last present.
4. **Times increase (FR-11D).** For each stop `arrival < departure` when both exist; each arrival is after the previous departure; the first stop has a departure only and the last an arrival only (as the seed). Times may pass midnight (the journey instants come from `ScheduleInstants`), but a service lasts less than 24 hours.
5. **Platform belongs to its station (FR-11D, FR-10).** A platform code must be a platform of that stop's station; the database also enforces it (composite key). A platform may be null.
6. **Platform clash is advice only (FR-11D, FR-57).** For each stop with a platform, any other active service on the same dates with a stop at the same station and platform within `Scheduling:PlatformWarningMinutes` (default 10, configurable) produces a warning. It never blocks; the controller confirms with `acknowledgeWarnings`.
7. **Train number (FR-10).** 1 to 10 characters, digits and letters, unique among active services whose effective ranges overlap. A cancelled service's number can be reused only for dates it does not cover.
8. **Days (FR-11A).** At least one day for a weekly service; normalised into `runs_on` as in section 5. **Every stop** is a form convenience: it fills the line's stations in order; times and platforms still have to be entered.
9. **Generator (FR-11, AC-E02-01, A-07i).** For every service and every operating date from today to today plus `Scheduling:HorizonDays` (default 7) on which the service runs (kind, effective range, `runs_on`, not cancelled), a journey exists: status `Unassigned`, with one stop state per stop (Pending). It is idempotent: the unique index `(service_id, operating_date)` plus `ON CONFLICT DO NOTHING`, so running it twice or concurrently creates no duplicate. It runs at start-up, every `Scheduling:GenerateEveryMinutes` (default 60) and right after a create or amend, always with `TimeProvider`.
10. **Amend (FR-11B, D-70).** An amendment inserts a new `train_service` row (version + 1, same train number, `superseded_service_id` set), copies unchanged stops, ends the previous version the day before `effectiveFrom`, and regenerates not-yet-started journeys from that date under the new version. Journeys before it keep the old version and its times. A journey with any event is never touched (`409 journey_has_events`).
11. **Cancel (FR-11C, D-70).** A cancellation is a row with its range and reason; the service and its versions stay. In-range journeys with no events become `Cancelled`; the generator never creates journeys on cancelled dates. Cancelling is reversible only by creating an extra train (nothing is un-cancelled in this slice).
12. **Never deleted (D-70).** No route deletes a service, a version, a stop or a cancellation. Old versions stay readable in the detail.
13. **Audit (NFR-17).** Every create, amend and cancel writes a `service_change_log` row in the same transaction. A failed change leaves no row.
14. **Passenger reads follow versions (spec 04 and 09).** `GET /trains` and the station board use only the service version in force for the date, skip cancelled dates, and skip journeys with status `Cancelled`; `trains/{n}/status` returns `404 journey_not_found` for a cancelled date. A cancelled train is therefore **absent**, not shown as Cancelled (Q5 decides whether to show it).
15. **Safety boundary (FR-63, A-05).** The screen keeps the safety notice; scheduling records the plan, it controls no train.
16. **Language.** Station names are shown in the controller's English with Sinhala beside them, as drawn; all text goes through the strings file.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- **Domain:** `ServiceDraft` (validated input), `ServiceValidator` (rules 2 to 8 as pure functions with a clock and the line data), `ServiceRuns(service, date, cancellations)` (the single place that decides whether a service runs on a date: kind, effective range, `runs_on`, cancellation), `ServiceCalendar` extended for day lists, `PlatformClash` (rule 6).
- **Application:** `Features/Timetable`: `ListServices`, `GetService`, `GetOptions`, `ValidateService`, `CreateService`, `AmendService`, `CancelService`, `JourneyGeneration` (the generator's core, callable from a handler and the hosted service). `ITimetableRepository`.
- **Infrastructure:** EF configurations, the migration, `TimetableRepository`, the advisory lock helper, `JourneyGeneratorService : BackgroundService`. The passenger queries (`PassengerQueries`, `GetStationBoard`, `FindTrains`) call `ServiceRuns` instead of reading `runs_on` directly.
- **Api:** `Features/Timetable/TimetableEndpoints.cs` with the routes above, `RequireAuthorization(Policies.Controller)`; added to the authenticated list in `AuthConfigurationTests`.
- **Telemetry:** counter `smartrail.timetable.changes` (`action` = created, amended, cancelled; `result` = ok, rejected, conflict), counter `smartrail.journeys.generated`, a log line per generator run (dates covered, journeys created); log fields `trainNumber`, `serviceId`, `actor`, never the token.
- **Web (`web/ops`):** `src/features/timetable/` with `TimetablePage` (tabs), `ServiceDetailPage`, `NewServicePage` (three steps, shared with Amend), `CancelDialog`, `queries.ts` (TanStack Query, no polling), `strings` additions; routes `/timetable`, `/timetable/new`, `/timetable/:serviceId`; a **Timetable** item in `OpsShell`. Form errors map from the `errors[].field` paths. Dates and times use the existing `core/time`.
- **Config:** `Scheduling:HorizonDays` (7), `Scheduling:GenerateEveryMinutes` (60), `Scheduling:PlatformWarningMinutes` (10); all configurable (NFR-41).

## 8) Edge Cases (proposed; none exist in the baseline)
| Case | Handling |
|---|---|
| Generator runs twice, or two instances run at once | No duplicate journey (unique index, `ON CONFLICT DO NOTHING`) |
| Service does not run on Sunday | No Sunday journey (AC-E02-01 3) |
| Controller clock is stepped forward (demo) | Generator uses `TimeProvider`; the next run fills the new horizon |
| Stops out of line order, a repeated station, one stop only | 422 `stops_not_in_line_order`, `duplicate_station`, `too_few_stops` |
| Arrival after departure, or a time going backwards | 422 `times_not_increasing` with the stop and the times |
| Platform of another station | 422 `platform_not_at_station`; the database also refuses |
| Same platform within 10 minutes of another train | Warning only; confirm to schedule |
| Date or effective date in the past | 422 `date_in_past` |
| Train number already used on overlapping dates | 409 `train_number_in_use` naming the clashing service |
| Amend a service that has a journey with events from that date | 409 `journey_has_events`; nothing changes |
| Cancel today's journey after it started | 409 `journey_has_events` (use an override, not built) |
| Cancel the same range twice | 200, 0 journeys changed |
| Overnight service (times pass midnight) | Allowed; instants from `ScheduleInstants`; under 24 h |
| Amended service queried for an old date | Old version's times (version in force that date) |
| Cancelled train searched by a passenger | Absent from results and board; status read 404 (Q5) |
| Controller loses connection while scheduling | The form keeps its values; the call is not repeated automatically; the 409 on retry shows it was or was not saved |

## 9) Acceptance Criteria (proposed, to be added to the baseline, Q8)
- [ ] The generator creates, for each running service and each of the next 7 days, exactly one Unassigned journey with a stop state per stop; running it again creates none; a service not running that day gets none (AC-E02-01 1 to 4).
- [ ] The Timetable lists weekly services, extra trains and cancelled services in three tabs with train, route, days, departure, stops and version (FR-11A).
- [ ] A controller can create a weekly service with chosen days, stops, times and platforms; "Every stop" fills the line in order; the new journeys appear within a minute (FR-11A, FR-11).
- [ ] A controller can add an extra train for one or more future dates (FR-11A).
- [ ] The server refuses: a date in the past, stops out of line order, times not increasing, a platform of another station, a train number clash; each with the field and the reason (FR-11D).
- [ ] A platform used by another train within the window gives a warning that can be confirmed, never a block (FR-11D, FR-57).
- [ ] Cancelling for one date or from a date onward needs a reason, keeps the service and its history, and no journey runs on those dates (FR-11C).
- [ ] A journey with events is never cancelled or regenerated by scheduling (409).
- [ ] Amending creates a new version and keeps the old one; journeys before the effective date keep the old times (FR-11B, D-70).
- [ ] The passenger search and the station board show the version in force for the date and no cancelled train.
- [ ] Operator, passenger and caterer tokens get 403, no token 401, on every timetable route.
- [ ] Every create, amend and cancel has an audit row with the actor.
- [ ] The existing tests of specs 03, 04, 09 still pass after the unique index and query changes.
- [ ] `dotnet test`, the web `npm test` and the Playwright flow are green; screenshots of the wizard, the warning and the cancel dialog are recorded.
- [ ] The report states plainly that passenger notification (FR-11B, FR-11C), refunds and caterer messages are not built, that operator assignment is not part of scheduling, and which of S1 to S4 were built.

## 10) Test Plan
### 10.1 Seed and fixture data
The seeded services (8058, 8061, 8075 and the demo trains), the five stations with platforms 1 and 2, controller and operator test tokens, a fake clock. New fixtures: a valid weekly body, an express with a skipped stop, an overnight service, a body per validation error, a second service on the same platform at +4 and +11 minutes.
### 10.2 Unit tests
`ServiceValidator` (every rule with passing and failing cases and boundaries: first departure now and now plus one minute, 24 hours, one stop, duplicate station, platform of another station), `ServiceCalendar` with day lists and the normalising of day sets, `ServiceRuns` (kind, effective range, cancellation range, from-onward), `PlatformClash` (10 minutes inclusive, different date, same train), generator date arithmetic across the horizon and midnight.
### 10.3 Integration tests (Postgres container)
Create weekly (journeys generated), create extra with two dates, 422 for each error, 409 train number and warnings not acknowledged, cancel one date and from-onward, cancel with events 409, amend with versions and with a journey that has events, generator idempotence (run twice, run concurrently), passenger `GET /trains` and the station board for an amended and a cancelled service, 401 and 403 matrix, audit rows, migration: constraints, the dropped index replaced, existing seed intact.
### 10.4 Web and end to end
Component tests for the wizard (steps, every stop, choose stops, errors from the server, warnings), the list tabs, the cancel dialog; Playwright against Compose: create service, see it in the list, see journeys generated, find it in the passenger search (API), cancel one date, see it under Cancelled.

## 11) References
- Specs 01 (database), 02 (Keycloak roles), 03 (events, `ScheduleInstants`), 04 (passenger reads, `ServiceCalendar` and R6), 08 (controller web), 09 (station board); `docs/smartrail-refined-requirements.md` FR-10, FR-11 to FR-11D, FR-57, NFR-41; `docs/smartrail-acceptance-criteria.md` AC-E02-01; D-69, D-70; ADR-11.
- Existing code reused: `ScheduleInstants`, `ServiceCalendar`, the `Policies.Controller` policy, the platform composite key, `DatabaseStartupService` hosting pattern, `web/ops` shell, query and form components.

## 12) Open Questions (recommendation first)
- **Q1. Scope and time.** About 3 days in total; only part can be built before 20 October. Recommend S1 plus S2 (read the timetable, generate journeys, create weekly and extra services with validation), then S3 (cancel) only if time allows, and S4 (amend with versions) last. If S2 is not built, FR-11A and FR-11D are unmet; if S3 and S4 are not built, FR-11B and FR-11C are unmet. Tell me the real budget and I will reduce the spec to match.
- **Q2. Versions as separate rows or snapshots.** Recommend a new `train_service` row per version, because journeys read their scheduled times from the service's stops: editing stops in place would rewrite the times of past journeys and of Previous journeys (spec 12). The cost is the dropped unique index and the passenger query changes. Alternative: keep one row and store a version snapshot as JSON (simpler to build, but history of old journeys is wrong after an amendment).
- **Q3. Extra trains with several dates.** Recommend one service row per date (reuses everything, the date is the effective range). Alternative: one service with a date list and a separate `service_date` table (cleaner to read, more code in every query).
- **Q4. Assignment.** Generated journeys are Unassigned and nobody can report on them, so scheduling does not make a new train usable by the operator app. FR-12 (assign an operator) and FR-56 (end an assignment) are Musts too. Recommend a separate small spec for a controller assignment endpoint and screen after S2. Until then only demo seeding assigns, and AC-E02-01 5 is unmet. Say so in the report.
- **Q5. What a passenger sees for a cancelled train.** Recommend, for now, absent from search and board (rule 14), and a second step that shows it as Cancelled (a status in the search result and the live screen, matching the Cancelled row and `LiveCancelled` board already built as fixtures in spec 10). Hiding it silently is weaker for a passenger who planned on it, so the second step matters; it is the same fixture work as the disrupted food screen.
- **Q6. Overlap check.** Recommend an application check inside a transaction with an advisory lock. Alternative: a range exclusion constraint (stronger) if the database image has `btree_gist`; I have not checked.
- **Q7. Undrawn screens.** Recommend I draw seven boards before the build: G1 Details step, G2 Review and schedule (with errors and warnings), G3 extra-train form, G4 cancel dialog, G5 amend entry, G6 Extra trains tab, G7 Cancelled tab (and empty states). Otherwise they are built from existing components and flagged as undesigned.
- **Q8. New acceptance criteria and edge cases.** The baseline has none for FR-11A to FR-11D. Recommend adding the section 9 list as AC-E02-03 onward and the section 8 table as EC entries, dated 19 October. That changes your requirements baseline, so it is your call.
- **Q9. Which stops need an arrival report.** The form has no field for it (the seed marks Gampaha, D-08). Recommend: a new service reports arrival at its last stop only (the existing rule for the final stop), and a controller-set flag is a later change. State it.
- **Q10. Amend from today.** Recommend the earliest effective date is tomorrow, so no running journey changes. Alternative: allow today for journeys that have not started (more rules to test).

## 13) Owner decisions of 19 Oct 2026
- Scope: **S1 (read the timetable, generate journeys) and S2 (validate and create weekly and extra services), backend only.** No web screens, no cancel (S3), no amend (S4). FR-11B, FR-11C, the controller screens and AC-E02-03 items for cancel and amend are therefore **not built** and the report must say so.
- Q2 to Q10 as recommended (Q7: no boards drawn; the screens are not built in this scope anyway).
- Branch `claude/feature/scheduling` starts from `claude/feature/operator-reports-history`; the stacked pull requests merge in order 10, 11, 12, 13.
- Narrowing made while building (stated, not hidden): the migration of this slice adds the `train_service` columns, the new unique index and `service_change_log` only; `service_cancellation` and the `Cancelled` journey status belong to S3. A service whose stops pass midnight is refused in this slice (times must increase within one calendar day); journeys of services that cross midnight come later. The journey generator is switched on by `Scheduling:GenerateJourneys` (off by default, on in Compose), so existing tests and the cluster are unchanged; a create always generates the journeys of the new service.
