# Feature Spec: Operator reports a departure, passengers see live status

branch: claude/feature/operator-report-live-status
spec: _specs/operator-report-live-status.md
owner: tharindu
date: 2026-10-12 (rewritten 2026-10-14 after the questioning rounds; builds on database-foundation and keycloak-identity)
status: ready, approved 2026-10-14 (owner approval given in chat)

---

## 0) Summary
**One-liner:** An operator reports "Departed <station>" for their assigned journey; the backend records it once, recalculates the delay and the ETAs, and a passenger can fetch the journey's live status.
**Why now:** It is the thinnest slice that touches every layer (token, role, assignment, rules, database, derived status, a client) and it is the core demo flow. Everything else (arrival, delay report, exceptions, SignalR, offline queue, overdue detection) builds on it. The database (13 tables, seed data) and Keycloak (Operator policy, test tokens) are already built.
**Who is this for:** Operator (reports), Passenger (reads, no account).
**Success looks like:**
- Nimal reports train 8058 leaving Veyangoda at 08:28 (8 minutes late); the status snapshot then shows "Departed Veyangoda" and Gampaha at 08:38 (+8 minutes), Ragama at 08:50, Maradana at 08:58 and Colombo Fort at 09:03 (the seed's scheduled times plus 8 minutes).
- The report is visible to a polling passenger well inside the 30 second target (NFR-04).
- The acceptance criteria below pass as automated integration tests, and `smartrail.events.received` is visible in Grafana.

## 1) Traceability
Requirement, acceptance-criterion and edge-case numbers below come from the owner's earlier draft. The owner will add the three documents that define them under `docs/` (`smartrail-refined-requirements.md`, `smartrail-acceptance-criteria.md`, `smartrail-edge-cases.md`). Until then the numbers are not checked against their source; this note is removed once they are.
- Requirements: FR-03, FR-05, FR-21, FR-35, FR-36 (accepted cases only), FR-38, FR-39, FR-40 (without its exception part), FR-43, FR-44, NFR-04, NFR-10, NFR-17
- Acceptance criteria: AC-E03-02 (1, 2, 4, 5, 6, 8), plus (7) refused with 409 and stored nowhere, and (10) accepted with delay 0 (its High exception waits for slice 2); AC-E04-01 (1); AC-E04-02 (1, 2, 5), and (3) **partial** (the expected next event time and the handover exception are not built); AC-E04-03 (1, 3), and (5) **partial** (the Level 1 content key and the expected next event time are not built); AC-E01-03 (1, 2, 3); AC-E06-02 (1)
- Edge cases: EC-02, EC-04, EC-06, EC-10, EC-15, EC-20, EC-27, EC-132, and EC-25 only for an operator calling a controller-only route. A controller calling the report route gets 403 as a design choice of the `Operator` policy, not because of EC-25.
- Decisions and assumptions: D-35, D-36, D-09, D-05, D-48, A-07 (configurable thresholds), A-TZ (Asia/Colombo), ADR-06 (server is the authority on time), ADR-08 (Keycloak), ADR-09 (snapshot plus version), D-28 (Keycloak knows the role, SmartRail knows the assignment)
- Design: operator app report screen; Pax-Results and live-status boards (hi-fi); wireframe Dash-02 (journey detail)

## 2) Goals and Non-Goals
### Goals
- Accept a departure event from an operator assigned at the event's occurrence time (judged at occurrence time, not arrival time).
- Make retries safe: the same client event ID is recorded once.
- Judge order by occurrence time. Accept late but consistent reports, and forward skips (skipped stops are recorded as Inferred).
- Derive delay and ETAs, and return a status snapshot with a version number.
- Store events append-only (the database already refuses UPDATE and DELETE).
- Let the operator app find its assigned journey and the stop IDs (`GET /api/v1/operator/journey`).
- Let a passenger find a train's journey by train number (`GET /api/v1/trains/{trainNumber}/status`): the journey in progress now, otherwise today's (R15).
- Support journeys that cross midnight and take at most 24 hours (the maximum travelling time), for example a train that leaves at 23:00 on Thursday and is still running at 08:00 on Friday (R16, R15). This lifts the midnight non-goal of `database-foundation` for the status logic only; the schema and the seed do not change.
### Non-Goals (this slice)
- Exceptions: slice 2 adds the `journey_exception` table. In this slice AC-E03-02 (5, 7) are refused with 409 and stored nowhere. AC-E03-02 (10), the early departure, is accepted with delay 0, and its High exception waits for slice 2. FR-40's exception part, and the handover exception in FR-38 and AC-E04-02 (3), are listed under D3.
- SignalR push (ADR-09): slice 2. Clients poll every 10 seconds. The `version` field is returned now so clients can already ignore older snapshots.
- Arrival and delay reports (FR-22, FR-23), flagging a wrong report (FR-27).
- Overdue detection, confidence and staleness: `expected_next_event_by` and `is_overdue` stay unset, and the snapshot omits `expectedNextEventBy`.
- The offline queue and retry back-off in the Flutter app (FR-24, FR-31): the API supports it through the idempotent client event ID, the client comes later.
- Controller entry of events, overrides, platform changes, reason verification.
- Assignment management (who creates assignments). The demo seed creates them.
- Any mobile or web screen. This spec is API only.
- Food ordering and caterer events: designed, not built (D-71, D-72). No outbox row is written.
- Statuses Unknown and Disrupted: the status field is shaped so they can be added by the slices that build them.
- Cluster databases: the endpoints in this spec need a database, which only Compose has today (section 7).

## 3) User Workflow
### Primary workflow
1. Nimal's operator app asks `GET /api/v1/operator/journey` and gets his assigned journey for now, with its stops and their IDs.
2. At 08:28 he taps "Departed Veyangoda". The app sends the event with a client event ID and the occurrence time.
3. The API checks the token, the role, his assignment at 08:28, duplicates, the time limits, the journey's state and the order.
4. The event, the rebuilt stop states and the journey (delay, status, version) are saved in one transaction.
5. Kasuni (passenger, no account) opens train 8058: `GET /api/v1/trains/8058/status`. She sees "Departed Veyangoda" and "Arrives at Gampaha 08:38 (+8 min)".
### Secondary workflows
- Same event sent again (a retry): acknowledged, nothing new stored.
- A report that occurred at 08:28 arrives after a later one (offline sync): accepted, and the state equals the in-order result.
- A stop is skipped: a later report is accepted, the skipped stop becomes Inferred, and no exception is raised.
- Nimal leaves a stop early: accepted, the delay is 0.

## 4) API Surface
All routes are under `/api/v1/`. Errors are RFC 9457 ProblemDetails with a short title and, where useful, a reason code, never more detail. The two status endpoints marked public are added to the public allow-list from `keycloak-identity`; every other endpoint stays protected by default.

### GET /api/v1/operator/journey
- Who: role `operator`.
- Returns the journey the signed-in operator is assigned to right now (an assignment with `valid_from <= now < valid_to`, or `valid_to` null).
- 200: `{ journeyId, trainNumber, operatingDate, status, version, stops: [{ serviceStopId, sequence, stationCode, stationName, scheduledArrival, scheduledDeparture, isFinalStop }] }`. `operatingDate` is the date the journey starts. `scheduledArrival` and `scheduledDeparture` are ISO-8601 UTC instants worked out by R16 (a stop after midnight is on the next calendar day), so they are unambiguous for a journey that crosses midnight; the app converts them to Asia/Colombo for display.
- 404: no assignment valid now (for example `operator2`). 401 no token. 403 wrong role.

### POST /api/v1/journeys/{journeyId}/events
- Who: role `operator`, assigned to that journey at `occurredAt` (FR-03). A controller gets 403, a design choice of the `Operator` policy.
- Request: `{ clientEventId: uuid, eventType: "Departed", serviceStopId: uuid, occurredAt: ISO-8601 UTC }`. All required.
- Order of checks: 401 or 403 (role) → 422 (request shape: a missing or invalid field, or an `eventType` other than `Departed`) → 404 (unknown journey) → 403 (not assigned at `occurredAt`) → duplicate checks (same client event ID, then same stop and time) → 422 (time limits, stop does not belong to the journey's service, departure from the final stop) → 409 (journey state) → 409 (order) → write.
- 201: `{ eventId, journeyVersion, status: "Delivered" }`.
- 200: the same body, with no second event stored (AC-E04-01 1), for either kind of duplicate: an exact repeat (same journey, same `clientEventId`, same content), or a different `clientEventId` for the same journey, stop and `occurredAt` (a duplicate by content; the body is the original event's, and it is counted as `duplicate`).
- 403 not assigned at that time: logged and counted, nothing stored (AC-E01-03 2).
- 409 (with a reason code): the journey is Unassigned or Completed; the same `clientEventId` arrives with different content; the stop already has a different recorded departure time (AC-E03-02 5); the report breaks the order rule, that departure times must strictly increase with stop sequence, so an equal time at another stop is also a conflict (R4); or the journey version changed and 3 attempts did not resolve it (the client may retry).
- 422: `occurredAt` more than `Events:MaxFutureMinutes` (1) ahead of the server clock or older than `Events:MaxAgeHours` (8) (EC-10, FR-38); the stop is not a stop of the journey's service; the stop is the final stop (no departure from it).
- Example: `POST /api/v1/journeys/{id}/events` with `{"clientEventId":"7f1e…","eventType":"Departed","serviceStopId":"<Veyangoda>","occurredAt":"2026-10-06T02:58:00Z"}` returns 201 `{"eventId":"…","journeyVersion":1,"status":"Delivered"}`.

### GET /api/v1/journeys/{journeyId}/status (public)
- Who: anyone, no token (FR-05). 404 for an unknown journey.
- 200: `{ journeyId, trainNumber, operatingDate, version, serverTime, statusGroup, statusKey, delayMinutes, primaryEta: { stationCode, stationName, time }, lastKnownPosition: { stationCode, stationName, departedAt }, stops: [{ serviceStopId, sequence, stationCode, stationName, scheduledArrival, scheduledDeparture, estimatedArrival, estimatedDeparture, actualDeparture, confirmation }] }` (AC-E04-03 5).
- `statusGroup` is `Normal` or `Warning`. `statusKey` is `Scheduled` (no report yet), `OnTime` or `Delayed`.
- `primaryEta` is the estimated arrival at the next stop after the last reported departure; `lastKnownPosition` is the last reported departure. Both are absent before the first report.
- Times are ISO-8601 UTC instants. `serverTime` is the server clock when the snapshot was built (ADR-06).

### GET /api/v1/trains/{trainNumber}/status (public)
- Who: anyone, no token.
- The same 200 body as above, for the journey of that train that is in progress now, otherwise the journey whose operating date is today (R15). A train that left on Thursday at 23:00 is therefore still returned on Friday at 08:00. A train that is still running never disappears because the date changed at midnight. 404, with a clear "no journey for this train today" reason, when the train number does not exist or there is no journey in progress and none for today.

## 5) Data Contract
No schema change and no new migration: all columns already exist (`database-foundation`). Names follow [smartrail-schema.sql](smartrail-schema.sql).
- Reads: `journey`, `train_service`, `service_stop`, `station`, `operator_assignment`, `journey_stop_state`, `train_event`.
- Writes: `train_event` (insert only), `journey_stop_state` (estimates, actual departure, confirmation), `journey` (`current_delay_minutes`, `status`, `version`).
- Constraints that matter: `UNIQUE (journey_id, client_event_id)`; index `(journey_id, occurred_at)`; `journey.version` as the optimistic-concurrency token.
- Append-only: `smartrail_app` has SELECT and INSERT only on `train_event`, and the trigger refuses UPDATE, DELETE and TRUNCATE (AC-E04-02 5, EC-132). Both are already in place.
- The API role has ordinary read and write access to the other tables through the default privileges, so no grants are added.
- Event content for the "same ID, different content" check is `eventType`, `serviceStopId` and `occurredAt`.

## 6) Business Rules
1. R1 (FR-03, AC-E01-03 4): authorised when the operator has an `operator_assignment` covering `occurredAt` (`valid_from <= occurredAt < valid_to`, or `valid_to` null). Judged at occurrence time, not at arrival time.
2. R2 (FR-35, D-35): the pair (journey, `clientEventId`) is recorded once. An exact repeat returns the original result (200); the same ID with different content returns 409 and changes nothing. A different `clientEventId` for the same journey, stop and `occurredAt` is a duplicate by content: it returns 200 with the original event's body, stores nothing new, and `smartrail.events.received` counts it as `duplicate`. The same stop with a different time is not a duplicate: it returns 409 (AC-E03-02 5).
3. R3 (FR-38): reject when `occurredAt` is more than `Events:MaxFutureMinutes` (default 1) ahead of the server clock or older than `Events:MaxAgeHours` (default 8). The clock is `TimeProvider`, never `DateTime.Now`. A repeat of an already accepted event is answered by R2 before this check, so a late retry still gets its original result.
4. R4 (FR-36): order is by occurrence time, and **departure times must strictly increase with stop sequence**. A report for a later stop than the last recorded one marks every stop in between Inferred (forward skip). A report that fits earlier in the order (late delivery) is accepted when its time is strictly later than every recorded departure at earlier stops and strictly earlier than every recorded departure at later stops (AC-E03-02 8). Anything else is impossible and is refused with 409 and stored nowhere: a time that is not later than an earlier stop's departure, a time that is not earlier than a later stop's departure, an equal time at a different stop, or a second, different time for a stop that already has one (AC-E03-02 5, 7).
5. R5 (FR-39, D-05, D-48): the delay is worked out in this order: (1) actual departure minus the scheduled departure instant of the reported stop (R16); (2) round to a whole minute with `MidpointRounding.AwayFromZero`; (3) clamp to 0, so it is never negative; (4) compare with the on-time threshold (R6). Each later stop's estimated arrival and departure = its scheduled time plus that delay (carried unchanged, no catch-up in this slice). A departure before its scheduled time is accepted and gives delay 0 (AC-E03-02 10; its High exception waits for slice 2).
6. R6 (FR-40, A-07): `OnTime` when the delay from R5 is at most `Journeys:OnTimeToleranceMinutes` (default 2), else `Delayed`. A journey with no report is `Scheduled`. `OnTime` and `Scheduled` map to status group `Normal`; `Delayed` maps to `Warning`.
7. R7 (FR-43): the event row stores source `Operator`, `created_by` = the token subject, `occurred_at`, `received_at` (server time) and `journey_version_after`.
8. R8 (EC-15): a journey that is `Unassigned` or `Completed` refuses reports with 409 and a reason code. Slice 1 never sets `Completed`.
9. R9: the first accepted report moves a `Scheduled` journey to `Running`.
10. R10 (late-report rule): after every accepted event the stop states, delay and estimates are rebuilt from all of the journey's events in occurrence-time order. R4 makes that order strict, so there are no ties. The journey's delay is the delay from the latest departure in occurrence order (R5). A stop with a reported departure shows its actual departure; a stop after the last reported departure shows estimates (R5); a stop that was skipped (Inferred) has no estimate and no actual time of its own, only `confirmation: Inferred`. The arrival order of reports can therefore never change the result.
11. R11 (ADR-09): saving the event, the stop states and the journey (with `version + 1`) is one transaction guarded by the journey version. On a version conflict the whole step is retried, up to 3 attempts, then 409.
12. R12: a refused report is logged and counted (journey ID, client event ID, subject, reason code; never the token) and stores no event.
13. R13: the report's `serviceStopId` must belong to the journey's service and must not be its final stop, and `eventType` must be `Departed` in this slice (422 otherwise).
14. R14 (no database): the endpoints in this section are mapped only when `ConnectionStrings:Smartrail` is set, as the database registration is. Without it (the cluster today) they do not exist, readiness is unchanged, and the cluster apps keep working.
15. R15 (train lookup, FR-05): `GET /trains/{trainNumber}/status` returns, in this order:
    1. **The journey in progress now.** Candidates are the train's journeys whose operating date is from two days ago to today (`OperatingTime.Today(clock)`: the calendar date in Asia/Colombo from the server clock, changing at 00:00 local; a journey takes at most 24 hours, so unless it is more than a day late it cannot still be in progress from an earlier date). A journey is in progress when now is at or after its first stop's scheduled departure instant (R16) and not later than the end, where the end = the last stop's estimated arrival (its scheduled arrival plus the journey's current delay) plus `Journeys:VisibleAfterEndMinutes` (default 15). This rule needs no `Completed` status, which the arrival slice sets later. If two candidates are in progress, the one that started later wins.
    2. Otherwise, **the journey whose operating date is today**, whether or not it has started or ended.
    3. Otherwise 404 with a clear reason (no journey for this train today). A finished journey is not shown once its grace period has ended: a passenger has no use for yesterday's "Arrived", and it could be read as today's.
    **A train that is still running never disappears because the calendar date changed.** Midnight is not a boundary of the "in progress" rule. So a train that leaves on Thursday at 23:00 is returned on Friday at 00:10 and at 08:00, and for the grace period after its end (so people meeting it can see "Arrived"), then Friday's journey of the same train takes over if there is one. The lookup by journey ID never depends on any of this.
16. R16 (scheduled instants, A-TZ): the schema stores stop times as plain local times of day. The scheduled instant of every stop is worked out by walking the service's stops in sequence, starting from the journey's operating date at the first stop's time: each time (arrival, then departure) that is earlier than the one before it is on the next calendar day. A journey that leaves at 23:50 and has stops at 00:20 and 08:00 therefore has instants on Thursday 23:50, Friday 00:20 and Friday 08:00. Times are converted to UTC with Asia/Colombo, which has no daylight saving. This supports journeys of at most 24 hours (the maximum travelling time); a longer one is out of scope and is not detected. R5, R10, R15 and every scheduled value in the responses use these instants.
- Missing data: a journey with no stop state rows is a server problem (500 with a log entry); the seed makes it impossible (AC-E02-01).

## 7) Architecture (follow CLAUDE.md and ADR-15)
Feature folders, no cross-feature references:
- `Domain/Journeys/JourneyProgress` (new, pure code, no EF): rebuilds stop states, delay, status and ETAs from a stop list and an ordered list of events (R4, R5, R6, R10). Fully unit-testable with no database.
- `Application/Features/Events/ReportDeparture`: command, validator, handler (one transaction, assignment check, duplicate check, retry loop). `Application/Features/Journeys/GetJourneyStatus` and `GetOperatorJourney`: queries and handlers. Interfaces in Application (for example `IJourneyRepository`), implemented in Infrastructure with EF Core. The existing `GetServiceStatus` placeholder stays (it serves `/api/v1/status`).
- `Api`: one `Map<Feature>` extension per feature, mapped only when a database is configured (R14). `POST` and `GET /operator/journey` use the `Operator` policy; the assignment check runs in the handler, not the endpoint (D-28). The two status GETs are `.AllowAnonymous()`.
- Authorisation test list: add `/api/v1/journeys/{journeyId}/status` and `/api/v1/trains/{trainNumber}/status` to the public allow-list in `AuthConfigurationTests`. The enumeration test must run with a database configured (a connection string set), or it will not see the new routes (R14). It then still fails for anything else that is anonymous.
- Configuration (section names are the contract): `Events:MaxFutureMinutes` (1), `Events:MaxAgeHours` (8), `Journeys:OnTimeToleranceMinutes` (2), `Journeys:VisibleAfterEndMinutes` (15).
- R16 lives in `Domain` next to `OperatingTime` (for example `ScheduleInstants.For(operatingDate, stops)`), as pure code with unit tests. `JourneyProgress` and the lookup both use it.
- Telemetry: counter `smartrail.events.received` (tags `event_type`, `outcome` = recorded, duplicate or rejected; `deployment.environment` comes from the resource); histogram `smartrail.events.report_to_visible_seconds` (the time from `occurredAt` to the commit, in seconds; the passenger's 10 second polling adds to it, NFR-04); a span per command; log fields `journeyId`, `clientEventId`, `subject`.
- Clients that will depend on this: the Flutter operator app, the passenger app and station displays, the controller dashboard (all later).

## 8) Edge Cases handled in this slice
| EC | Handling |
|----|----------|
| EC-02 | An exact repeat returns the original result (200), no error |
| (new) | The same client event ID with different content: 409, nothing changes |
| (new) | A different client event ID for the same stop and time: 200 with the original event, nothing stored, counted as duplicate |
| (new) | The same stop with a different time (AC-E03-02 5): 409 |
| (new) | An equal departure time at a different stop: 409 (strict order rule, R4) |
| EC-04 | A late, correct-order report is accepted; the rebuilt state equals the in-order state |
| EC-06 | Forward skip: the stops in between become Inferred, no exception |
| EC-10 | Time outside the limits: 422 |
| EC-15 | Journey Unassigned (or Completed): 409 with a reason code |
| EC-20 | Assignment judged at occurrence time |
| EC-25 | An operator calling a controller-only route: 403 (proved by the authentication tests) |
| (design) | A controller calling the report route: 403, because of the `Operator` policy |
| EC-27 | Not assigned to the journey: 403, logged and counted, nothing stored |
| EC-132 | The database refuses UPDATE and DELETE on `train_event` (already built) |
| (new) | Early departure: accepted, delay 0 |
| (new) | Two reports at the same moment: version conflict, retried up to 3 times, then 409 |
| (new) | Operator with no assignment now: `GET /operator/journey` returns 404 |
| (new) | Unknown train number or no journey today: 404 |
| (new) | A train that left on Thursday at 23:50 is still running at Friday 00:10 and 08:00: the lookup by train number returns Thursday's journey (R15) |
| (new) | Thursday's train ended and the grace period is over: the lookup returns Friday's journey of the same train, or today's (R15) |
| (new) | A finished train after its grace period, with no journey for today: 404 with a clear reason, never yesterday's data (R15) |
| (new) | A stop after midnight: its scheduled instant is on the next calendar day, so the delay is correct (R16) |
| (new) | No database configured: the endpoints are not mapped (R14) |
Deferred to slice 2: EC-05, EC-07, EC-08 (exceptions).

## 9) Acceptance Criteria
- [ ] A departure by the assigned operator at 08:28 is stored with `occurredAt` 08:28 and returns 201 Delivered (AC-E03-02 1).
- [ ] The status snapshot then shows Veyangoda departed and estimated arrivals of Gampaha 08:38, Ragama 08:50, Maradana 08:58 and Colombo Fort 09:03 (the seed's scheduled times plus 8; the acceptance-criteria document's 08:48 for Ragama is not used) (AC-E03-02 2, AC-E04-03 1). The journey is `Running`, its version is 1, and `statusKey` is `Delayed` with group `Warning`.
- [ ] The same event sent twice stores one row and the second call returns 200 with the same body (AC-E03-02 4, AC-E04-01 1).
- [ ] A different client event ID for the same journey, stop and `occurredAt` returns 200 with the original event's body, stores nothing new, and is counted as `duplicate`.
- [ ] The same client event ID with a different stop or time returns 409 and stores nothing. The same stop with a different time (new client event ID) returns 409 (AC-E03-02 5).
- [ ] An equal departure time at a different stop returns 409; departure times that do not strictly increase with stop sequence return 409 (R4).
- [ ] A report for Ragama with no Gampaha report is accepted and marks Gampaha Inferred, with no estimate and no actual time of its own and no exception (AC-E03-02 6).
- [ ] A Veyangoda report that occurred at 08:28 and arrives after a Ragama departure report (08:51, which is 08:43 plus 8) is accepted, and the state equals the in-order result (AC-E03-02 8).
- [ ] A departure before the scheduled time is accepted, with delay 0 (AC-E03-02 10).
- [ ] A delay of 8 minutes 30 seconds becomes 9 minutes, 7 minutes 29 seconds becomes 7, and 30 seconds early becomes 0 (R5).
- [ ] No token: 401. A controller token on the report route: 403. An operator not assigned at `occurredAt`: 403, logged and counted, no event stored (AC-E01-03 1 to 3).
- [ ] `occurredAt` 2 minutes in the future or 9 hours old: 422 (AC-E04-02 1, 2). A late retry of an accepted event still returns 200.
- [ ] A journey that is Unassigned or Completed: 409. A departure from the final stop, an unknown stop, or an event type other than Departed: 422.
- [ ] Two simultaneous reports both end up recorded, or one gets 409 after 3 attempts, and the journey version never skips or repeats.
- [ ] The application database role cannot UPDATE or DELETE `train_event` (AC-E04-02 5, already proved by the database tests).
- [ ] `GET /api/v1/operator/journey` returns Nimal's journey with its stops and stop IDs; `operator2` gets 404.
- [ ] `GET /api/v1/trains/8058/status` returns today's journey; an unknown train number returns 404.
- [ ] A night train (departs Thursday 23:50, stops at Friday 00:20 and Friday 08:00) is returned by the train lookup on Friday at 00:10, and at 08:00, and until its last stop's estimated arrival plus 15 minutes (R15). One minute after that, the lookup returns Friday's journey of the same train, or 404 if there is none (R15).
- [ ] No train disappears at midnight: for a day train that finished at 20:00, lookups at 23:59 and at 00:01 on the next day both return a journey of that train (today's if there is one, otherwise the same finished journey with its operating date), never 404.
- [ ] If that night train is running 8 minutes late, it stays visible until 8 minutes after its scheduled end plus 15 minutes (the end uses the estimated arrival).
- [ ] The scheduled instants of the night train are Thursday 23:50, Friday 00:20 and Friday 08:00, and a departure from the 00:20 stop reported at 00:28 gives delay 8 (R16, R5).
- [ ] For a day train (8058), a lookup at 15:00 after it has finished and the grace has passed returns today's journey, not 404.
- [ ] The train lookup returns 404 for an unknown train number, and for a train with no journey in progress and none for today (for example a day train that finished at 20:00, after its 15 minute grace period, with no journey on the new date). It never returns an older finished journey.
- [ ] A journey of exactly 24 hours (for example departs 23:50 Thursday, last stop 23:50 Friday) gets the right scheduled instants (R16) and, while running, is returned by the lookup (R15).
- [ ] A 24 hour journey that is running late is still returned while it is in progress, including when its operating date is 2 days before today.
- [ ] The two status GETs work with no token, and the allow-list test, run with a database configured, passes with exactly those two additions.
- [ ] `smartrail.events.received` is visible in Grafana with the outcomes recorded, duplicate and rejected.
- [ ] With no database configured the new endpoints do not exist, and `/health/ready` and `/api/v1/status` behave as before (the cluster apps stay Synced and Healthy).
- [ ] CI is green: build with `-warnaserror`, vulnerable-package check, and all tests.

## 10) Test Plan
### 10.1 Seed and fixture data
The clock is set per test, to the reported time or later: 08:28 on 2026-10-06 (a Tuesday) for the happy path, and never earlier than `occurredAt` minus the 1 minute allowance, or R3 would reject the report as coming from the future. A single clock fixed at 08:20 would reject the 08:28 report. Service 8058 uses the seed times in `database-foundation.md` (Veyangoda departs 08:20; Gampaha arrives 08:30, departs 08:31; Ragama 08:42 and 08:43; Maradana 08:50 and 08:51; Colombo Fort arrives 08:55). The demo seed assigns Nimal for the whole operating day. Tests that need a narrow window create their own assignment (for example 08:00 to 10:00). A second operator (`operator2`) has no assignment. The seed has no night train, so the tests create their own service: Veyangoda 23:50, a stop at 00:20 and a final stop at 08:00 the next day, with its own journey and an assignment that covers the night. The demo assignment (the operating date only) is not used for it. Tokens come from the test token issuer; Nimal's subject is `DemoIds.Nimal`.
### 10.2 Unit tests (Domain and Application, no database)
R16 scheduled instants (a day train unchanged; 23:50, 00:20, 08:00 gives Thursday 23:50, Friday 00:20, Friday 08:00; a departure time earlier than its arrival at the same stop moves to the next day; the Asia/Colombo conversion). R15 selection with a fake clock (in progress, the grace boundary, two candidates, none in progress, no journey). `JourneyProgress`: delay and ETAs (+8 carried to every later stop; early departure gives 0); rounding at exactly 30 seconds (8 minutes 30 seconds gives 9, 7 minutes 29 seconds gives 7, 30 seconds early gives 0 after the clamp); on time at 2 and delayed at 3 minutes; forward skip marks the stops in between Inferred with no estimate; out-of-order arrival gives the same final state; a conflicting second time for a stop; an equal time at another stop; strict increase with stop sequence. Assignment window edges (equal to `valid_from`, equal to `valid_to`, `valid_to` null). Time limits at exactly +1 minute and −8 hours. Status group mapping.
### 10.3 Integration tests (WebApplicationFactory plus Postgres in Testcontainers)
Happy path; duplicate; duplicate by content (different client event ID, same stop and time); same ID with different content; the train lookup for a night train that left the day before (at 00:10, at 08:00, at the end plus 15 minutes and one minute later, and when it runs 8 minutes late); the lookup for a day train after it has finished, and across midnight (23:59 and 00:01 with and without a journey on the new date); the 2 day limit (2 days old returned, 3 days old not); a 24 hour journey, on time and late; no token; wrong role; controller on the report route; not assigned; Unassigned and Completed journeys; invalid times; the late retry; late-arrival ordering; version conflict with two concurrent requests; the append-only trigger through the API role; status snapshot content and version increment; the operator journey and train number lookups; the allow-list test; the metric outcomes; no database configured.

## 11) References
- `database-foundation.md` (tables, `DemoIds`, privileges), `keycloak-identity.md` (policies, test token issuer, allow-list), `android-signin-spike.md` and its RESULT.md (the operator app's sign-in), ADR-06, ADR-08, ADR-09, ADR-15, CLAUDE.md.
- To be added by the owner under `docs/`: `smartrail-refined-requirements.md`, `smartrail-acceptance-criteria.md`, `smartrail-edge-cases.md`. Their numbers are used above and will be checked then.
- Existing code reused: `Features/Status` (endpoint pattern), `TimeProvider`, `OperatingTime`, the entities in `Domain/Journeys`, the auth policies.

## 12) Open Questions and Decisions
### Open questions
None. (Required empty before approval.)
### Decisions made on 2026-10-14
| # | Decision | Recorded in |
|---|----------|-------------|
| Q1 | Passengers poll every 10 seconds; no SignalR in slice 1 | section 2 |
| Q2 | Routes as proposed, with the two additions below | section 4 |
| Operator journey | `GET /api/v1/operator/journey` returns the assigned journey and stop IDs; 404 when none | section 4 |
| Passenger lookup | `GET /api/v1/trains/{trainNumber}/status` for today, plus the lookup by journey ID | section 4 |
| Q3 | Impossible-order reports are refused with 409 and stored nowhere; exceptions come in slice 2 | R4 |
| Early departure | Accepted, delay clamped to 0 (the 409 line in the old draft is removed) | R5 |
| Overdue | `expected_next_event_by` stays empty; overdue logic is a later slice | section 2 |
| Thresholds | Configuration with defaults 2 minutes, 1 minute, 8 hours | section 7 |
| Late reports | Rebuild from all events in occurrence order after every report | R10 |
| Version conflict | The server retries up to 3 times, then 409 | R11 |
| Not assigned | 403, logged and counted, nothing stored | R1, R12 |
| Status groups | Normal for on time or not yet departed, Warning for late | R6 |
| Same ID, different content | 409, nothing changes | R2 |
| Journey state | Unassigned or Completed: 409 with a reason code | R8 |
| No assignment | `GET /operator/journey` returns 404 | section 4 |
| Journey status | The first accepted report moves Scheduled to Running | R9 |
### Decisions made on 2026-10-14, from the owner's review
| # | Decision | Recorded in |
|---|----------|-------------|
| Duplicate by content | A different client event ID for the same journey, stop and `occurredAt` returns 200 with the original event, stores nothing, counts as `duplicate`. The same stop with a different time is 409 (AC-E03-02 5) | R2 |
| Order rule | Departure times must strictly increase with stop sequence; an equal time is a 409. No tie-break exists | R4, R10 |
| Operator journey response | No `assignedFrom` or `assignedTo` | section 4 |
| Overnight trains | A train that left on Thursday at 23:00 must still be visible on Friday at 08:00 (the owner's requirement, replacing the earlier 404 at 00:10). `OperatingTime.Today` alone cannot do it: it is the calendar date in Asia/Colombo and changes at 00:00 | R15 |
| Lookup rule | The journey in progress now (first scheduled departure passed, not past the last estimated arrival plus 15 minutes), otherwise today's journey, otherwise 404 with a reason. The earlier "last 2 days" fallback was removed on 2026-10-14: from a passenger's view a finished train has no use after its grace period and old data can be mistaken for live. The in-progress candidates are the operating dates from two days ago to today | R15 |
| Maximum travelling time | A train takes at most 24 hours (the owner's rule), which bounds R16 and the lookup | R15, R16 |
| No disappearing at midnight | The owner's rule: a train that is still running must never vanish because the date changed (its "in progress" status ignores midnight). A train that has finished does not need to stay | R15 |
| Stop times across midnight | Walk the stops in order from the operating date; a time earlier than the previous one is on the next day; no schema change, journeys of at most 24 hours | R16 |
| Rounding | `MidpointRounding.AwayFromZero`, then clamp to 0, then the on-time threshold; unit test at 30 seconds | R5 |
| Test clock | Set per test to the reported time or later (08:28 for the happy path) | section 10.1 |
| Exceptions | AC-E03-02 (5, 7) refused with 409, stored nowhere; (10) accepted with delay 0; the exception parts wait for slice 2 | sections 1, 2, D3 |
| Allow-list test | Runs with a database configured so it sees the new routes | section 7 |
### Deferred decisions
| # | Decision | Fallback chosen now | Revisit when |
|---|----------|---------------------|--------------|
| D1 | An operator with two overlapping assignments | `GET /operator/journey` returns the assignment with the latest `valid_from` and logs a warning; the report endpoint checks the one named in the URL | Assignment management is built |
| D2 | SignalR push, and the version rule on the client | Polling with the `version` field | Slice 2 |
| D3 | Exceptions: the 409 order cases (AC-E03-02 5, 7), the High exception for an early departure (AC-E03-02 10), FR-40's exception part, and the handover exception in FR-38 and AC-E04-02 (3) | Refuse (409) or accept as described and store no exception | Slice 2 |
| D4 | Overdue detection, confidence, staleness, Unknown status | Not built; fields stay empty | The overdue slice |
| D5 | Catch-up (a train recovering lost time) in the ETAs | Delay carried unchanged to every later stop | A later slice, with real timetable data |
| D6 | Running slice 1 in the cluster | The endpoints exist only where a database is configured (R14) | The `cluster-databases` spec |
| D7 | Journeys longer than 24 hours, and a demo assignment for overnight trains | Out of scope: R16 supports journeys of at most 24 hours; the demo assignment covers the operating date only, so night-train tests create their own | The first real timetable of that length, or assignment management |
| D8 | Ending a journey's visibility at `Completed` (set by the arrival slice) instead of the estimated end plus a grace period | The estimated end plus `Journeys:VisibleAfterEndMinutes` (15 minutes), which covers errors in the estimate because slice 1 has no arrival report | The arrival slice: set the grace period to 0, so a finished train leaves the lookup at arrival |
### Proposed additions to confirm on review
- The status response includes `serverTime` (ADR-06).
- `report_to_visible_seconds` is measured from `occurredAt` to the commit; polling time is added by the client.
- R14: without a database the endpoints are not mapped at all, as the whoami flag does. The alternative is to map them and answer 503 with a ProblemDetails.
- Decision owner: tharindu. Approval needed before the build starts.
