# Feature Spec: Operator reports arrival and delay, journeys complete

branch: claude/feature/operator-arrival-delay-reports
spec: _specs/06-operator-arrival-delay-reports.md
owner: tharindu
date: 2026-10-15
status: ready, approved 2026-10-15 (owner approval given in chat with the recommended answers to Q2, Q3 and Q4). Built as A1 (arrival and Completed) then A2 (delay).

---

## 0) Summary
**One-liner:** An operator reports "Arrived <station>" at a designated arrival station and a delay with a reason and an estimate of the total delay. The server records both, recalculates the delay and the ETAs, and marks the journey Completed when the arrival at the final stop is accepted.
**Why now:** FR-20 and FR-23 are Must requirements and the operator app only partly meets them: arrival, delay and the completed journey are drawn in the designs but the API refuses them (slice 1 answers 422 to any event type other than Departed). This slice reuses the slice 1 pipeline (idempotency, order by occurrence time, rebuild from all events, append-only audit trail), so it is the smallest step that closes the gap.
**Who is this for:** Operator (reports), Passenger (sees the delay and the arrival; no account).
**Success looks like:**
- Nimal reports Departed Veyangoda 08:28 (+8), then DelayReported "Signal issue, total +10" at 08:35: the status read shows delay 10 and Gampaha 08:40 (AC-E03-04 2). The status read contains no reason (AC-E03-04 3).
- Nimal reports Arrived Gampaha at 08:40 (+10 against the scheduled 08:30): the status read shows "Arrived Gampaha 08:40", Gampaha departure 08:41, Ragama 08:52, Maradana 09:00, Colombo Fort 09:05.
- Arrived Colombo Fort at 09:05: the journey is Completed with no further tap (AC-E03-03 4), and `smartrail.journeys.completed` moves in Grafana.
- A retry of any of these returns 200 and stores nothing twice.

**Size and the freeze (for the owner to decide, Q1).** No migration, no new table, no new route. The change is one generalised handler, one rebuild function, three small additions to the status read, and about 40 tests. My estimate is about half a working day for the backend and half a day for the app. It can be cut in two pull requests if time is short: A1 arrival and Completed, A2 delay.

## 1) Traceability
Checked on 2026-10-15 against `docs/smartrail-refined-requirements.md` v1.13, `docs/smartrail-acceptance-criteria.md` and `docs/smartrail-edge-cases.md`.
- Requirements: FR-22, FR-23, FR-24 and FR-25 (the app side), FR-35, FR-36, FR-38, FR-39, FR-42 (the reason is never published here), FR-43, FR-44 (partly), NFR-04, NFR-10, NFR-17 (unchanged).
- Acceptance criteria used (full text in `docs/smartrail-acceptance-criteria.md`):
  - AC-E03-03 (1) (2) (3) (4): arrival at designated stations, no arrival action elsewhere, offline arrival, Completed at the final stop.
  - AC-E03-04 (1) (2) (3) (5) (6): delay with reason and total, ETAs recalculated, reason not shown, Unknown, only options above the running delay.
  - AC-E04-01 (1): a repeat is acknowledged once. AC-E04-02 (1) (2) (4): time limits and out-of-order arrival. AC-E04-03 (1) (2) (4): ETAs, no negative delay, a total delay is not added to the current one.
  - **Not built here:** AC-E03-04 (4) (verification, FR-52), AC-E03-04 (7) (the Critical exception, D-44), AC-E03-06 (flag), AC-E03-01 (6) (suspended), AC-E03-02 (5) (7) (10) (exceptions). They wait for slice 2.
- Edge cases: EC-02, EC-04, EC-06, EC-10, EC-15, EC-16 (handled by refusing, see R8), EC-17, EC-20, EC-27, EC-35, EC-37.
- Decisions and assumptions: D-05, D-07 (total, not extra), D-08 (arrival only at designated stops), D-09, D-10 (reason published only after verification), D-35, D-36, D-48, A-TZ, ADR-06, ADR-09, and D8 of `03-operator-report-live-status.md` (Completed ends visibility; this spec changes how, see R11).
- Design: hi-fi canvas boards `Nimal-Journey` (arrival), `Nimal-DelayReason`, `Nimal-DelayEstimate`, `Nimal-DelaySent`, `Nimal-DelayNotSent`, `Nimal-Completed`; wireframes Op-05, Op-08 to Op-11, Op-15. No new screen is needed.

## 2) Goals and Non-Goals
### Goals
- `POST /api/v1/journeys/{journeyId}/events` accepts `eventType` `Arrived` and `DelayReported` besides `Departed`. The same rules apply to all three: assignment judged at occurrence time, idempotency by client event ID, time limits, one transaction, rebuild from all events.
- An arrival is accepted only at a designated stop (the stop's `arrival_report_required` flag, D-08) or at the final stop.
- A delay report carries a reason (`SignalIssue`, `TechnicalFault`, `Obstruction`, `Weather`, `Unknown`) and either a total delay in minutes or "unknown". A numeric total must be greater than the delay in force at that moment (AC-E03-04 6).
- The status read shows the delay, the ETAs and the arrival. It never shows the reason (AC-E03-04 3).
- Arrival at the final stop sets the journey `Completed` (AC-E03-03 4).
- The operator app can then enable arrival and delay sending (section 7, clients).
### Non-Goals
- Controller verification of the reason (AC-E03-04 4, FR-52), and any exception record: Critical for a technical fault, obstruction or weather (AC-E03-04 7), High for an early departure (AC-E03-02 10); an early arrival raises none. All exceptions wait for slice 2 (Q3).
- Flag my last report (AC-E03-06), the suspended state, the free-text "Other" reason (FR-30, Should), controller entry of events (FR-53), push notifications, SignalR.
- Any change to the database schema (section 5).

## 3) User Workflow
### Primary workflow
1. Nimal's app reads `GET /operator/journey` and sees, for each stop, whether it is an arrival stop (`isArrivalStation`).
2. 8058 reaches Gampaha (designated). He taps "Arrived Gampaha". The app sends `Arrived` with the stop ID and the corrected occurrence time. The server answers 201 and the status read shows "Arrived Gampaha 08:40".
3. He taps "Departed Gampaha" as before. At a stop that is not designated the app offers no arrival action (AC-E03-03 2).
4. The train is late. He taps "Report delay", picks "Signal issue", then a total of +10, +15, +20 or Unknown. The app sends `DelayReported`. Passengers see "Delayed (~10 min)" at once; the reason is not shown (FR-42).
5. At Colombo Fort he taps "Arrived Colombo Fort". The server records it and sets the journey Completed. The app shows the completed screen (`Nimal-Completed`).
### Secondary workflows
- **Offline:** arrival and delay reports are queued like departures, with their occurrence time, and sent in occurrence order (AC-E03-03 3). A late report is judged by its occurrence time (AC-E04-02 4).
- **Skipped stop:** an arrival or departure at a later stop marks the unreported stops before it as Inferred (as slice 1 does).

## 4) API Surface
No new route. One route gains two event types, and the two reads gain a few fields.

### POST /api/v1/journeys/{journeyId}/events (changed)
- Who: role `operator`, assigned to the journey at `occurredAt` (FR-03).
- Request: `{ clientEventId: uuid, eventType: "Departed" | "Arrived" | "DelayReported", serviceStopId?: uuid, occurredAt: ISO-8601 UTC, reason?: string, totalDelayMinutes?: int, delayUnknown?: bool }`.

| eventType | serviceStopId | reason | totalDelayMinutes / delayUnknown |
|---|---|---|---|
| Departed | required | must be absent | must be absent |
| Arrived | required | must be absent | must be absent |
| DelayReported | must be absent | required: `SignalIssue`, `TechnicalFault`, `Obstruction`, `Weather`, `Unknown` | exactly one of: `totalDelayMinutes` from 1 to 240, or `delayUnknown: true` |

  A field that does not belong to the type, or a missing or invalid required field, is 422 `invalid_request`.
- Response: 201 `{ eventId, journeyVersion, status: "Delivered" }`; 200 with the original body for a repeat. Unchanged.
- Order of checks (extends slice 1): 401 or 403 (role) -> 422 request shape -> 404 unknown journey -> 403 not assigned at `occurredAt` -> duplicate checks -> 422 time limits, stop not in the service, final stop (Departed only), **not an arrival station (Arrived only)** -> 409 journey state -> 409 order, **409 delay not greater (DelayReported only)** -> write.
- Errors: the existing reason codes stay. New: `not_arrival_station` (422), `delay_not_greater` (409). `stop_has_different_time` now also covers an arrival (its title is generalised from "departure time" to "time for this report"). `final_stop` stays for Departed.
- Example: `{"clientEventId":"3c1e...","eventType":"DelayReported","occurredAt":"2026-10-06T03:05:00Z","reason":"SignalIssue","totalDelayMinutes":10}` returns 201 `{"eventId":"...","journeyVersion":3,"status":"Delivered"}`.

### GET /api/v1/operator/journey (additive)
Each stop gains `isArrivalStation: bool` (the stop's flag, or true for the final stop). Everything else is unchanged.

### GET /api/v1/journeys/{journeyId}/status and GET /api/v1/trains/{trainNumber}/status (additive, public)
- `stops[].actualArrival`: ISO instant or null.
- `lastKnownPosition`: gains `state` (`"Arrived"` or `"Departed"`) and `arrivedAt`; `departedAt` becomes nullable (null while the train is at the station).
- `statusKey`: gains `Completed` (group `Normal`).
- `delayKnown: bool`: false after an operator reported "Unknown" and until the next measured or numeric report. While false, `statusKey` is `Delayed`, the pending stops have no estimated times and `primaryEta` is null (R7).
- No field ever carries the delay reason in this slice.

## 5) Data Contract
- **No migration, no new table.** Checked in the repo: `train_event.event_type` already allows `Arrived` and `DelayReported` (the constraint is built from the `EventType` enum, which already lists all values); `train_event.service_stop_id` is nullable; `train_event.payload` is jsonb; `journey_stop_state.actual_arrival` already exists; `journey.status` already allows `Completed`.
- Writes: `train_event` (insert only), `journey_stop_state` (estimates, `actual_arrival`, `actual_departure`, confirmation), `journey` (`current_delay_minutes`, `status`, `version`).
- Payload of a delay event (jsonb): `{"reason":"SignalIssue","totalDelayMinutes":10}` or `{"reason":"TechnicalFault","delayUnknown":true}`. Arrived and Departed events have no payload. The reason is therefore stored now and published only by a later slice (D-10).
- Constraints that matter: `UNIQUE (journey_id, client_event_id)` unchanged.
- Seed: **the designated stops are already in the data.** `service_stop.arrival_report_required` exists and the seed marks Gampaha (8058, sequence 2). The final stop is an arrival station by rule (R2), so no seed change and no data migration are needed (Q2).

## 6) Business Rules
Slice 1 rules R1 (assignment at occurrence time), R2 (idempotency), R3 (time limits), R8 (journey state), R10 (rebuild from all events), R11 (version conflict retried) apply unchanged to every event type.
1. **Unique occurrence times.** No two accepted events of one journey share an `occurredAt`, unless one is a duplicate of the other (same type, stop and content). A different event at an equal time is 409 `order_conflict`. This keeps slice 1's "no tie-break" rule and makes the rebuild deterministic (FR-36).
2. **Arrival stops (D-08, FR-22).** An arrival is accepted at a stop whose `arrival_report_required` is true, and at the final stop. Otherwise, or at the first stop (no scheduled arrival), it is 422 `not_arrival_station`. A departure never requires an arrival report first: the app asks for it, the server accepts a departure without one.
3. **Order of stop reports.** For one journey, the arrival and departure times, taken in stop order and arrival before departure within a stop, must strictly increase: departure at stop k-1 < arrival at stop k < departure at stop k. A report that breaks this is 409 `order_conflict`. A second, different time for the same stop and kind is 409 `stop_has_different_time`. A later stop reported first marks every earlier stop that has no report Inferred (forward skip, EC-06).
4. **Delay in force.** Fold all accepted events in occurrence order. Departed sets the delay to actual minus scheduled departure, Arrived to actual minus scheduled arrival; both are rounded away from zero and never negative, so an early arrival or departure gives 0 (AC-E04-03 2, EC-35). A numeric DelayReported sets the delay to its total (D-07, EC-37: a total, not added to the delay already running). The delay in force is the result after the last event. A measured report overrides an earlier estimate, because it is later in occurrence order.
5. **ETAs (FR-39, D-05).** Every stop after the last reported one gets its scheduled time plus the delay in force. At the stop the train has arrived at but not left, the estimated departure is its scheduled departure plus the delay, and is never earlier than the scheduled one.
6. **Delay validation (AC-E03-04 6, EC-16).** A numeric total must be greater than the delay in force just before the report's own occurrence time (events earlier in occurrence order). If the previous report was "Unknown", the total must be at least the last numeric delay. Otherwise 409 `delay_not_greater`. Later events are not re-validated by an out-of-order insert; the rebuild simply applies R4. The server accepts totals from 1 to 240 so the app can offer more options than the design shows without a server change (decided, Q6: the app offers +10, +15, +20, +30, +45 and Unknown).
7. **Unknown delay (AC-E03-04 5).** "Unknown" leaves the numeric delay as it was, sets `delayKnown` false, makes `statusKey` `Delayed`, and removes the estimated times of the pending stops and the primary ETA (the same honesty as an overdue train, AC-E04-04 1). The next Departed, Arrived or numeric DelayReported sets `delayKnown` true again.
8. **Status key.** No event at all: `Scheduled`. Otherwise by the delay in force against the tolerance (`OnTimeToleranceMinutes`, 2): `OnTime` or `Delayed`. A DelayReported counts as an event for this, so a delay reported before the first departure shows Delayed. After Completed the key is `Completed`.
9. **Journey status.** The first accepted Departed or Arrived sets `Running`. A DelayReported before any of those leaves the journey `Scheduled`.
10. **Completed (AC-E03-03 4).** Accepting an Arrived at the final stop sets the journey `Completed`, in the same transaction. A Completed journey refuses new reports (409 `journey_not_reportable`, slice 1 R8); duplicates are still answered first.
11. **Visibility after Completed.** The train lookup (slice 1 R15) treats a Completed journey as not in progress, so a finished night train leaves the lookup at arrival and the next day's journey is not hidden by it. The existing step "otherwise today's journey" still returns a Completed journey of today. `Journeys:VisibleAfterEndMinutes` stays at 15 for journeys that are not Completed. **This differs from D8 and CLAUDE.md, which say to set it to 0**: with 0, a train that runs later than its last estimate would vanish from the lookup while still running. This is a decision for the owner (Q4).
12. **The reason is never published** by any read in this slice (FR-42, D-10). A test checks that no response contains the reason.
13. **Missing or late data.** A delay report for a journey with no reported stop is accepted (delay in force before it is 0). A late report is judged by occurrence time (AC-E04-02 4).

## 7) Architecture (follow CLAUDE.md and ADR-15)
- **Domain:** `JourneyProgress` is generalised: the input is the list of accepted events (type, stop, time, delay payload) instead of departures only; `CheckOrder` takes arrivals and departures; a new `DelayInForce` function folds the events (R4, R6). `StopProgress` gains `ActualArrival`. `JourneyStatusKey` gains `Completed`. All of it stays pure and unit-testable.
- **Application:** the feature folder `Features/Events/ReportDeparture` becomes `Features/Events/ReportEvent`: one handler, one validator per event type (shape table in section 4). `RecordedEvent` carries the type, a nullable stop and the payload; `DepartureChange` becomes `ReportChange` (with the arrival time, the new journey status and `delayKnown`). `IJourneyRepository.SaveDepartureAsync` becomes `SaveReportAsync`. The status builder and the operator journey handler get the additive fields. `RejectReason` gains `NotArrivalStation` and `DelayNotGreater`.
- **Infrastructure:** `JourneyRepository` loads every event type (today it filters on Departed with a stop), writes `actual_arrival` and `Completed`, and stores the delay payload as jsonb. The stored `delayKnown` needs a place: derive it from the events at read time (no new column), as the rebuild already reads all events.
- **Api:** `JourneyEndpoints` maps the two new codes in `Describe`; no new route.
- **Telemetry:** the counter `smartrail.events.received` already has the labels `event_type` and `outcome`; it now also receives `Arrived` and `DelayReported`. New counter `smartrail.journeys.completed`. Add one panel to SmartRail Overview (completed journeys, and events by type). Log refused reports with `LogSafe.Clean`, as slice 1 does.
- **Clients:**
  - Operator app (a separate small change to `_specs/05-operator-app.md`, after this slice is merged): send `Arrived` where `isArrivalStation`, enable "Report delay" with reason and the estimate options above the running delay (the status read gives `delayMinutes`): +10, +15, +20, +30, +45 and Unknown (decided 2026-10-15; the hi-fi board `Nimal-DelayEstimate` is redrawn with the two extra options), enable the completed screen, and map the new reason codes. This removes the Non-Goals "Arrival reports", "Report delay" and "Journey completed" from the app spec and closes the FR-20 gap.
  - Passenger find trains (`04-passenger-find-trains.md`, already approved): its `statusKey` list gains `Completed`, and a train may carry `delayKnown: false`. Both are additive; add them to its contract and tests.

## 8) Edge Cases
- Arrival at a stop that is not designated (EC for D-08): 422 `not_arrival_station`; the app does not offer it.
- Arrival before the previous departure, or after this stop's departure: 409 `order_conflict` (R3).
- Arrival repeated with a different time (EC-02): same ID with the same content 200; a different time 409 `stop_has_different_time`.
- Arrival offline and sent late, or arriving out of order (EC-04): accepted if its occurrence time fits (R3); the rebuild gives the same result as in order.
- Early arrival (EC-35): accepted, delay 0, the departure stays scheduled (R4, R5). It raises no exception; only an early departure does (D-48, D-49).
- Delay lower than or equal to the one running (EC-16): 409 `delay_not_greater`. Slice B may later change this to "accept and flag", as EC-16 reads "backend flags unusual values" (Q5).
- Delay "Unknown" (EC-17): `delayKnown` false, no ETAs (R7).
- Total versus extra (EC-37): a +10 total after +8 gives 10 in total, not 18 (R4).
- Time outside the limits (EC-10): 422 `time_out_of_range`, as in slice 1. A queued arrival older than 8 hours is refused after sync.
- Reassigned operator (EC-20, EC-27): judged at occurrence time (R1).
- Arrival at the final stop, then a late report for an earlier stop: 409 `journey_not_reportable`. The app sends in occurrence order, so this should not happen from one phone.
- Journey Unassigned or Completed (EC-15): 409 `journey_not_reportable`.
- Two reports at the same moment: version conflict retried up to 3 times (R11), then 409 `version_conflict`.

## 9) Acceptance Criteria
- [x] AC-E03-03 (1): Arrived at Gampaha 08:40 is recorded with that occurrence time; the status read shows `lastKnownPosition` state Arrived at 08:40. **A1: ReportArrivalTests, and on the real stack.**
- [x] AC-E03-03 (2): an arrival at Ragama (not designated) is 422 `not_arrival_station`; `GET /operator/journey` shows `isArrivalStation` true only for Gampaha and Colombo Fort. **A1: ReportArrivalTests (not_arrival_station at Veyangoda, Ragama and Maradana; isArrivalStation flags).**
- [x] AC-E03-03 (3): an arrival with an earlier occurrence time that arrives later is accepted and the state equals the in-order result. **A1: ReportArrivalTests (a late arrival gives the in-order state).**
- [x] AC-E03-03 (4): Arrived at Colombo Fort sets the journey Completed; `statusKey` is `Completed`; a further report is 409. **A1: unit and integration tests, and on the real stack (Arrived Colombo Fort gave Completed).**
- [x] AC-E03-04 (1): a delay with reason SignalIssue and total 10 is recorded; the status read shows delay 10 and `statusKey` Delayed. **A2: ReportDelayTests (A_delay_with_a_reason_and_a_total...).**
- [x] AC-E03-04 (2) and AC-E04-03 (4): with a delay of 10 reported, Gampaha shows 08:40; a total of 10 after a measured delay of 8 gives 10, not 18. **A2: ReportDelayTests and DelayRulesTests: Gampaha 08:40, and 10 not 18.**
- [x] AC-E03-04 (3): no read returns the reason (a test scans the JSON). **A2: ReportDelayTests scans the status, train, line, search, stations, operator journey and the report response.**
- [x] AC-E03-04 (5): `delayUnknown: true` gives `delayKnown` false, `statusKey` Delayed and no `primaryEta`; a later measured report clears it. **A2: ReportDelayTests: delayKnown false, Delayed, no primaryEta, no estimates; a later measurement or total clears it.**
- [x] AC-E03-04 (6): with 8 running, a total of 8 or 5 is 409 `delay_not_greater`, 10 is accepted; after "Unknown", a total equal to the last numeric delay is accepted. **A2: ReportDelayTests: 8 and 5 are 409, 10 accepted, after Unknown equal accepted and lower refused.**
- [x] AC-E04-01 (1): a repeat of an arrival and of a delay report returns 200 and stores nothing twice; a different ID for the same stop and time is also 200. **A1: arrival repeats by id and by content; the delay part is A2.**
- [x] AC-E04-02 (1) (2): times 2 minutes ahead and 9 hours back are 422 for the new types. **A1: tested for Arrived; DelayReported is A2.**
- [x] AC-E04-03 (2): an arrival at 08:28 against 08:30 gives delay 0 and keeps the departure at 08:31. **A1: ReportArrivalTests and ArrivalRulesTests.**
- [x] A delay report before any departure is accepted, the journey stays Scheduled and `statusKey` is Delayed (R8, R9). **A2: ReportDelayTests and ReportDelayBeforeDepartureTests (the passenger search shows it as Delayed).**
- [x] The train lookup does not return a Completed journey as "in progress"; a finished night train does not hide the next day's journey; a running train later than its last estimate is still returned (R11). **A1: ArrivalRulesTests (JourneySelection) and ReportArrivalTests.**
- [x] The events counter shows `Arrived` and `DelayReported`; `smartrail.journeys.completed` moves; both are visible in Grafana. **A1: Arrived and smartrail.journeys.completed read in Grafana on the Compose stack; DelayReported counted by outcome too (3 recorded, 2 refused) on the Compose stack after the A2 run, 15 Oct.**
  - Live check of A2 on the Compose stack, 15 Oct 2026 (fresh database, real clock): a delay before any departure gave Delayed with the journey still Scheduled; after a measured +134 a total of 139 was accepted and a total of 139 again was 409; "unknown" gave delayKnown false, no ETA and no estimates; a later measurement made it known again; a bad shape was 422; no public read contained a reason; the stored events had a jsonb payload and no stop.
- [ ] `dotnet build -warnaserror` and `dotnet test` pass and CI is green. The slice 1 tests still pass unchanged apart from the renamed handler and the generalised order function.
- [ ] End to end on the emulator after the app change: arrival, delay and completion from the app appear in `GET /api/v1/trains/8058/status`.
- [ ] App (after the app change): with +8 running the estimate screen offers +10, +15, +20, +30, +45 and Unknown; with +25 running it offers +30, +45 and Unknown.

## 10) Test Plan
### 10.1 Seed and fixture data
Journey 8058 (Veyangoda 08:20, Gampaha 08:30/08:31, Ragama 08:42/08:43, Maradana 08:50/08:51, Colombo Fort 08:55), user `nimal` (assigned), `operator2` (never assigned), `jayasinghe` (a controller, 403). Designated: Gampaha, and Colombo Fort by rule. Clock set per test with `FakeTimeProvider`; one fresh database per test (`JourneyApiTestBase`). Expected values:
- Departed Veyangoda 08:28 (+8), then DelayReported total 10 at 08:35: delay 10, Gampaha 08:40, Ragama 08:52, Maradana 09:00, Colombo Fort 09:05.
- Arrived Gampaha 08:40 (+10): Gampaha departure estimate 08:41.
### 10.2 Unit tests
- `DelayInForce`: arrival and departure measurements, numeric estimate, Unknown then measured, early arrival gives 0, rounding at 30 seconds, estimate overridden by a later measurement.
- Order check with arrivals: before the previous departure, equal times, after the stop's departure, forward skip, the same stop with a different time.
- Delay validation: greater, equal, lower, after Unknown (equal accepted), first delay at 0.
- Request shape per event type (every row of the table in section 4), reason codes, total bounds 1 and 240.
- Status key and `delayKnown`, `Completed` mapping.
### 10.3 Integration tests (extend `ReportDepartureTests`, new `ReportArrivalTests`, `ReportDelayTests`, lookup tests)
Happy paths for both types; duplicate by ID and by content; same ID with different content; arrival at a non-designated stop and at the first stop; arrival at the final stop completing the journey and then 409 for a new report; unauthorised (401, controller 403, `operator2` 403 with nothing stored); invalid input; time limits; out-of-order arrival; a delay lower than the running one; Unknown; no reason in any response; the lookup with a Completed night train; version conflict with two simultaneous reports; the operator journey `isArrivalStation` flag. Run against Postgres in a container, as slice 1.

## 11) References
- `_specs/03-operator-report-live-status.md` (slice 1: R1 to R16, section 4 error codes, D8), `_specs/04-passenger-find-trains.md`, `_specs/05-operator-app.md`, ADR-05, ADR-06, ADR-09, ADR-11, CLAUDE.md.
- Existing code reused: `ReportDepartureHandler` (becomes `ReportEventHandler`), `JourneyProgress`, `SnapshotBuilder`, `JourneyRepository`, `JourneyApiTestBase`, `EventMetrics`.

## 12) Open Questions
Decision owner: tharindu.
- **Q1 (the freeze): decided 2026-10-15 by the owner, following the recommendation.** Slice A is allowed past the freeze, built as two pull requests: A1 arrival and Completed (can land on 15 Oct), A2 the delay report (the only part after the freeze). Everything else stays frozen. If A2 is not finished by the owner's cut-off, the delay report stays in the Non-Goals and FR-20 stays recorded as partly met.
- **Q2 (designated stations): decided 2026-10-15 by the owner, following the recommendation.** The flag exists and the seed already marks Gampaha. Recommendation: Gampaha plus the final stop by rule, with no data change. The alternative, also flagging Colombo Fort in the seed, needs a data migration for no gain.
- **Q3 (exceptions): decided 2026-10-15 by the owner, following the recommendation.** Recommendation: leave every exception, including the Critical one for a technical fault, to slice B. Here the reason is stored in the payload so slice B can read it.
- **Q4 (Completed and visibility): decided 2026-10-15 by the owner, following the recommendation.** Completed comes in this slice (AC-E03-03 4 asks for it). Recommendation: Completed ends "in progress" but `VisibleAfterEndMinutes` stays 15, not 0 (R11), because 0 would hide a train that runs past its last estimate. This changes D8 and the CLAUDE.md note; do you agree?
- **Q5 (EC-16): decided 2026-10-15 by the owner, following the recommendation.** The server refuses a numeric delay that is not greater than the delay in force (409 `delay_not_greater`, R6), because exceptions do not exist yet. Slice B may change this to "accept and flag" (EC-16).
- **Q6 (options above +20): decided 2026-10-15 by the owner, narrowed the same day after the first emulator run.** The buttons stop at 60, and 60 reads "1 hour": +10, +15, +20, +30, +45, +1 hour and Unknown. Longer delays are not offered as buttons (a delay already above 1 hour leaves only Unknown). The text below is the first decision, kept for the record. The app adds +30 and +45 to the design's +10, +15, +20 and Unknown. The server needs no change (1 to 240). The hi-fi board `Nimal-DelayEstimate` is redrawn to match.
- **Q7 (Unknown delay): decided 2026-10-15 by the owner, following the recommendation.** An Unknown delay removes the estimated times and the primary ETA and sets `delayKnown` false (R7).
