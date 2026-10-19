# Feature Spec: Operator assignment (assign an operator to a journey, end an assignment, list)

branch: claude/feature/operator-assignment
spec: _specs/17-operator-assignment.md
owner: tharindu
date: 2026-10-19
status: approved 2026-10-19 (owner: scope A1 then A2, A3 not built; Q2 to Q11 as recommended; branch from timetable-web)

---

## 0) Summary
**One-liner:** The controller can **assign an operator** to a journey (FR-12), **replace one mid-journey** as consecutive time-bounded assignments (FR-13), and **end an assignment immediately**, for example for a lost device (FR-56). Today only the demo seeder creates assignments, so a journey made by scheduling (specs 13 to 15) starts Unassigned and nobody can ever report on it.
**Why now:** FR-12, FR-13 and FR-56 are Musts (SEC-12 for FR-56). Spec 13 Q4, spec 15 Q9 and spec 16 Q9 all say "assignment is a separate spec". Without it the demo cannot show a new train becoming usable (AC-E02-01 5) or a lost phone being cut off (AC-E05-07, EC-23). The write side is small: the `operator_assignment` table and the "was he assigned at the time" check in `ReportEvent` already exist.
**Who is this for:** Controller (Jayasinghe) acts. Operator (Nimal) sees the result in the operator app: his journey appears, or "No longer assigned". No new role (D-69, D-74).
**Success looks like:**
- Jayasinghe opens the Unassigned tab, picks train 8072 on Friday, picks Sunil K., presses Assign. Within one poll of the operator app (10 s) Sunil sees the journey and can report on it.
- He ends Nimal's assignment with the reason "Lost or stolen device". The next report from Nimal's phone for that journey is refused, including a late-synced report that claims to have happened earlier. Reports Nimal made before stay in the record.
- A crew change is one action: assign Dilani from the changeover time, and Nimal's assignment ends at that moment. Nobody is ever assigned twice to one journey at the same time.
- Every assign and end records who did it and when. An operator or passenger token gets 403.

Size: **about 2.5 days** (section 12, Q1). Slices in a fixed order: **A1** API and rules, **A2** the two web screens, **A3** the operator directory from Keycloak. Whatever is not built is reported as **designed, not built**.

## 1) Traceability
- Requirements (wording read in `docs/smartrail-refined-requirements.md` v1.13): **FR-12** (a journey without an assigned operator shows Unassigned; operators cannot report on it; the controller can assign an operator or enter events), **FR-13** (assignments are time-bounded, so a crew change is consecutive assignments), **FR-56** (the controller ends an assignment immediately, blocking further reports), FR-03 (an event is judged by assignment), FR-06 (users are managed in the identity provider), NFR-10 (role checks), NFR-17 (audit), NFR-30, NFR-33, NFR-41 (thresholds configurable).
- Acceptance criteria: **AC-E02-01 5** (Unassigned journey: "No journey assigned"; after assignment reporting is available), **AC-E05-07 1 to 3** (end assignment; earlier reports kept; replacement can report from that point), AC-E01-03 4 and AC-E04-02 3 (a report made while assigned and synced later is accepted), AC-E03-01.
- Edge cases: **EC-13, EC-15** (no journey / Unassigned), **EC-20** (crew change), **EC-21, EC-22** (offline report after handover; backdating), **EC-23** (lost device), EC-27.
- Decisions: D-28 (events judged by assignment at occurrence time), D-36 (reports never deleted), D-69 (no new role), D-74 to D-76 (controller in charge and senior controller are separate and stay designed, not built), SEC-12.
- Design: boards added to the Design canvas on 19 October: **Dash-Journeys-Unassigned, Dash-Assign, Dash-EndAssignment**. Dash-Journey already shows the operator under "Crew and control". Operator app: Nimal-NoLongerAssigned.
- Not the same thing: the **controller in charge** (FR-125, `controller_assignment`, boards Dash-HandOver, Dash-ByController, Dash-Reassign) is about controllers and stays designed, not built (D-76). This spec is only about operators.

## 2) Goals and Non-Goals
### Goals
- Assign one operator to one journey for a time range; replace one at a changeover; end one now.
- List journeys that need an operator, and the assignment history of one journey.
- Keep the report check in step: ending an assignment must really stop reports (FR-56), and an assignment must really let reports in (FR-12).
- Tell the operator app the truth within its normal poll.

### Non-Goals
- Controller in charge, hand over, senior controller, by-controller view (FR-125, FR-126, D-76).
- Assigning many journeys at once, a roster or shift pattern, or automatic assignment.
- Notifying the operator by push (the operator app polls; push is not built).
- Un-ending an assignment or editing one in place. A wrong end is fixed by assigning again (rule 7).
- Ending an assignment that has not started yet (Q9).
- Creating, disabling or changing operator accounts: that is the identity provider's console (FR-06).
- Changing who may report, rate limiting (NFR-13 deferred everywhere).

## 3) User Workflow
### Primary: assign
1. Controller opens **Journeys**, tab **Unassigned** (count shown). Each row: train, route, departs, from (weekly service or extra train), **Assign operator**.
2. **Assign** page: choose an operator (radio list: name, "Available", or a warning "On train 8058 until 09:21"), From, To, **Assign operator**.
3. On success the page returns to the list with the message "Train 8072 is assigned to Sunil K." The row leaves the Unassigned tab.

### Secondary
- **Crew change.** Open the journey, **Crew and control**, choose **Assign operator** with From = the changeover time. The previous operator's assignment ends at that moment (rule 4).
- **End assignment** (journey detail, **Crew and control**): dialog with a reason (Lost or stolen device, Crew change, Other), an optional note, **End assignment**. From then the operator app shows "No longer assigned" (rule 9).
- A journey nobody is assigned to is shown "No operator assigned" in the journey list and detail; the controller can still enter events for it (spec 16, F7, FR-53).

## 4) API Surface
Base `/api/v1`, controller policy on every route (operator, passenger and anonymous get 403 or 401). Times are UTC with offset; the clock is the controllable clock (FR-105). Errors are ProblemDetails with a stable `reason` code.

| Method and route | Purpose |
|---|---|
| `GET /assignments/journeys?date=YYYY-MM-DD&filter=unassigned\|all` | Journeys of an operating date with their current operator. Default `date` = today, `filter` = unassigned. |
| `GET /assignments/operators?journeyId=&from=&to=` | Active operators from the directory (A3), each with `available` or the journey they are busy on in that range. |
| `GET /journeys/{id}/assignments` | All assignments of one journey, oldest first, with who and why. |
| `POST /journeys/{id}/assignments` | Assign. Body: `operatorUserId`, `validFrom?`, `validTo?`, `acknowledgeOverlap` (default false). |
| `POST /journeys/{id}/assignments/{assignmentId}/end` | End now. Body: `reason` (`LostDevice`, `CrewChange`, `Other`), `note?` (max 500). |

**`GET /assignments/journeys`** returns `[{ journeyId, trainNumber, route{from,to}, operatingDate, firstDeparture, lastArrival, status, source: "Weekly"|"Extra", operator: { userId, name, validFrom, validTo } | null }]`. `operator` is the assignment covering the current time, or null (shows "No operator assigned"). Unassigned means no assignment now or later today; a journey with only a future assignment is not in this list (Q3 explains the "later" edge).

**`POST /journeys/{id}/assignments`**
- Defaults: `validFrom` = first departure minus the lead time setting (30 minutes proposed, `Assignment:LeadMinutes`, NFR-41), but never earlier than now when the journey is today; `validTo` = null ("until the journey ends").
- 201 with the new assignment, the previous assignment if it was ended (rule 4), and `journeyStatus`.
- 422 `operator_not_found` (not in the directory), `not_an_operator` (account has no operator role), `operator_disabled`, `valid_to_not_after_from`, `valid_from_in_past_beyond_clock` (From more than 5 minutes before now: a past start would make old reports acceptable, EC-22).
- 409 `journey_not_assignable` (Cancelled or Completed), `overlaps_later_assignment` (rule 4), `operator_overlaps_another_journey` (carries `{ journeyId, trainNumber, until }`; repeat with `acknowledgeOverlap: true` to confirm, rule 5).
- 404 `journey_not_found`. 503 `operator_directory_unavailable` (A3).

**`POST /journeys/{id}/assignments/{assignmentId}/end`**
- Sets `valid_to` = now, records `ended_by`, `end_reason`, `end_note`. 200 with the assignment.
- 404 `assignment_not_found`; 409 `already_ended` (valid_to is in the past or equal to now), `not_started` (valid_from is in the future, Q9).

Example: `POST /api/v1/journeys/{id}/assignments` `{ "operatorUserId": "d0d0d0d0-0000-4000-8000-000000000002" }` returns `201 { "assignmentId": "...", "operatorUserId": "...", "name": "Second Operator", "validFrom": "2026-10-09T11:25:00Z", "validTo": null, "journeyStatus": "Scheduled", "ended": null }`.

## 5) Data Contract
- **Table `operator_assignment`** (exists): id, journey_id, operator_user_id (Keycloak subject), valid_from, valid_to (exclusive; null = until the journey ends), assigned_by, `CHECK (valid_to IS NULL OR valid_to > valid_from)`. No operator name is stored: names are read from the directory at display time, so a renamed or removed account never leaves a stale copy (data minimisation).
- **Migration `AddAssignmentEnd`** (new, small): add to `operator_assignment`
  - `ended_by varchar(64) NULL` (the controller who ended it early or replaced it; null when it ran out by itself),
  - `end_reason varchar(20) NULL` (`LostDevice`, `CrewChange`, `Other`),
  - `end_note varchar(500) NULL`,
  - `CHECK (end_reason IS NULL OR ended_by IS NOT NULL)`.
  Existing rows are untouched (all null). Nothing is deleted by any route (D-36).
- **Overlap rule** is enforced in the application inside one transaction that locks the journey row (`SELECT ... FOR UPDATE`), not with a Postgres exclusion constraint, because that needs the `btree_gist` extension and the app role may not be allowed to create it (Q10).
- **Journey status** (`journey.status`, stored): assigning moves `Unassigned` to `Scheduled`. Ending the only assignment of a journey that is still `Scheduled` with no events moves it back to `Unassigned` (FR-12). A `Running` journey keeps `Running`; the list shows "No operator assigned" from the assignments (rule 10).
- **Seed data:** the realm already has two operators (nimal `d0d0d0d0-0000-4000-8000-000000000001`, operator2 `...0002`). The boards show names that do not exist (Sunil K., Dilani R., Kamal S., Ruwan T.). Nothing is added to the realm here; the demo uses Nimal and Second Operator (Q7).

## 6) Business Rules
1. **Controller only** (NFR-10). `assigned_by` and `ended_by` are the token subject, never taken from the body.
2. **Only an enabled account with the operator role can be assigned** (FR-06). The directory is asked at assign time, not trusted from the client.
3. **One operator per journey at any moment.** A journey has at most one assignment covering any given instant.
4. **Crew change (FR-13).** If another operator's assignment covers `validFrom`, it is ended at `validFrom` (`ended_by` = this controller, reason `CrewChange`) in the same transaction, and the new one starts there. If an existing assignment starts at or after `validFrom` the new one is refused `overlaps_later_assignment`; the controller sets a later start or ends the other first.
5. **An operator can be on one journey at a time.** If the chosen operator has another assignment overlapping the range, the call fails with `operator_overlaps_another_journey` and succeeds only with `acknowledgeOverlap: true`. This is advice, like the platform warning in spec 13: the controller may know the first train has finished (Q6). It does not apply to rows that exist already (the demo seed gives Nimal two whole-day assignments).
6. **No past starts.** `validFrom` may be at most 5 minutes before now (clock skew), so assigning cannot retroactively authorise reports (EC-22).
7. **Never edited, never deleted.** A mistake is corrected by ending and assigning again (D-36, NFR-17).
8. **Reports follow the assignment (FR-12, FR-03, D-28).** `ReportEvent` already accepts a report when the operator was assigned at the time it occurred. That stays for CrewChange and Other (a report made offline during the assignment and synced later is accepted, EC-21, AC-E01-03 4).
9. **Lost device is stricter (FR-56, AC-E05-07 1).** For `end_reason = LostDevice`, a report from that operator for that journey is refused when it is **received** at or after `valid_to`, whatever occurrence time it claims. This stops a stolen phone from sending reports dated before the end. The rest of the operator's earlier reports stay (AC-E05-07 2). Needs a small change in `ReportEvent` (Q5).
10. **What the operator and controller screens show.** After an end the operator app gets "No journey assigned / No longer assigned" on its next poll. For the controller, a journey with no covering assignment shows "No operator assigned" and, if it is not yet running, the Unassigned status.
11. **Audit (NFR-17).** The row itself is the record: who assigned (`assigned_by`), who ended and why (`ended_by`, `end_reason`, `end_note`), and the times. Each call also writes a structured log line (journey, operator subject, controller subject, outcome). The history route reads these rows.
12. **Directory unavailable.** The list of operators cannot be built, so Assign is disabled with "Operators could not be loaded. Try again." Ending an assignment still works (it needs no directory).

## 7) Architecture (follow CLAUDE.md and ADR-15)
- **Domain:** `OperatorAssignment` gains `EndedBy`, `EndReason`, `EndNote`; `EndReason` enum; pure rule `AssignmentRules` (covering instant, overlap, default From, lost-device cut-off) with no I/O.
- **Application:** `Features/Assignments/` with `ListJourneys`, `ListOperators`, `ListAssignments`, `AssignOperator`, `EndAssignment`; port `IOperatorDirectory` (`ListOperatorsAsync`, `FindAsync(subject)`); `AssignmentInfo` (already used by `ReportEvent`) gains `EndedAt` and `BlocksLateReports`.
- **Infrastructure:** repository methods inside the existing `JourneyRepository` style (one transaction, journey row locked); `KeycloakOperatorDirectory` (A3) using the admin API with a service client; until A3, a `ConfigOperatorDirectory` reading a seeded list so A1 and A2 can be built and tested without Keycloak changes.
- **Api:** `AssignmentEndpoints` mapped only when a database is configured, controller policy.
- **Telemetry:** counter `smartrail.assignments.assigned` and `.ended` (tag `reason`), log fields `journeyId`, `operatorSubject`, `controllerSubject`; span `assignments.assign`.
- **Clients:** web `features/assignments` (Journeys Unassigned tab, Assign page, End dialog on journey detail); operator app unchanged (it already shows "No journey assigned" and Nimal-NoLongerAssigned).

## 8) Edge Cases
| Case | Handling |
|---|---|
| Assign an operator who is already on another train (EC-20 variant) | Rule 5: warning, confirm to proceed; row shows who and until when |
| Crew change at a station | Rule 4: new assignment from the changeover time ends the old one at the same instant |
| Two controllers assign the same journey at once | Journey row lock; the second call sees the first and gets `overlaps_later_assignment` or ends the first as a crew change, never two at once (rule 3) |
| Lost device, then a late report dated earlier arrives (EC-22, EC-23) | Rule 9: refused for LostDevice |
| Crew change, previous operator's offline report syncs later (EC-21) | Accepted if it occurred during his assignment; a High exception is already raised by the existing handover rule |
| End an assignment twice (double click) | Second call `already_ended`; nothing changes |
| Journey cancelled or completed | `journey_not_assignable` |
| Operator account disabled after assignment | The assignment stays as a record; the token stops working in the identity provider, so reports stop; the list shows the name with "(disabled)" |
| Directory slow or down | Rule 12; ending still works |
| Assign for a journey that has already departed with no operator (events entered by the controller) | Allowed; `validFrom` is at most 5 minutes in the past, so the operator can report from now; earlier reports stay the controller's |

## 9) Acceptance Criteria
- [ ] A controller can assign an enabled operator to an Unassigned journey; the journey becomes Scheduled and the operator can report on it (AC-E02-01 5, FR-12).
- [ ] A journey with no assignment shows "No operator assigned" (FR-12); an operator cannot report on an Unassigned journey (EC-15).
- [ ] A crew change is one call; the previous assignment ends at the changeover and there is never more than one operator at an instant (FR-13, EC-20, AC-E05-07 3).
- [ ] Ending an assignment stops reports from that moment; for LostDevice, also reports claiming an earlier time but received later (FR-56, AC-E05-07 1, EC-23).
- [ ] Reports made before the end are kept (AC-E05-07 2, D-36).
- [ ] An operator or passenger token cannot call any route (403); anonymous gets 401 (NFR-10).
- [ ] Every assignment records who assigned it; every early end records who, why and when (NFR-17).
- [ ] Assigning a non-operator, disabled or unknown account is refused with a reason code and changes nothing.
- [ ] The web Unassigned tab, Assign page and End dialog work by keyboard, targets 44 px, status not by colour alone (NFR-30, NFR-33). *(Not yet checked by hand.)*
- [ ] The operator app shows the new journey, or "No longer assigned", on its next poll.

## 10) Test Plan
### 10.1 Seed and fixture data
Two operators (Nimal, Second Operator) and the controller from the realm export; the journeys generated by spec 13 tests (Unassigned); the demo journey 8058 with Nimal's whole-day assignment; a journey with events; a Cancelled and a Completed journey.
### 10.2 Unit tests (`AssignmentRules`)
Covering instant (ValidTo exclusive, null open end); overlap detection with open ends; default From (lead time, not before now); crew change ends the earlier one at the boundary; lost-device cut-off by received time; end of an assignment that is not started or already ended.
### 10.3 Integration tests (Postgres container)
Assign happy path (status change, operator can report, 201 body); each 422 and 409 reason; acknowledgeOverlap; crew change; two concurrent assigns; end then report refused (LostDevice with an earlier `occurredAt`); end with CrewChange then late report accepted; ending the last assignment of an unreported journey returns it to Unassigned; operator/passenger/anonymous matrix; audit columns filled; migration is additive and passes on the existing seed.
### 10.4 Web and E2E
Assign flow from the Unassigned tab, the overlap confirm, the end dialog, empty list and load error; one Playwright flow: assign, operator reports, end (lost device), report refused.

## 11) References
- ADR-09 (journey version), ADR-15, D-28, D-36, D-69, D-74 to D-76, SEC-12. `smartrail-schema.sql` (`operator_assignment`). Existing code: `OperatorAssignment`, `EventRules.IsAssignedAt`, `ReportEvent` (R1 check), `JourneyRepository.GetAssignedAsync`, `DemoSeeder`, `docker/keycloak/realm-smartrail.json`.
- Specs 12 (operator history, assignment at occurrence time), 13 Q4, 15 Q9, 16 Q9 (all point here).

## 12) Open Questions (recommendation first)
- **Q1. Scope.** About 2.5 days: A1 1.25, A2 1.0, A3 0.25 to 0.5. Recommend building **A1** first (it makes the operator flow demonstrable through the API and tests) with the config-based directory, then A2. Report A3 as designed, not built if time runs out. Tell me the real budget and I will cut it.
- **Q2. Where operator names come from.** SmartRail stores only the Keycloak subject. To show names and to refuse non-operators it must ask the identity provider. Recommend an `IOperatorDirectory` port with a Keycloak admin API adapter (A3: a service client with `view-users`, `GET /admin/realms/smartrail/roles/operator/users`) and a config-based list for A1 and tests. Alternative: a small `operator_directory` table seeded from the realm (simpler, but names can drift from FR-06's source of truth). Your call.
- **Q3. Default From and the "later today" edge.** The board says an assignment "begins at the journey's first departure". If it begins then, the operator app shows "No journey assigned" until departure, so the operator cannot open the journey early. Recommend From defaults to **30 minutes before first departure** (a setting, NFR-41) and To empty ("until the journey ends"); the board copy changes (Q7). Alternative: first departure exactly, as drawn.
- **Q4. Crew change auto-ends the previous assignment.** Recommend yes (rule 4): one action, no gap or overlap, audited with reason CrewChange. Alternative: the controller must end the first manually, then assign (two steps, a gap in which nobody can report).
- **Q5. Lost device blocks late reports by received time.** FR-56 and AC-E05-07 1 say "every further event is rejected immediately"; AC-E01-03 4 and EC-21 say a report made during the assignment and synced later is accepted. Both can hold if only LostDevice cuts by received time (rule 9). That is a small change to `ReportEvent` and a new reject reason (`AssignmentEnded`). Recommend yes. Alternative: no change; a stolen phone could still send reports dated earlier than the end, which is weak for a Must.
- **Q6. Overlap on another journey: warn or refuse.** Recommend warn and confirm (rule 5), because the controller may know the other train has finished early and a hard block would stop a real crew change. Alternative: refuse outright (safer, less flexible).
- **Q7. Board changes.** The boards I drew need three edits: (1) "Signed in on 1 device" is not available (SmartRail does not know sign-in state); recommend deleting that line; (2) the sample operators Sunil, Dilani and Ruwan do not exist in the realm: either add them to the realm export for the demo or relabel the sample to Nimal and Second Operator; (3) the End dialog says "Afterwards the journey shows Unassigned", which is only true before departure: change to "Nobody can report on this journey until you assign another operator." The From/To fields also need a "Until the journey ends" option (Q3).
- **Q8. New acceptance criteria and edge cases.** AC-E02-01 5, AC-E05-07 and EC-13 to EC-23 already cover most of this. Rules 4, 5, 6 and 9 and the status change are new. Recommend adding them as dated 19 October entries, as for specs 13 and 14. That changes your baseline, so your call.
- **Q9. Ending an assignment that has not started.** The `valid_to > valid_from` constraint forbids it. A wrong future assignment then cannot be undone here. Recommend accepting the limit for now and saying so in the report; fix later with a `withdrawn_at` column. Alternative: add that column now (small, but one more state to explain on screen).
- **Q10. Overlap enforced in code, not in the database.** Recommend the transaction with a journey row lock (rule 3). Alternative: a Postgres exclusion constraint on `(journey_id, tstzrange(valid_from, valid_to))`, which is the stronger guarantee but needs `btree_gist` and a role that may create extensions.
- **Q11. Wording elsewhere.** Specs 15 and 16 say assignment is "seed data only" until this spec exists. Recommend leaving them as written (they were true on the day) and updating the report and the "not built" list when this lands.

## 13) Owner decisions of 19 Oct 2026
- Scope: **A1** (API, rules, migration, tests, config-based operator list) then **A2** (web: Unassigned tab, Assign page, End dialog). **A3** (Keycloak admin directory) is reported as designed, not built.
- Q2 to Q11 as recommended. Branch `claude/feature/operator-assignment` starts from `claude/feature/timetable-web` (which has the web menu), so its pull request merges after spec 15.

## 14) Delivery notes for A1 (19 Oct 2026)
- **Built (backend):** `GET /api/v1/assignments/journeys`, `GET /assignments/operators`, `GET /journeys/{id}/assignments`, `POST /journeys/{id}/assignments`, `POST /journeys/{id}/assignments/{assignmentId}/end`; migration `AddAssignmentEnd` (`ended_by`, `end_reason`, `end_note` with their checks); `AssignmentRules` (pure); the crew change that ends the covering assignment at the new start; the advice for an operator on another journey (`acknowledgeOverlap`); the lost-device rule in `ReportEvent` (new reason `assignment_ended`, 403); an operator list from configuration (`Operators:Directory`, the two demo operators) behind the `IOperatorDirectory` port; counters `smartrail.assignments.assigned` and `.ended`. 14 new unit tests and 21 new integration tests pass; the whole suite passes.
- **Deviation from section 5 (stated, not hidden):** ending the only assignment of a Scheduled journey does **not** set the stored status back to Unassigned. A reverted status would make the server refuse a report the operator made during the assignment and syncs later (EC-21, AC-E01-03 4), because an Unassigned journey takes no reports (spec 03). "No operator assigned" and the Unassigned list are derived from the assignments, which is what the controller sees. Assigning still moves Unassigned to Scheduled.
- **Other small decisions:** ending an assignment in the very instant it started ends it one microsecond later (the window check needs `valid_to > valid_from`); a Running journey keeps Running.
- **Not built:** A3 (the Keycloak admin-API directory: names come from configuration), ending a not-started assignment (Q9), a roster.
