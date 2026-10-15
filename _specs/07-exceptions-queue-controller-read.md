# Feature Spec: Exceptions queue and controller read

branch: claude/feature/exceptions-queue-controller-read
spec: _specs/exceptions-queue-controller-read.md
owner: tharindu
date: 2026-10-15
status: draft

---

## 0) Summary
**One-liner:** Raise an exception record whenever a stored operator report needs a controller's attention (early departure, delay with a reason, operator flag), and give the controller two read endpoints: the open-exceptions queue and a network overview of active journeys. Controller can also resolve an exception.
**Why now:** The demo flow needs Jayasinghe to see "8058 · Gampaha · Reason awaiting verification · 3 min" after Nimal reports a delay (slice A). Without this the delay reason has nowhere to go. It is a Must (FR-50, FR-51).
**Who is this for:** Controller (reads, resolves). Operator reports create the records but see nothing new.
**Success looks like:** After Nimal reports a delay with reason Technical fault on 8058, the controller queue returns one Critical exception for it within 5 s; resolving removes it; the passenger status read is unchanged.

Size: two PRs, B1 (raise + queue read + resolve) and B2 (overview read). Feature freeze is today (15 Oct), so B2 is the first thing to drop if time runs short; B1 alone already gives the demo moment.

## 1) Traceability
- Requirements: FR-50, FR-51, FR-43 (resolve is audited), FR-27 (flag), NFR-30 (source label not colour alone). FR-52 verify and FR-53 controller entry are NOT here (see Non-Goals).
- Acceptance criteria: AC-E05-01 (1, 3, 4, 5, 6 read side only), AC-E05-02 (1 to 6), AC-E03-02 (10), AC-E03-04 (7), AC-E03-06 (1), AC-E04-02 (3 deferred).
- Edge cases: EC-05, EC-07, EC-08, EC-50, EC-53, EC-54.
- Decisions: D-44 (severity, one report one exception, order), D-43 and P-07 (what is not an exception), D-48/D-49 (early departure High; early arrival raises nothing), D-41 (flag), D-10 (reason only after verification), D-76 (senior controller deferred).
- Design: no hi-fi board for the controller yet. This spec is API only; the controller web screen is a later slice.

## 2) Goals and Non-Goals
### Goals
- Map `journey_exception` in EF (no migration, the table and open-queue index already exist).
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
- Nimal flags his last report: a High `OperatorCorrectionRequested` exception appears; journey state unchanged.
- Nimal reports departure before the scheduled time: accepted as in slice 1 and a High `EarlyDeparture` appears.

## 4) API Surface
All three require the Controller policy (secure by default; a Passenger or Operator token gets 403, no token 401).

**GET /api/exceptions?status=open**
- Query: `status` (open only in this slice, anything else 422 `invalid_request`).
- Response 200: `{ items: [ { id, journeyId, journeyNumber, stationId, stationName (default language), type, severity, createdAt, ageSeconds, relatedEventId } ], openCount }`. Order: Critical then High, then `createdAt` ascending.
- Example row: `{ "journeyNumber":"8058","stationName":"Gampaha","type":"ReasonAwaitingVerification","severity":"Critical","ageSeconds":180 }`.
- The client formats the age ("3 min") and the label text; the API returns codes only.

**POST /api/exceptions/{id}/resolve**
- Body: `{ "action": "Acknowledged" }`, one of `Acknowledged`, `NoActionNeeded`, `Escalated` (max 200 chars stored in `resolution`).
- 204 on success. Errors: 404 `exception_not_found`, 409 `already_resolved`, 422 `invalid_request`.

**GET /api/overview** (B2)
- Response 200: `{ openExceptionCount, journeys: [ { journeyId, journeyNumber, statusKey, delayMinutes, delayKnown, lastEvent: { type, stationName, occurredAt, source }, nextStop: { stationName, expectedAt }, secondsSinceUpdate, verification } ] }`.
- `source` is one of OperatorReport (only value that exists today), ControllerEntry, Inference, Override. `verification` is `none` or `awaiting` now; `verified` arrives with FR-52.
- Reuses `SnapshotBuilder`; a journey with an override shows its state instead of delay (AC-E05-01 5), none exist yet so the field is future-proofed only.

## 5) Data Contract
- Table `journey_exception`: written on raise (`journey_id`, `related_event_id`, `type`, `severity`, `created_at`), updated on resolve (`resolved_at`, `resolved_by`, `resolution`). Other columns stay null.
- Station for a row comes from the related event's stop, not a new column.
- The delay reason is read from the related event (slice A stores it on the event); it is not copied.
- New migration: no. The unique rule "one report one exception" is enforced by a partial unique index on `(related_event_id, type)`; **this index is the only schema change and needs a small migration**, so the answer to "migration needed" is yes, one index only (see Q2).
- Seed data: none. Demo exceptions are created by running the demo reports.

## 6) Business Rules
1. Types and severities (D-44, AC-E05-02 1): Critical = `ReasonAwaitingVerification` when reason is TechnicalFault, Obstruction or Weather. High = `ReasonAwaitingVerification` for SignalIssue or Unknown, `EarlyDeparture`, `OperatorCorrectionRequested`.
2. One report, one exception. A delay with a technical fault creates one Critical row, not two (AC-E05-02 2).
3. An early departure that also carries nothing else creates one `EarlyDeparture`; an early arrival creates none (D-48, D-49).
4. Not exceptions (AC-E05-02 5): forward skips, late events in correct order, sync retries (a replayed `clientEventId` returns the first result and raises nothing), a delay report with no reason.
5. The exception and the event are saved in one transaction; if either fails neither exists.
6. Queue order: severity (Critical first), then `created_at` ascending.
7. Resolving writes who, when and the action and removes the row from the open queue (AC-E05-02 6, FR-43). Resolving never changes journey state or what passengers see.
8. A flag creates `OperatorCorrectionRequested` linked to the flagged report; if the flag is queued offline it is created when it arrives (AC-E03-06 3).
9. Time since update and age use server time.

## 7) Architecture (CLAUDE.md, ADR-15)
- Domain: `JourneyException` entity, `ExceptionType`, `ExceptionSeverity`, pure rule `ExceptionPolicy.For(event)` returning zero or one exception.
- Application (feature folder `Exceptions`): `ListOpenExceptionsQuery`, `ResolveExceptionCommand` with validator, `GetOverviewQuery`; `ReportDepartureHandler` and the slice A handlers call `ExceptionPolicy` before save.
- Infrastructure: EF configuration for `journey_exception`, `ExceptionRepository`, index migration.
- Api: `ExceptionEndpoints` mapped with the Controller policy; reason codes added to `Describe`: `exception_not_found`, `already_resolved`.
- Telemetry: counter `smartrail.exceptions.raised` (type, severity), `smartrail.exceptions.resolved`; log fields exceptionId, journeyId, type.
- Clients: controller web screen (later slice). Operator app unchanged.

## 8) Edge Cases
- EC-05/07/08 (wrong tap, flag, early departure): flag and early departure create High records, state never rolled back (D-41).
- EC-50 (reason awaiting verification): Critical or High by reason, one per report.
- EC-53/54 (queue ordering and row format): API returns codes, client formats.
- Replay of the same `clientEventId`: no second exception.
- Two reports from the same journey and stop in quick succession: two events, two exceptions, each linked to its own event.
- Resolve twice: second call 409 `already_resolved`.
- Event saved but exception raise fails: whole request fails and the operator app retries (rule 5).
- Journey completed or ended: its open exceptions stay open until resolved.

## 9) Acceptance Criteria
- [ ] A delay report with TechnicalFault, Obstruction or Weather creates one Critical exception (AC-E05-02 1, 2).
- [ ] A delay report with SignalIssue or Unknown creates one High exception.
- [ ] A departure before the scheduled time creates one High `EarlyDeparture`; an early arrival creates none.
- [ ] A flag creates one High `OperatorCorrectionRequested` linked to that report and journey state is unchanged (AC-E03-06 1).
- [ ] A replayed request creates no second exception (AC-E05-02 5).
- [ ] Queue order is Critical then High, oldest first (AC-E05-02 3); rows carry journey, station, type, severity, age (AC-E05-02 4).
- [ ] Resolve removes the row and stores controller, time, action (AC-E05-02 6).
- [ ] Passenger and Operator tokens get 403 on all three endpoints; no token gets 401.
- [ ] Overview (B2) returns last event, delay, next stop, seconds since update, source and open count (AC-E05-01 1, 3, 4).
- [ ] A dashboard/queue read never changes passenger-visible status.

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
- Depends on `operator-arrival-delay-reports.md` (A2) for delay reasons; B1 can ship with early departure and flag first if A2 is late.

## 12) Open Questions (answer before building; recommendation first)
- **Q1. Conflicts (backward, same station different time).** Slice 1 stores nothing on 409, so there is nothing to link. Recommend: leave out of B (stay a documented gap), and say so at the viva; storing rejected events is a design change to slice 1. Alternative: save the rejected report as an event with an outcome flag (bigger change, touches the order rules).
- **Q2. Duplicate guard.** Recommend the partial unique index on `(related_event_id, type)`, one small migration. Alternative: rely on the transaction and handler logic only, no migration, slightly weaker.
- **Q3. Resolve actions.** Recommend the fixed three (`Acknowledged`, `NoActionNeeded`, `Escalated`). Alternative: free text, harder to test and audit.
- **Q4. Overview in or out given the freeze.** Recommend B1 now, B2 only if B1 is merged by 17 Oct.
- **Q5. Live update.** Recommend polling (10 s) for the demo; push is not needed for any AC at Must level beyond AC-E05-01 2, which polling approximates. Say so honestly in the report.
