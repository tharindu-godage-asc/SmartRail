# Feature Spec: Scheduling, cancel and amend (S3 and S4 of spec 13)

branch: claude/feature/scheduling-cancel-amend
spec: _specs/14-scheduling-cancel-amend.md
owner: tharindu
date: 2026-10-19
status: approved 2026-10-19 (owner: Q1 to Q8 as recommended; branch from dev, which has specs 10 to 13)

---

## 0) Summary
**One-liner:** Finish the two slices of spec 13 that were left out. **S3 cancel:** a controller cancels a service for one date or from a date onward with a reason; the service and its history stay; journeys in the range that have no events become `Cancelled`; the generator never creates journeys on cancelled dates. **S4 amend:** a controller changes a weekly service from a chosen date; the change is saved as a new version, the old version stays, journeys before the date keep the old times.
**Why now:** FR-11B and FR-11C are Musts and are the two items spec 13 section 14 lists as not built. The Cancelled tab of `GET /timetable/services` is empty today, the check constraint of `service_change_log` already allows `Amended` and `Cancelled`, and `train_service` already has `version`, `effective_to` and `superseded_service_id`, so most of the data work was done in S1/S2.
**Who is this for:** Controller (Jayasinghe). Passengers and operators feel the result (a cancelled train disappears, an amended train shows new times). No new role (D-69).
**Success looks like:**
- Jayasinghe cancels train 8072 for Friday 23 October with the reason "Rolling stock not available". The 23 October journey is `Cancelled`, 22 and 26 October are untouched, the passenger search for 23 October no longer lists 8072, and the Cancelled tab lists it with the reason.
- He cancels 8075 "from 26 October onward". No journey of 8075 exists from that date, and the generator does not create any on later runs.
- He amends 8058 so Gampaha departs 08:42 (was 08:38) from Monday 26 October. Version 2 is current, version 1 is kept, 19 to 25 October journeys keep 08:38 and 26 October onward show 08:42.
- Cancelling or amending a date whose journey already has events is refused with 409 and nothing changes.
- A controller token is required; operator and passenger tokens get 403.

Size: **about 1.25 days**, backend only (S3 0.5, S4 0.75). Web screens are not in this spec (Q1).

## 1) Traceability
- Requirements: FR-11B (amend creates a version, passengers see the latest, earlier versions in the audit trail, affected passengers notified: **notification not built**, sending to the caterer designed not built), FR-11C (cancel one date or from a date onward with a reason, never deleted, notifies passengers, starts refunds, caterer later: **notification, refunds and caterer not built**), FR-11 (journeys), FR-11D (validation reused), D-69, D-70, D-65, D-72, FR-54 (override, the tool for a running journey, not built), NFR-10, NFR-17.
- Acceptance criteria: AC-E02-01 (generator never creates a cancelled date). The proposed AC-E02-03 onward in spec 13 section 9 for cancel and amend are made concrete in section 9 here.
- Builds on: spec 13 sections 4 to 6 (routes, rules 1 to 14), section 13 (owner decisions: Q2 a row per version, Q5 absent for now, Q10 earliest amend date is tomorrow) and section 14 (what exists).
- Design: `Dash-ServiceDetail` (Versions panel, Amend service, Cancel service) and `Dash-Timetable` (Cancelled tab) are drawn; the cancel dialog and the amend entry are not drawn. No screen is built here.

## 2) Goals and Non-Goals
### Goals
- `POST /api/v1/timetable/services/{serviceId}/cancel` and `POST /api/v1/timetable/services/{serviceId}/amend`, controller only.
- Migration `AddCancellation`: table `service_cancellation`, and `Cancelled` in the journey status check and the `JourneyStatus` enum.
- The Cancelled tab returns real rows; the service detail lists cancellations and shows the version list (summary text of what changed).
- `ServiceRuns` and the generator know cancellations; the passenger reads skip cancelled dates and `Cancelled` journeys.
- Audit rows `Cancelled` and `Amended` in `service_change_log` in the same transaction as the change.
### Non-Goals
- Controller web screens (Timetable list, wizard, detail, cancel dialog): a later spec (Q1).
- **Telling passengers** (push is not built), **refunds** (ticketing not built), **messages to the caterer** (D-72). Passengers see the new times or the missing train on their next refresh. Stated, not hidden.
- Un-cancelling. Cancelling a running or finished journey (an override, FR-54). Operator assignment (FR-12), so an amended or cancelled journey that had an operator assignment keeps the row as it is.
- Showing a cancelled train as "Cancelled" to passengers (Q4, a later step).
- Amending an extra train (cancel it and create another, Q3). Changing train number, kind or line in an amendment. Services that pass midnight (still refused, spec 13 section 13).

## 3) User Workflow
### Cancel
1. Jayasinghe opens a service (detail) and chooses **Cancel service**.
2. He picks "this date" (one date) or "from this date onward", a date (today or later), and types a reason (1 to 200 characters).
3. Confirm. The server cancels the journeys in the range that have no events and records the cancellation. The service moves to the Cancelled tab for those dates.
4. If a journey in the range already has events, nothing is cancelled and he is told which date is the problem.
### Amend
1. He chooses **Amend service**; the form is filled with the current version, plus an **effective from** date (tomorrow or later).
2. He changes days, stops, times or platforms, reviews errors and warnings (the same validation as create), and presses Schedule.
3. Version 2 becomes current from that date; version 1 stays in the Versions list with its dates and who made each.
### Errors
Unknown service 404; date in the past 422; a rule of FR-11D fails 422 with the field; journey with events 409; train number or overlap 409; warnings not acknowledged 409.

## 4) API Surface
Both routes: policy `Controller` (the existing group). No token 401, other roles 403. ProblemDetails with a stable `code`.

### 4.1 `POST /api/v1/timetable/services/{serviceId}/cancel`
```json
{ "from": "2026-10-23", "to": "2026-10-23", "reason": "Rolling stock not available" }
```
- `to` is null for "from this date onward". `from` is today or later (Asia/Colombo, controllable clock); `to` is not before `from`. `reason` is trimmed, 1 to 200 characters.
- The service may be any version; the cancellation applies to the **train number on the line** for those dates, whatever version runs (Q2). An extra train is cancelled the same way.
- Dates in the range on which the service does not run are skipped without error (a Sunday for a weekday train). If no date in the range has a service, `422 nothing_to_cancel`.
- Journeys of the range with status Unassigned or Scheduled and **no** `train_event` become `Cancelled`. If any journey of the range has a train event or status Running or Completed, `409 journey_has_events` with `{ "date": "2026-10-23" }` and **nothing is changed** (all or none).
- Response `200`: `{ "cancellationId": "...", "cancelledJourneys": 1, "from": "...", "to": null }`. Repeating an identical cancel returns `200` with `cancelledJourneys: 0` and writes no second row (same train number, line, from, to).
### 4.2 `POST /api/v1/timetable/services/{serviceId}/amend`
Body as create (spec 13 section 4.5) for a **weekly** service, with `effectiveFrom` required; `trainNumber`, `kind`, `lineCode` must equal the current ones.
- `{serviceId}` may be any version; the amendment applies to the **latest** version of that train number (the one with no later version).
- `effectiveFrom` is tomorrow or later and **after** the latest version's `effectiveFrom` (`422 effective_date_not_after_current`).
- Same validation and platform advice as create; the platform advice and the train-number overlap check ignore the service's own earlier versions.
- Effect, in one transaction under the train-number advisory lock: insert version N+1 with its stops, `superseded_service_id` = the latest version, `effective_from` = the date, `effective_to` as the old row had it; set the old version's `effective_to` to the day before; re-point the not-yet-started journeys (rule 4); write the `Amended` log row with the changed stops.
- `409 journey_has_events` if any journey of the train number on or after `effectiveFrom` has events (nothing changes). `409 warnings_not_acknowledged` as in create. `422 amend_extra_not_supported` for an extra train. `422 service_ended` if the latest version has an `effective_to` in the past.
- Response `201`: `{ "serviceId": "<new version>", "version": 2, "journeysUpdated": 3, "warnings": [] }`.
### 4.3 Reads that change
- `GET /timetable/services?tab=cancelled`: one row per cancellation that covers a date today or later: `trainNumber`, route, `from`, `to`, `reason`, `cancelledBy`, `cancelledAt`, `serviceId`.
- `GET /timetable/services/{id}`: gains `cancellations[]` (same fields) and each `versions[]` entry gains `summary` (for example "Gampaha 08:42 (was 08:38)"), built from the `Amended` log row.
- Weekly tab: a service whose every future date is cancelled still shows under Weekly with a `cancelledRanges` list (it is also under Cancelled).

## 5) Data Contract
**Migration `AddCancellation`** (EF Core, snake_case; default privileges give the app role DML on new tables):
- `service_cancellation`: `id uuid pk`, `line_id` fk, `train_number varchar(10)`, `service_id uuid fk` (the version the controller was looking at, for the audit), `from_date date`, `to_date date null`, `reason varchar(200)`, `created_by varchar(64)`, `created_at timestamptz`; check `to_date is null or to_date >= from_date`; index `(line_id, train_number, from_date)`; unique `(line_id, train_number, from_date, to_date)` is not used (null `to_date`), repeat detection is in the handler.
- `ck_journey_status` gains `Cancelled`; `JourneyStatus` gains `Cancelled` (last value, so stored text for old rows is unchanged). Every `switch` over `JourneyStatus` is checked in the build (Q5 of the test plan lists the files to grep).
- `service_change_log` is unchanged: its check already allows `Amended` and `Cancelled`; `details jsonb` holds, for an amendment, `{ "fromVersion":1, "toVersion":2, "effectiveFrom":"...", "changes":[...] }` and for a cancel `{ "from":..., "to":..., "reason":... , "journeys":n }`.
- No column changes on `train_service`.
- `ServiceRuns.On(...)` gets an overload taking the cancellations for the train number; the old signature stays for callers that do not know them (the seeder). One implementation of "does it run" is kept (spec 13 rule 9).

## 6) Business Rules
1. **Controller only (D-69, NFR-10).** The actor in the log is the token subject.
2. **Never deleted (D-70).** Cancel adds a row and changes journey status; amend adds a service row. No route deletes a service, a version, a stop or a cancellation. Journeys are never deleted.
3. **Cancel is all or none.** One transaction: check events for the whole range first, then write the cancellation, the journey statuses and the log row. Any failure leaves nothing.
4. **Amend re-points, it does not duplicate.** There is a unique `(service_id, operating_date)` per version, so generating from version 2 would create a second journey for the same train and date. For each journey of the train number with `operating_date >= effectiveFrom`, no events and status Unassigned or Scheduled, the journey keeps its id, its assignment and its date, gets `service_id` = the new version, and its stop states are rebuilt (all Pending) from the new stops; its `version` counter increases. Journeys that the new days do not cover (a day removed) become `Cancelled` with the reason "Removed by amendment" and are listed in the log; dates the new days add are created by the generator right after (the same call).
5. **Events block change.** A journey with any `train_event`, or status Running or Completed, on or after the date: `409 journey_has_events` for both cancel and amend (an override is the tool for a running journey, FR-54, not built).
6. **Earliest dates.** Cancel `from` is today or later; amend `effectiveFrom` is tomorrow or later (Q10 of spec 13). A cancel for today is allowed only while the journey has no events.
7. **The generator and cancellations.** The generator, per service and date, asks `ServiceRuns` with the cancellations; a cancelled date gets no journey and a cancelled date is never "created again" by a later run. A journey already `Cancelled` is never regenerated (the unique `(service_id, operating_date)` keeps it).
8. **Passenger reads (spec 13 rule 14, Q5).** `FindTrains`, the station board and the timetable fallback skip a cancelled date (through `ServiceRuns`) and skip journeys whose status is `Cancelled`; `trains/{n}/status` for a cancelled date returns `404 journey_not_found`. A cancelled train is absent, not shown as Cancelled (Q4).
9. **Operator side.** A `Cancelled` journey is not reportable: the existing not-reportable reason is returned and the operator's active-journey list omits it (checked in the build, Q6).
10. **Validation reuse.** Amend uses the same `ServiceValidator` (dates, stop order, increasing times, platform of its station, platform advice). The advisory lock per train number is taken exactly as in create.
11. **Reason text.** Plain text, stored as typed (trimmed), never rendered as markup or HTML; maximum 200 characters.
12. **Audit (NFR-17).** Every cancel and amend writes one `service_change_log` row (actor, time, details) in the same transaction; a failed change writes none.
13. **Safety boundary (FR-63).** Cancelling or amending records the plan; it commands no train.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- **Domain:** `ServiceRuns.On` overload with cancellations; `CancellationRange` (a pure type: from, to, `Covers(date)`); `AmendmentDiff` (pure: old stops vs new stops → readable changes, for the version summary).
- **Application (`Features/Timetable`):** `CancelServiceHandler`, `AmendServiceHandler`; repository additions `CancelAsync(...)`, `AmendAsync(...)`, `GetCancellationsAsync(...)`. Both call `JourneyGenerationHandler` for the new dates after commit.
- **Infrastructure:** migration `AddCancellation`, EF configuration of `service_cancellation`, repository implementations (the advisory lock helper from create is reused), the passenger queries pass cancellations to `ServiceRuns` and filter `Cancelled` journeys.
- **Api:** two routes added to `TimetableEndpoints`; the 401/403 matrix in `AuthConfigurationTests` gains both.
- **Telemetry:** `smartrail.timetable.changes` with `action` = cancelled or amended and `result` = ok, rejected, conflict; log line with `trainNumber`, `serviceId`, `actor`, counts; never the reason text at Information level beyond its length (it is free text).
- **Config:** none new.

## 8) Edge Cases
| Case | Handling |
|---|---|
| Cancel a date the service does not run on | Skipped; if nothing in range, `422 nothing_to_cancel` |
| Cancel "from onward" on a service with a future version | Applies to the train number, so version 2 is cancelled too |
| Cancel the same range twice | `200`, `cancelledJourneys: 0`, one row only |
| A journey in the range has a report | `409 journey_has_events`, nothing changes, even for earlier dates in the range |
| Cancel an extra train | Works as a weekly one (its date is the range) |
| Cancel today before any event | Allowed; journey becomes Cancelled |
| Generator runs after a cancel | No journey on cancelled dates; the Cancelled journey is not touched |
| Amend where a day is removed | That day's future journeys become Cancelled ("Removed by amendment"), logged |
| Amend where a day is added | Generator creates journeys for the new days inside the horizon |
| Amend effective date not after the latest version's start | `422 effective_date_not_after_current` |
| Amend while a later version already exists | The latest version is the one amended; its start must be before the new date |
| Amend with an assigned operator on a future journey | Journey id and assignment stay; stop states are rebuilt |
| Amend an extra train | `422 amend_extra_not_supported` |
| Two controllers amend the same train at once | The advisory lock serialises; the second sees version 2 and fails with `422 effective_date_not_after_current` or succeeds as version 3 if the date allows |
| Cancelled train asked for by number | Status read `404 journey_not_found` for that date |
| Reason with `<script>` | Stored and returned as text; the UI shows it as text (spec 08 escaping) |

## 9) Acceptance Criteria
- [x] A controller can cancel a service for one date or from a date onward with a reason; the service and every version remain (FR-11C, D-70).
- [x] Journeys in the range with no events become `Cancelled`; others are untouched; the generator creates none on cancelled dates, also after a restart (FR-11C, AC-E02-01).
- [x] A range containing a journey with events is refused with 409 and nothing changes (all or none).
- [x] Repeating the same cancel changes nothing and writes no second row.
- [x] The Cancelled tab lists cancellations that cover a date today or later with train, route, range, reason, who and when; the service detail lists them (FR-11C).
- [x] A controller can amend a weekly service from a date tomorrow or later; the result is a new version, the old one is kept with its dates (FR-11B, D-70).
- [x] Journeys before the effective date keep the old service version and times; journeys from it show the new times; no date has two journeys of the same train (FR-11B).
- [x] The Versions list shows each version with who and when and a readable summary of what changed.
- [x] Amend runs the same validation as create (dates, stop order, increasing times, platform of its station) and the platform advice needs acknowledging (FR-11D).
- [x] The passenger search, the station board and the status read follow the version in force and omit cancelled trains (spec 13 rule 14).
- [x] A `Cancelled` journey cannot receive operator reports and is not in the operator's active list.
- [x] Operator, passenger and caterer tokens get 403, no token 401, on both routes.
- [x] Every cancel and amend has one audit row with the actor; a failed call has none.
- [x] The existing suites (261 unit, 305 integration at the end of spec 13) still pass, plus the new tests.
- [x] The report states plainly that passenger notification, refunds, caterer messages, the controller screens, amending extra trains and showing "Cancelled" to passengers are not built.

## 10) Test Plan
### 10.1 Seed and fixture data
Seeded services (8058 weekdays, 8061, 8075 daily night), a created weekly service (8072) with generated journeys, one journey with a `train_event` (reported), controller/operator/passenger tokens, a fake clock set to 19 October 2026 10:00.
### 10.2 Unit tests
`ServiceRuns` with cancellations (one date, from-onward, outside the range, no cancellation), `CancellationRange.Covers`, `AmendmentDiff` (time change, platform change, stop added/removed, no change), cancel and amend validation (reason empty/201 chars, date in the past, `to` before `from`, effective date today, effective date not after the current).
### 10.3 Integration tests (Postgres)
Cancel one date; cancel from onward (generator after: no journeys); cancel twice (one row); cancel a range with a reported journey (409, nothing changed, including the earlier dates); cancel a date the train does not run; cancel an extra train; amend a time (versions, `effective_to`, journeys re-pointed, stop states Pending, old journeys unchanged); amend removing a day (journeys Cancelled); amend adding a day (generator); amend with a reported journey (409); amend an extra (422); amend twice concurrently (advisory lock, one wins); passenger `GET /trains`, the station board and the status read for a cancelled and an amended service; operator report on a Cancelled journey is refused; 401/403 matrix for both routes; audit rows; migration (constraint, enum text, old data intact); `ck_journey_status` accepts Cancelled and refuses other text.
Before building: grep every `switch` or comparison on `JourneyStatus` (domain, application, passenger queries, operator history, exceptions, overview, mobile contracts) and decide the `Cancelled` case in each (Q6).
### 10.4 End to end
Compose smoke by hand: create 8072, cancel 23 Oct, amend 8058, read the passenger search and the Cancelled tab. No Playwright in this spec (no screens).

## 11) References
- Spec 13 (sections 4 to 6, 13, 14), spec 12 (previous journeys must keep the version they ran under), spec 04 (passenger reads), spec 09 (board); `docs/smartrail-refined-requirements.md` FR-11B, FR-11C, FR-11D, FR-54; D-69, D-70, D-65, D-72.
- Code touched: `SmartRail.Domain/Scheduling/Scheduling.cs` (`ServiceRuns`), `Application/Features/Timetable/Timetable.cs`, `Infrastructure/Persistence/TimetableRepository.cs`, `Application/Features/Passenger/PassengerData.cs`, `Api/Features/Timetable/TimetableEndpoints.cs`, `JourneyConfigurations.cs`.

## 12) Open Questions (recommendation first)
- **Q1. Scope.** Recommend backend only, as for spec 13. Then a separate spec 15 for the controller Timetable web screens (list, wizard, detail, cancel dialog, amend) together with the seven undrawn boards, if time allows. If no screens are built, FR-11A to FR-11D exist as API only and the report must say it.
- **Q2. Cancellation by train number or by service row.** Recommend by train number and line (stored with the version viewed), so a "from onward" cancel also covers a later version. Alternative: tie it to one service row and copy it on amend (more code, easy to lose). Side effect: a weekly and an extra train with the same number are cancelled together for dates both cover.
- **Q3. Amend an extra train.** Recommend not supported (422); cancel it and create another. An extra train is one date, so a version adds little.
- **Q4. Show "Cancelled" to passengers.** Recommend a later step (spec 10 already has the Cancelled fixture and the `LiveCancelled` board). For now a cancelled train is absent; a passenger who planned on it gets no signal. Say so.
- **Q5. Journeys whose day is removed by an amendment.** Recommend they become `Cancelled` with the reason "Removed by amendment" (history kept). Alternative: leave them on the old version until their date (needs the old version to stay effective, which contradicts the date rule).
- **Q6. `Cancelled` status in existing code.** Recommend adding it as the last enum value and checking every `switch` in the build (the list is in 10.3). Alternative with less risk to the older code: keep the status and add a `cancelled_at` column. The enum is cleaner and was already the plan in spec 13; the grep decides the cost.
- **Q7. Cancel with events.** Recommend refuse the whole range with 409 and name the first date (all or none). Alternative: cancel the clean dates and report the others (partial result, harder to explain and to test).
- **Q8. Reason visibility.** Recommend the reason is visible to controllers only for now (not in passenger reads), since passenger display of reasons needs wording and translation (FR-14). Say so.

## 13) Owner decisions of 19 Oct 2026
- Q1 to Q8 as recommended: backend only; cancel by train number and line; no amending of extra trains; a cancelled train is absent from passenger reads (not shown as Cancelled); a removed day's journeys become Cancelled ("Removed by amendment"); `Cancelled` is added as the last `JourneyStatus` value and every use is checked; a range with a journey that has events is refused whole (409); the reason is visible to controllers only.
- Branch `claude/feature/scheduling-cancel-amend` starts from `dev`, which already holds specs 10 to 13.

## 14) Delivery notes (19 Oct 2026)
- **Built (backend):** `POST /api/v1/timetable/services/{id}/cancel` and `/amend`; migration `AddCancellation` (`service_cancellation`, `Cancelled` in the journey status check); `JourneyStatus.Cancelled` handled in every use (operator report refused with `journey_not_reportable`, operator active journey, passenger status by id and by number, search, line status, station board, controller overview); the generator and the passenger reads skip cancelled dates (`ServiceRuns.On` with cancellations); the Cancelled tab, `cancellations[]` and `cancelledRanges` on reads; version `summary` from the audit row; the weekly list shows the newest version per train number. 30 new tests (264 unit and 336 integration in all) pass.
- **Checked live on Compose** as `jayasinghe`: cancelling 8072 for 23 October returned 1 cancelled journey, the Cancelled tab lists it with the reason and 8072 is absent from the passenger search for that date; amending 8072 from 26 October returned version 2 with 1 journey updated.
- **Not ticked:** the report criterion (this section is its start). Everything else in section 9 has a test.
- **Not built (as agreed):** controller screens; telling passengers (FR-11B, FR-11C notification), refunds, messages to the caterer; un-cancelling; cancelling a running or finished journey (an override); showing "Cancelled" to passengers (a cancelled train is simply absent); amending an extra train; services that pass midnight; operator assignment.
- **Decisions made while building:** a journey that is Running or Completed also blocks cancel and amend, even without a report row. The amendment date must be after the start of the latest version and tomorrow or later (`date_in_past`, `effective_date_not_after_current`). Two amendments at once: the second one builds on the first, or is refused with `effective_date_not_after_current` when its date is not after the first one's start. A cancellation covers every version and every extra date of the train number on that line.

## 15) Closing report (19 Oct 2026)
- **Built:** cancel (one date or from a date onward, with a reason; all or none; 409 `journey_has_events`) and amend with versions (weekly services), as in the delivery notes above. The screens are spec 15.
- **Not built:** passenger notification, refunds, caterer messages (D-72), amending extra trains (cancel and add another instead), un-cancelling, and showing "Cancelled" to passengers. A cancelled train is absent from search, board and status. A per-journey "Cancelled" state for passengers belongs to the overrides of spec 16 (F3), which is **not built**.
