# Feature Spec: Operator reports a departure, passengers see live status

branch: claude/feature/operator-report-live-status
spec: _specs/operator-report-live-status.md
owner: tharindu
date: 2026-10-12 (updated 2026-10-13: depends on database-foundation)
status: draft (for owner review; items marked ❓ need your decision)

---

## 0) Summary
**One-liner:** An operator reports "Departed <station>" for their assigned journey; the backend records it once, recalculates the delay and ETAs, and a passenger can fetch the journey's live status.
**Why now:** It is the thinnest slice that touches every layer (auth, rules, database, status derivation, a client) and is the core demo flow. Everything else (arrival, delay report, exceptions, SignalR, offline queue) builds on it.
**Who is this for:** Operator (reports), Passenger (reads, no account).
**Success looks like:** AC-E03-02 (1), (2), (4), (6), (8) pass as automated integration tests, and a report shows in the status response within the 30-second target (NFR-04) in the local stack.

## 1) Traceability
- Requirements: FR-03, FR-21, FR-35, FR-36 (accepted cases only), FR-38, FR-39, FR-40, FR-43, FR-44, NFR-04, NFR-10, NFR-17
- Acceptance criteria: AC-E03-02 (1, 2, 4, 6, 8), AC-E04-01 (1), AC-E04-02 (1, 2, 3, 5), AC-E04-03 (1, 3, 5), AC-E01-03 (1, 2, 3), AC-E06-02 (1)
- Edge cases: EC-02, EC-04, EC-06, EC-10, EC-15, EC-20, EC-25, EC-27, EC-132
- Decisions and assumptions: D-35, D-36, D-09, D-05, D-48, ADR-09 (snapshot + version), ADR-08 (Keycloak)
- Design: Operator app report screen; Pax-Results and live-status boards (hi-fi); wireframes Dash-02 (journey detail)

## 2) Goals and Non-Goals
### Goals
- Accept a departure event from an operator assigned at the event's occurrence time.
- Make retries safe (same client event ID recorded once).
- Judge order by occurrence time; accept late, correct-order reports and forward skips (skipped stops recorded as inferred).
- Derive delay and ETAs, and return a status snapshot with a version number.
- Store events append-only; the database refuses UPDATE and DELETE.
### Non-Goals (this slice)
- ❓ Rejected-to-exceptions cases (AC-E03-02 5, 7, 10): slice 2, needs the `journey_exception` table. For now they return a clear error and are not stored as exceptions.
- Arrival and delay reports (FR-22, FR-23), "flag wrong report" (FR-27).
- Offline queue and retry back-off in the Flutter app (FR-24, FR-31): the API supports it (idempotent client event ID), the client comes later.
- ❓ SignalR push (ADR-09): slice 1 uses the REST snapshot only, clients poll. Add the hub in slice 2, keeping the `version` field now so clients can ignore older updates.
- Controller entry of events, overrides, platform changes, reason verification.
- Food ordering and caterer events: designed, not built (D-71, D-72). No outbox row is written in this slice.

## 3) User Workflow
### Primary workflow
1. Nimal (operator, assigned to 8058 on 2026-10-06) taps "Departed Veyangoda" at 08:28.
2. The app sends the event with a client event ID and the occurrence time.
3. The API checks the token, his assignment at 08:28, the time limits, duplicates and order.
4. The event, the updated stop states and the journey (delay, version) are saved in one transaction.
5. Kasuni (passenger, no account) opens train 8058 and sees "Departed Veyangoda" and "Arrives at Gampaha 08:38 (+8 min)".
### Secondary workflows
- Same event sent again: acknowledged, nothing new stored.
- A report occurred 08:28 but arrives after a later one (offline sync): accepted, state equals in-order result.
- A stop skipped: later report accepted, the skipped stop becomes Inferred, no exception.

## 4) API Surface (❓ routes are proposals; confirm against your container diagram)
### POST /api/v1/journeys/{journeyId}/events
- Who: role Operator, assigned to that journey at `occurredAt` (FR-03). Controller role is not allowed here in this slice (EC-25).
- Request: `{ clientEventId: uuid, eventType: "Departed", serviceStopId: uuid, occurredAt: ISO-8601 UTC }`. All required.
- Validation: `occurredAt` not more than 1 minute in the future, not older than 8 hours (FR-38). `serviceStopId` belongs to the journey's service. Departure is not allowed at the final stop.
- Response 201: `{ eventId, journeyVersion, status: "Delivered" }`.
- Response 200 (duplicate, same journey + clientEventId): same body, no second event (AC-E04-01 1).
- Errors (ProblemDetails): 401 no token; 403 wrong role; 403/422 not assigned at that time, logged (AC-E01-03 2); 422 time outside limits; 409 conflicting order, same stop with different time, or early departure (❓ handled in slice 2, see Non-Goals); 404 unknown journey.
### GET /api/v1/journeys/{journeyId}/status
- Who: anyone (FR-05), no token.
- Response 200: `{ journeyId, trainNumber, version, statusGroup, statusKey, delayMinutes, primaryEta: { stationId, time }, lastKnownPosition, expectedNextEventBy, stops: [{ serviceStopId, stationId, scheduled, estimated, actual, confirmation }] }` (AC-E04-03 5).
- Errors: 404 unknown journey.

## 5) Data Contract
Tables used (all exist in `smartrail-schema.sql`): `journey`, `train_service`, `service_stop`, `station`, `platform` (reference only), `operator_assignment`, `journey_stop_state`, `train_event`.
- Writes: `train_event` (insert only), `journey_stop_state` (actual, estimates, confirmation), `journey` (current_delay_minutes, version, expected_next_event_by).
- Constraints that matter: `UNIQUE (journey_id, client_event_id)`; index `(journey_id, occurred_at)`.
- Append-only: the application's database role has INSERT and SELECT only on `train_event`, and a trigger raises on UPDATE or DELETE (AC-E04-02 5, EC-132).
- Concurrency: use the journey `version` (optimistic check) so two reports for one journey cannot both apply on stale state (ADR-09).
### Migration plan (answer to "start with migrations?")
Yes, but one migration for this slice, not the whole schema. See section 7.

## 6) Business Rules
1. R1 (FR-03, AC-E01-03 4): authorised when the operator has an `operator_assignment` row covering `occurredAt` (valid_from <= occurredAt < valid_to or valid_to null). Judged at occurrence time, not arrival time.
2. R2 (FR-35): the pair (journey, clientEventId) is recorded once; a repeat returns the original result.
3. R3 (FR-38): reject if occurredAt > now + 1 minute or < now − 8 hours. Use `TimeProvider`, never `DateTime.Now`.
4. R4 (FR-36): order is by occurrence time of stops. A report for a later stop than the last recorded one marks every stop between them Inferred (forward skip). A report that fits earlier in the order (late delivery) is accepted if its occurrence time is consistent (AC-E03-02 8).
5. R5 (FR-39, D-05, D-48): delay = actual departure − scheduled departure at that stop; each remaining stop's ETA = scheduled + current delay. A departure earlier than scheduled never produces a negative carried delay (clamp to 0).
6. R6 (FR-40): status is On time if |delay| ≤ 2 minutes (configurable), else Delayed.
7. R7 (FR-43): the event row stores source = Operator, created_by = the token subject, occurred_at and received_at.
8. R8: the journey must not be Unassigned (EC-15) or Completed.
- ❓ Missing data: if the journey has no stop state rows, return 500-level problem and log; seeding must prevent this (AC-E02-01).

## 7) Architecture (follow CLAUDE.md and ADR-15)
Feature folders, no cross-feature references:
- `Features/Events/ReportDeparture`: Domain rules (R1–R8) in `Domain/Journeys`; Application: `ReportDepartureCommand`, validator, handler (one transaction); Infrastructure: EF Core repository and the DB trigger migration; Api: endpoint.
- `Features/Status/GetJourneyStatus`: query + handler that builds the snapshot (extends the existing `GetServiceStatus` skeleton, or replaces it).
- Authorisation: JWT from Keycloak (ADR-08); a policy "Operator" plus the assignment check in the handler (not in the endpoint).
- Telemetry: counter `smartrail.events.received` (tags: event_type, outcome = recorded | duplicate | rejected, deployment.environment); histogram `smartrail.events.report_to_visible_seconds` (NFR-04); a span per command; log fields journeyId, clientEventId (never the token).
### Migration and seed (moved to its own spec)
The migration and seed are now specified in `_specs/database-foundation.md` and must be built **before** this slice. The list below is kept for reference and matches that spec.
- Migration `0001_initial` (13 tables): language, line, line_translation, station, station_translation, platform, line_station, train_service, service_stop, journey, operator_assignment, journey_stop_state, train_event, plus the append-only trigger and the revokes on `train_event` (the role `smartrail_app` gets default privileges from the Compose init script). Do NOT include `controller_assignment` (added by the controller-in-charge slice), `order_reference` (D-71), tickets, payments, menu or outbox tables yet.
- Seed, reference layer (every environment): Sinhala/Tamil/English languages; the seeded line with its stations (Veyangoda, Gampaha, Ragama, Maradana, Colombo Fort), platforms, service 8058 with stop times and designated arrival stops, an express service that skips stops (FR-15).
- Seed, demo layer (only where `Seeding:DemoData` is true, which Compose sets and the cluster does not): journey 8058 for the controllable clock's date with status Scheduled, one operator assignment for Nimal covering the whole operating day, stop states Pending. Keep demo seed out of prod.
- Clients depending on this: the Flutter operator app (later), the passenger app and displays (later), the controller dashboard.

## 8) Edge Cases handled in this slice
| EC | Handling |
|----|----------|
| EC-02 | Repeat returns the original result, no error |
| EC-04 | Late, correct-order report accepted; state equals in-order |
| EC-06 | Forward skip: stop Inferred, no exception |
| EC-10 | Time outside −8 h / +1 min rejected with 422 |
| EC-15 | Journey Unassigned: refused |
| EC-20 | Assignment judged at occurrence time |
| EC-25 | Operator calling a controller-only route gets 403 |
| EC-27 | Not assigned to the journey: rejected and logged |
| EC-132 | Database refuses UPDATE/DELETE on train_event |
Deferred to slice 2: EC-05, EC-07, EC-08 (exceptions).

## 9) Acceptance Criteria
- [ ] Departure by the assigned operator at 08:28 is stored with occurredAt 08:28 and returns Delivered (AC-E03-02 1).
- [ ] The status snapshot then shows departed Veyangoda and Gampaha ETA 08:38 (+8) (AC-E03-02 2, AC-E04-03 1).
- [ ] The same event sent twice stores one row and returns the same result (AC-E03-02 4, AC-E04-01 1).
- [ ] A report for Ragama with no Gampaha report accepts Ragama and marks Gampaha Inferred, no exception (AC-E03-02 6).
- [ ] A Veyangoda report that occurred 08:28 arriving after Ragama (08:52) is accepted; state equals in-order (AC-E03-02 8).
- [ ] No token: 401. Operator not assigned at occurredAt: rejected and logged. Operator on a controller-only route: 403 (AC-E01-03 1–3).
- [ ] occurredAt 2 minutes in the future or 9 hours old: rejected (AC-E04-02 1, 2).
- [ ] Application database role cannot UPDATE or DELETE `train_event` (AC-E04-02 5).
- [ ] CI is green and `smartrail.events.received` is visible in Grafana.

## 10) Test Plan
### 10.1 Seed and fixture data
Fixed test clock set to 2026-10-06 08:20 (Tuesday). Service 8058 uses the times in `database-foundation.md` §7 (Veyangoda dep 08:20; Gampaha arr 08:30 dep 08:31; Ragama arr 08:42 dep 08:43; Maradana arr 08:50 dep 08:51; Colombo Fort arr 08:55). The demo seed assigns Nimal for the whole operating day (00:00 to 24:00 Asia/Colombo), so manual testing works at any time; for the assignment-window edge tests (R1) the test creates its own assignment (for example 08:00–10:00) rather than relying on the demo seed. A second operator has no assignment.
### 10.2 Unit tests (Domain/Application, no database)
Assignment window edges (equal to valid_from, equal to valid_to); time limits at exactly +1 min and −8 h; delay and ETA calculation (+8 → each remaining stop +8; early departure clamps to 0); On time at ±2, Delayed at +3; forward skip marks intermediate stops Inferred; out-of-order arrival gives the same final state.
### 10.3 Integration tests (WebApplicationFactory + Postgres in Testcontainers)
Happy path; duplicate; no token; wrong role; not assigned; Unassigned journey; invalid times; late-arrival ordering; the append-only trigger (UPDATE and DELETE fail); status snapshot contents and version increment.

## 11) References
- smartrail-refined-requirements.md, smartrail-acceptance-criteria.md, smartrail-edge-cases.md, smartrail-schema.sql, ADR-08, ADR-09 (in discovery doc), ADR-15, CLAUDE.md.
- Existing code: `Features/Status/GetServiceStatus.cs` skeleton; `TimeProvider` registration in `AddInfrastructure`.

## 12) Open Questions
- ❓ Q1: Poll only in slice 1, SignalR in slice 2? (recommended, keeps the first slice small; NFR-04 is met by polling every 10 s)
- ❓ Q2: Routes and response shape as proposed in section 4, or do you already have names?
- ❓ Q3: Should invalid-order reports (backward, conflicting time) be stored as exceptions already, or wait for slice 2? (recommended: wait)
- Q4 (settled 13 Oct): the 8058 stop times come from `database-foundation.md` §7.
- Decision owner: tharindu, by 13 Oct so the migration can start.
