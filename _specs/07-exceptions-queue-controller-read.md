# Feature Spec: Exceptions queue and controller read

branch: claude/feature/exceptions-queue-controller-read
spec: _specs/07-exceptions-queue-controller-read.md
owner: tharindu
date: 2026-10-15
status: ready, approved 2026-10-15 (owner approved the corrected spec in chat: Q1, Q3, Q4, Q5, Q6, Q7, Q8 and Q9 as recommended). Built as B1 (raise, queue, resolve); B2 (overview) only if B1 is merged by 17 Oct.

---

## 0) Summary
**One-liner:** Raise an exception record whenever a stored operator report needs a controller's attention (early departure, delay with a reason; the operator flag is a later slice), and give the controller two read endpoints: the open-exceptions queue and a network overview of active journeys. Controller can also resolve an exception.
**Why now:** The demo flow needs Jayasinghe to see "8058 · Gampaha · Reason awaiting verification · 3 min" after Nimal reports a delay (slice A). Without this the delay reason has nowhere to go. It is a Must (FR-50, FR-51).
**Who is this for:** Controller (reads, resolves). Operator reports create the records but see nothing new.
**Success looks like:** After Nimal reports a delay with reason Technical fault on 8058, the controller queue returns one Critical exception for it within 5 s; resolving removes it; the passenger status read is unchanged.

Size: two PRs, B1 (raise + queue read + resolve) and B2 (overview read). Feature freeze is today (15 Oct), so B2 is the first thing to drop if time runs short; B1 alone already gives the demo moment.

## 1) Traceability
- Requirements: FR-50, FR-51, FR-43 (resolve is audited), NFR-30 (source label not colour alone). FR-52 verify and FR-53 controller entry are NOT here (see Non-Goals).
- Acceptance criteria: AC-E05-01 (1, 3, 4, 5, 6 read side only, B2), AC-E05-02 (1 to 6), AC-E03-02 (10), AC-E03-04 (7), AC-E04-02 (3 deferred). AC-E03-06 (the flag) moved out of this slice (Q6).
- Edge cases: EC-08 (early departure: accepted, High exception), EC-53 (one report, one exception), EC-54 (nothing non-actionable raises an exception). EC-05 (same station, different time) and EC-07 (backward report) are the conflicts left out (Q1). EC-50 (operator event during a suspension) needs the suspended state, which does not exist. *(The first draft cited EC-50 for "reason awaiting verification" and described EC-53 and EC-54 as ordering and row format; the edge-cases file says otherwise.)*
- Decisions: D-44 (severity, one report one exception, order), D-43 and P-07 (what is not an exception), D-48/D-49 (early departure High; early arrival raises nothing), D-41 (flag), D-10 (reason only after verification), D-76 (senior controller deferred).
- Design: no hi-fi board for the controller yet. This spec is API only; the controller web screen is a later slice.

## 2) Goals and Non-Goals
### Goals
- **Create** the `journey_exception` table (one migration) and map it in EF. *(The first draft said the table already existed. It does not: the real database has 13 tables and this is not one of them. See section 5.)*
- Raise exactly one exception per stored report that qualifies, inside the same transaction as the event.
- `GET` open exceptions ordered Critical first, then oldest first, with journey, station, type, severity, age.
- `POST` resolve with an action text; stores who, when, what.
- B2: `GET` overview of active journeys: last event, delay, next stop, seconds since last update, source label, open-exception count.

### Non-Goals
- Verify or correct a delay reason and publish "Verified by Operations" (AC-E05-03, FR-52). Next slice, since it changes the passenger status.
- Controller enters an event for an operator (FR-53).
- Conflicts that slice 1 rejects with 409 (backward, same station different time, AC-E03-02 5 and 7): nothing is stored today, so there is no record to attach. See Q1.
- Overdue journey detection (needs a background job), operator activity on a suspended service, handover reports (AC-E04-02 3), platform conflicts (FR-57). All need pieces that do not exist yet.
- Ownership/claiming (FR-58, Should), senior controller (D-76), push or live update (the overview is polled; AC-E05-01 2 is met by polling every 10 s in the client for now).
- Hardware, new roles, any change to the five roles.

## 3) User Workflow
### Primary workflow
1. Nimal reports "Delayed 15 min, Technical fault" for 8058 at Gampaha (slice A).
2. The handler stores the event and one Critical `ReasonAwaitingVerification` exception linked to it.
3. Jayasinghe calls the queue; row reads "8058 · Gampaha · Reason awaiting verification · 3 min" with severity Critical.
4. He resolves it with an action ("Acknowledged"). It leaves the queue; audit shows him, the time and the action.
### Secondary
- Nimal reports departure before the scheduled time: accepted as in slice 1 and a High `EarlyDeparture` appears.

## 4) API Surface
All three require the Controller policy (secure by default; a Passenger or Operator token gets 403, no token 401).

**GET /api/v1/exceptions?status=open** *(the first draft said `/api/exceptions`; CLAUDE.md puts every route under `/api/v1/`)*
- Query: `status` (open only in this slice, anything else 422 `invalid_request`).
- Response 200: `{ items: [ { id, journeyId, trainNumber, stationId, stationName (default language), type, severity, createdAt, ageSeconds, relatedEventId } ], openCount }`. Order: Critical then High, then `createdAt` ascending.
- Example row: `{ "trainNumber":"8058","stationName":"Gampaha","type":"ReasonAwaitingVerification","severity":"Critical","ageSeconds":180 }`.
- The client formats the age ("3 min") and the label text; the API returns codes only.

**POST /api/v1/exceptions/{id}/resolve**
- Body: `{ "action": "Acknowledged" }`, one of `Acknowledged`, `NoActionNeeded`, `Escalated` (max 200 chars stored in `resolution`).
- 204 on success. Errors: 404 `exception_not_found`, 409 `already_resolved`, 422 `invalid_request`.

**GET /api/v1/overview** (B2)
- Response 200: `{ openExceptionCount, journeys: [ { journeyId, trainNumber, statusKey, delayMinutes, delayKnown, lastEvent: { type, stationName, occurredAt, source }, nextStop: { stationName, expectedAt }, secondsSinceUpdate, verification } ] }`.
- `source` is one of OperatorReport (only value that exists today), ControllerEntry, Inference, Override. `verification` is `none` or `awaiting` now; `verified` arrives with FR-52.
- Reuses `SnapshotBuilder`; a journey with an override shows its state instead of delay (AC-E05-01 5), none exist yet so the field is future-proofed only.

## 5) Data Contract
- **New table `journey_exception`, created by a new migration** (nothing creates it today). Columns, from `_specs/smartrail-schema.sql` without the three Should-level ones (`owner_user_id` for FR-58, `related_journey_id` and `platform_id` for FR-57): `id`, `journey_id`, `related_event_id`, `type`, `severity` (CHECK Critical or High), `created_at`, `resolved_at`, `resolved_by`, `resolution` (200 characters), plus **`station_id`** (new, nullable, Q8). Written on raise, updated on resolve (`resolved_at`, `resolved_by`, `resolution`).
- **The station of a row cannot come from the related event.** A delay report has no stop (spec 06), so `train_event.service_stop_id` is null for exactly the events that raise most exceptions. The first draft said the station comes from the event's stop; that works only for an early departure. Recommendation (Q8): store `station_id` at raise time, the station of the last reported stop before the event (null when none has been reported).
- The delay reason is read from the related event (slice A stores it on the event); it is not copied.
- **Migration: yes, one.** It creates the table, the partial unique index `(related_event_id, type)` that enforces "one report, one exception" (AC-E05-02 2), and the open-queue index `(severity, created_at) WHERE resolved_at IS NULL`. Privileges need nothing special: the API role gets DML on new tables by default, and this table must allow UPDATE (resolve), unlike `train_event`. *(The first draft said "no migration" in section 2 and "one index" here; both were wrong.)*
- Update `_specs/smartrail-schema.sql` to match (the reference file), since `station_id` is new.
- Seed data: none. Demo exceptions are created by running the demo reports.

## 6) Business Rules
1. Types and severities (D-44, AC-E05-02 1): Critical = `ReasonAwaitingVerification` when reason is TechnicalFault, Obstruction or Weather. High = `ReasonAwaitingVerification` for SignalIssue or Unknown, `EarlyDeparture`, `OperatorCorrectionRequested`.
2. One report, one exception. A delay with a technical fault creates one Critical row, not two (AC-E05-02 2).
3. **Early departure (Q7, decided 2026-10-15).** A departure raises one `EarlyDeparture` (High) when it happened at least 30 seconds before its scheduled time, the same half-minute rounding the delay uses: 08:29:40 against 08:30:00 raises one, 08:29:50 does not. An early arrival raises none (D-48, D-49).
4. Not exceptions (AC-E05-02 5): forward skips, late events in correct order, sync retries (a replayed `clientEventId` returns the first result and raises nothing), a delay report with no reason.
5. The exception and the event are saved in one transaction; if either fails neither exists.
6. Queue order: severity (Critical first), then `created_at` ascending.
7. Resolving writes who, when and the action and removes the row from the open queue (AC-E05-02 6, FR-43). Resolving never changes journey state or what passengers see.
8. **The flag is not in this slice (Q6, decided 2026-10-15).** No endpoint lets an operator flag a report yet, so `OperatorCorrectionRequested` is never raised here; it arrives with the flag slice (AC-E03-06).
9. Time since update and age use server time.

## 7) Architecture (CLAUDE.md, ADR-15)
- Domain: `JourneyException` entity, `ExceptionType`, `ExceptionSeverity`, pure rule `ExceptionPolicy.For(event)` returning zero or one exception.
- Application (feature folder `Exceptions`): `ListOpenExceptionsQuery`, `ResolveExceptionCommand` with validator, `GetOverviewQuery`; `ReportDepartureHandler` and the slice A handlers call `ExceptionPolicy` before save.
- Infrastructure: EF configuration for `journey_exception`, `ExceptionRepository`, index migration.
- Api: `ExceptionEndpoints` mapped with the Controller policy; reason codes added to `Describe`: `exception_not_found`, `already_resolved`.
- Telemetry: counter `smartrail.exceptions.raised` (type, severity), `smartrail.exceptions.resolved`; log fields exceptionId, journeyId, type.
- Clients: controller web screen (later slice). Operator app unchanged.

## 8) Edge Cases
- EC-08 (early departure): High record, the departure is accepted and the journey state is never rolled back. EC-05 and EC-07 (conflicts) and the flag are out of this slice (Q1, Q6).
- EC-50 (reason awaiting verification): Critical or High by reason, one per report.
- EC-53/54 (queue ordering and row format): API returns codes, client formats.
- Replay of the same `clientEventId`: no second exception.
- Two reports from the same journey and stop in quick succession: two events, two exceptions, each linked to its own event.
- Resolve twice: second call 409 `already_resolved`.
- Event saved but exception raise fails: whole request fails and the operator app retries (rule 5).
- Journey completed or ended: its open exceptions stay open until resolved.

## 9) Acceptance Criteria
- [x] A delay report with TechnicalFault, Obstruction or Weather creates one Critical exception (AC-E05-02 1, 2).
- [x] A delay report with SignalIssue or Unknown creates one High exception.
- [x] A departure at least 30 seconds before its scheduled time creates one High `EarlyDeparture`; 29 seconds early, on time and an early arrival create none (Q7).
- [x] A replayed request creates no second exception (AC-E05-02 5).
- [x] Queue order is Critical then High, oldest first (AC-E05-02 3); rows carry journey, station, type, severity, age (AC-E05-02 4).
- [x] Resolve removes the row and stores controller, time, action (AC-E05-02 6).
- [x] Passenger and Operator tokens get 403 on all three endpoints; no token gets 401. (B1: two endpoints, list and resolve; the overview is B2.) Checked by integration tests and live on Compose, 15 Oct.
- [x] Overview (B2) returns last event, delay, next stop, seconds since update, source and open count (AC-E05-01 1, 3, 4). Also the verification state (AC-E05-01 6). Integration tests, 15 Oct. AC-E05-01 2 (updates without refresh) is the client's job: it polls this endpoint; no push is built.
- [x] A dashboard/queue read never changes passenger-visible status. (Test: two overview reads leave the status version, delay and event count unchanged.)

## 10) Test Plan
### 10.1 Seed and fixture data
Journey 8058 (Veyangoda 08:20, Gampaha 08:30/08:31, Ragama 08:42/08:43, Maradana 08:50/08:51, Colombo Fort 08:55); users nimal, operator2, jayasinghe.
### 10.2 Unit tests
`ExceptionPolicy`: each reason maps to the right severity; early departure yes, early arrival no, forward skip no, on-time departure no; exactly one result per event.
### 10.3 Integration tests (Postgres container, base `JourneyApiTestBase`)
- Nimal reports Technical fault at Gampaha: queue (as jayasinghe) shows 1 Critical row, `journeyNumber` 8058, `stationName` Gampaha.
- Replay the same request: still 1 row.
- Signal issue then Technical fault: Critical row sorts before the older High row.
- Resolve: row gone, `resolved_by` set; second resolve 409.
- Nimal and anonymous calls to the queue: 403 and 401.
- Unknown id: 404.

## 11) References
- docs: acceptance criteria E03/E05, discovery 19.4, requirements FR-43, 50, 51, 58.
- Code reused: `ReportDepartureHandler`, `JourneyProgress`, `SnapshotBuilder`, `JourneyRepository`, `EventMetrics`, `JourneyEndpoints.Describe`, Controller policy in `AuthenticationExtensions`.
- Depends on `06-operator-arrival-delay-reports.md` slice A2 for delay reasons. **A2 is merged into `dev` (15 Oct 2026)**, so B1 raises delay exceptions as well as early departures.

## 12) Open Questions (answer before building; recommendation first)
- **Q1. Conflicts (backward, same station different time).** Slice 1 stores nothing on 409, so there is nothing to link. Recommend: leave out of B (stay a documented gap), and say so at the viva; storing rejected events is a design change to slice 1. Alternative: save the rejected report as an event with an outcome flag (bigger change, touches the order rules).
- **Q2. Duplicate guard: answered by a fact, no longer a question.** The table does not exist, so a migration is needed anyway; the partial unique index on `(related_event_id, type)` goes in the same migration at no extra cost.
- **Q3. Resolve actions.** Recommend the fixed three (`Acknowledged`, `NoActionNeeded`, `Escalated`). Alternative: free text, harder to test and audit.
- **Q4. Overview in or out given the freeze.** Recommend B1 now, B2 only if B1 is merged by 17 Oct.
- **Q5. Live update.** Recommend polling (10 s) for the demo; push is not needed for any AC at Must level beyond AC-E05-01 2, which polling approximates. Say so honestly in the report.

### Questions added by the review (2026-10-15)
- **Q6. The flag has no way in.** Section 3, rule 8 and the criteria promise an `OperatorCorrectionRequested` exception when the operator flags their last report (AC-E03-06, FR-27), but nothing lets an operator flag: there is no endpoint, the `ReportFlagged` event type is unused, and spec 06 left it out. Recommend: **out of B1.** B1 raises exceptions for delay reports and early departures, which the API can already produce; the flag arrives as its own small slice (an endpoint and the "Something's wrong with my last report" link), and the criterion about it is moved there. Alternative: build the flag endpoint inside B1 (more code, and the app change on top).
- **Q7. How early is "early" (AC-E03-02 10, D-49).** The criteria say "before the scheduled time" and give 08:27 against 08:30. Recommend the same rounding as the delay (whole minutes, half a minute counts): early when the departure is at least 30 seconds before schedule, so 08:29:40 against 08:30:00 raises one and 08:29:50 does not. Alternative: any second early raises it (a departure 1 second early would fill the queue).
- **Q8. Where the station of a row comes from.** A delay report has no stop. Recommend storing `station_id` on the exception when it is raised (the last reported stop; null if none), one extra column in the migration we have to write anyway. Alternative: work it out when the queue is read from the stop events before the exception, with no extra column, but slower and harder to test.
- **Q9. One report, one exception means repeated delay updates repeat the exception.** If Nimal reports "Technical fault, 10" and then "Technical fault, 15", the queue shows two Critical rows for one problem (D-44 says one per report). Recommend accepting it and saying so in the report; it matches the rule and a controller resolves both. Alternative: link or group by journey and reason (more design, not in the requirements).

### What the review corrected (so it is not lost)
1. **No table.** `journey_exception` is not in the database; a migration is needed (sections 2 and 5).
2. **Routes** moved under `/api/v1/`, and `journeyNumber` renamed `trainNumber` like every other read.
3. **Station source** for delay reports (Q8).
4. **Edge-case citations** (EC-50, EC-53, EC-54) did not match the edge-cases file.
5. **The flag** was promised with no way to raise it (Q6).
6. **Header** pointed at the old file name.
7. Checked and correct: the severity split, the queue order, the row contents and "nothing else raises one" against AC-E05-02 (1) to (6); FR-43, FR-50, FR-51 and NFR-30; the Controller policy exists; the one-transaction rule works with the existing single save.
8. Not in the spec but needed later for the controller screen: AC-E05-01 (7) asks for a permanent notice that SmartRail is an information system and not a safety system on every operations screen.

### Decisions (owner, 2026-10-15; each is the recommendation above)
- **Q1** the backward and same-station conflicts stay out (a documented gap, said at the viva). **Q2** the unique index goes in the new migration. **Q3** three fixed resolve actions: `Acknowledged`, `NoActionNeeded`, `Escalated`. **Q4** B1 now, B2 only if B1 is merged by 17 Oct. **Q5** polling every 10 seconds, not push; said honestly in the report. **Q6** the flag is out of this slice. **Q7** early means at least 30 seconds early. **Q8** `station_id` is stored when the exception is raised. **Q9** one exception per report, so repeated delay updates repeat the exception.
