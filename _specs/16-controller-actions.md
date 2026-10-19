# Feature Spec: Controller actions and the remaining dashboard screens (journey detail, verify, overrides, platform change, disruptions, line closure)

branch: claude/feature/controller-actions
spec: _specs/16-controller-actions.md
owner: tharindu
date: 2026-10-19
status: approved (F2 only)

---

## 0) Summary
**One-liner:** Everything on the controller dashboard that is drawn and has no spec yet: the **journey detail** with the one-track view, **verify** a delay reason (one and several), **record a suspension, cancellation or early termination** (override), **change a platform** with the conflict warning, **publish and withdraw disruption messages**, **close a line section**, and **enter an event for an operator**. Each is a feature group (F1 to F7) with its own API, data and screens. Hand over, By controller and Reassign (FR-125, FR-126) are drawn and stay **designed, not built**.
**Why now:** FR-52, FR-53, FR-54, FR-55 and FR-57 are Musts and are the "protected, not cut" core of the controller (requirements section on cuts: controller verification and overrides). Today the controller can read the overview, work the exceptions queue and, since specs 13 to 15, manage the timetable. Nothing can be verified, overridden, re-platformed or announced. Without verification the passenger never sees "Verified by Operations" (FR-52), and the suspended, cancelled and terminated states already drawn for the passenger and operator apps (spec 10, spec 05 fixtures) can never be reached for real.
**Who is this for:** Controller (Jayasinghe). Passengers, operators and the platform displays see the results. No new role (D-69, D-74).
**Success looks like:**
- Jayasinghe opens Train 8058, sees the stops, the one-track view and the event list, verifies the delay reason "Signal failure", and the passenger live screen now shows the reason with "Verified by Operations" and the time.
- He records a suspension with a reason; the override stays until he clears it; the passenger and operator screens show "Suspended"; every set and clear is in the audit trail with his name.
- He changes Gampaha's platform for 8058; a train planned on the same platform within 10 minutes gives a warning (higher severity when the trains run in opposite directions); he can still confirm; the displays show the new platform within 30 seconds.
- He publishes "Train 8058 delayed about 10 minutes, signal failure" from a template, sees where it will appear before confirming, and withdraws it later.
- A controller token is required for every route; operator and passenger tokens get 403.

Size: **large, about 8 days in all** (section 12, Q1). It cannot be built before 20 October. It is cut into slices in a fixed order, and anything not built is reported as **designed, not built** with the spec and the board as the evidence, the same way as FR-95 and FR-126.

## 1) Traceability
- Requirements: FR-52 (verify or change a delay reason; passengers see "Verified by Operations" and the time), FR-53 (controller enters an event for an operator, with its source), FR-54 (suspend, cancel or terminate with a reason; active until cleared; set and clear audited; records a decision, controls no train), FR-55 (publish and withdraw disruption messages from pre-translated templates), FR-57 (change a platform among the station's platforms; reaches displays and app within 30 seconds; same-platform warning within a configurable window, 10 minutes proposed; opposite directions are higher severity; advice only; the check also runs when estimates change and raises an exception), FR-59 (close a line section, both directions, Should), FR-62 (what passengers see, Could, helped by the preview), FR-63 (information system, not a safety system), FR-60 (correct a report, Should: not built), FR-125 (controller in charge, Should), FR-126 (senior controller, **Deferred, D-76**), FR-12 and FR-56 (assignment, see Q9), FR-14, NFR-04 (30 seconds), NFR-12 (public data is passenger-safe), NFR-17 (audit), NFR-30, NFR-33.
- Decisions: D-10 (verification), D-25 (templates), D-44 (severity), D-74 (controller in charge), D-75/D-76 (senior controller, deferred), D-40, ADR-05 (overrides as rows, active until cleared), ADR-09.
- Acceptance criteria: the E05 exception criteria already cover verification wording (AC-E05-02); the rest of this spec proposes new criteria in section 9, as in specs 13 and 14 (the baseline has none for overrides, disruptions or closures; Q10).
- Existing code and data (read on 19 October): `EventType` already has `ReasonVerified`, `OverrideSet`, `OverrideCleared`, `PlatformChanged` and the check constraint allows them, and `EventSource` has `Controller`, but **nothing writes them**. The real database has **no** `journey_override`, `disruption_message` or `controller_assignment` table (they are only in `smartrail-schema.sql`, the review artefact). `ExceptionType` has only `ReasonAwaitingVerification` and `EarlyDeparture` (no platform conflict). Controller endpoints today: `GET /exceptions`, `POST /exceptions/{id}/resolve`, `GET /overview`, and the timetable routes. `journey_stop_state` already has a platform and the confirmation state.
- Design boards (Design canvas): `Dash-Journey`, `Dash-Verify`, `Dash-BulkVerify`, `Dash-Exceptions` (platform conflict detail), `Dash-Suspend`, `Dash-Platform-Warning`, `Dash-Disruptions`, `Dash-Publish-Review`, `Dash-LineClosure`, `Dash-Overview` (the Active overrides and Awaiting verification cards, the Verification column and the two buttons are drawn here), `Dash-HandOver`, `Dash-ByController`, `Dash-Reassign`. **Not drawn:** the manual event entry form (F7), disruption steps 1 and 2 (message and where it appears), withdraw confirmation, clear-override confirmation, a Journeys list page, empty and error states.

## 2) Goals and Non-Goals
### Goals
- **F1 Journey detail** (read): stops with scheduled, estimated or actual time, state and platform; the one-track view; the event list (newest first, with source and actor); crew and control; the current override and verification.
- **F2 Verify** (FR-52): verify, change the reason, or reject, for one exception; verify several at once; the passenger read shows the reason and "Verified by Operations" only after verification.
- **F3 Overrides** (FR-54): suspend, cancel or terminate a journey with a reason; clear it; passenger, operator, board and display reads show the state; audit.
- **F4 Platform change** (FR-57): preview and confirm a platform change with the conflict warning; the automatic conflict check when estimates change raises a **Platform conflict** exception.
- **F5 Disruption messages** (FR-55): templates, targets, preview, publish, withdraw, list; shown on the displays and in the passenger app.
- **F6 Line closure** (FR-59, Should): close and reopen a section, with the affected journeys listed and a disruption message created.
- **F7 Enter an event for an operator** (FR-53): a form from the Overview and the journey detail, source `Controller`.
- Web screens for all of these in `web/ops`, in the style of spec 08 and spec 15.
### Non-Goals
- **Hand over, By controller, Reassign** (FR-125 Should, FR-126 Deferred): the boards stay as design only. The Journey board's "Crew and control" card shows "No controller in charge" until FR-125 is built (the board shows "Jayasinghe (you)").
- **Operator assignment** (FR-12, FR-56): Musts, **no board and no API**. See Q9; recommended as its own spec after this one.
- **Correct an operator report** (FR-60, Should): the button on the Journey board is not built and is hidden.
- Telling passengers by push (not built, specs 13 and 14); refunds; caterer messages (D-72).
- Computing when two trains will pass each other (the "passes near Ragama about 08:56" line on the one-track view): see Q3.
- Individual tracks (A-24); a station terminal / kiosk disruption layout beyond the displays already built (Could); rate limiting (NFR-13).

## 3) User Workflow
- **F1:** Overview row (or **Journeys** in the menu, undrawn list, Q8) opens the journey. One-track view shows each stop as a dot, "this train" and other trains between stops, above the line for one direction and below for the other; a text alternative describes it.
- **F2:** Exceptions, a "Delay reason waiting for verification" row opens Verify: the operator's report, two checks (estimate consistent with stop times; reason from the approved list), "What passengers will see", and **Verify and publish reason**, **Edit reason**, **Reject report**. In the queue, **Select several to verify** lets him tick the exceptions that can be published; a free-text "Other" reason is locked.
- **F3:** Journey detail, **Suspend, cancel or terminate**: dialog with type, reason, optional note (and the station for an early termination); confirm. The journey shows an active override with **Clear override**.
- **F4:** Journey detail (or the platform-conflict exception), **Change platform at Gampaha**: radio list of that station's platforms with conflict chips; the warning callout; **Confirm change** (for a High warning a second step, "I have checked with signalling and control", is proposed in Q5).
- **F5:** Disruptions list (Showing now, Earlier today), **New message**: step 1 message (template and parameters), step 2 where it appears (train, station or line section; displays are pre-selected and can be unticked), step 3 review with "How it will look" and the list of places; **Publish**. **Withdraw** on a live message.
- **F6:** Line closures: From and To stations (with distances), reason, the strip drawn with the closed section, the affected journeys; **Record closure**; later **Reopen**.
- **F7:** **Enter event manually**: pick a journey, the event (departed, arrived, delay), the stop and time, a reason for the entry; the event is stored with source `Controller` and shown in the event list as "entered by Jayasinghe for the operator".
- **Errors:** a conflict (another controller already verified or cleared) is shown and the screen refreshes; a refused platform or an unknown template is a field error; session and role problems use the shell's existing screens.

## 4) API Surface
All routes below require the `Controller` policy. 401 without a token, 403 for other roles. Errors are ProblemDetails with a stable `reason`. New routes only; nothing existing changes its meaning except the public status reads (rule 9).

### F1 Journey detail
- `GET /api/v1/controller/journeys?line=MAIN&scope=running|today` : rows as on the Overview table plus `journeyId` (Q8).
- `GET /api/v1/controller/journeys/{journeyId}` : `trainNumber`, route names, direction, category, status key and group, delay, `verification` (`reasonCode`, `verifiedAt`, `verifiedBy`) or null, `override` (type, reason, atStation, setAt, setBy) or null, `stops[]` (station, scheduled, estimatedOrActual, state, platform), `events[]` (time, type, source, actor display name, summary text), `operator` (name, assigned from, to) or null, `controllerInCharge` (always null until FR-125), `otherTrains[]` (train number, direction, between which two stops; no meeting time, Q3). 404 `journey_not_found`.

### F2 Verify
- `POST /api/v1/exceptions/{id}/verify` body `{ "action": "verify" | "change" | "reject", "reasonCode": "signal_failure" }` (`reasonCode` required for `change`). 200 `{ journeyId, verifiedAt }` for verify and change; reject resolves the exception with resolution `Rejected` and publishes nothing. 409 `already_resolved`; 404 `exception_not_found`; 422 `reason_not_approved` (free text or an unknown code), `not_a_verification_exception`.
- `POST /api/v1/exceptions/verify` body `{ "ids": ["..."] }` : each id is handled on its own (no all-or-none): 200 `{ results: [ { id, status: "verified" | "skipped", reason?: "reason_not_approved" | "already_resolved" } ] }`.

### F3 Overrides
- `POST /api/v1/journeys/{journeyId}/overrides` body `{ "type": "Suspended" | "Cancelled" | "Terminated", "reasonCode": "...", "atStationCode": "GPH" (Terminated only), "note": "..." }` : 201 `{ overrideId, setAt }`; 409 `override_active`; 422 `invalid_override`, `reason_not_approved`, `station_not_on_journey`, `journey_completed`; 404.
- `POST /api/v1/journeys/{journeyId}/overrides/{overrideId}/clear` : 200 `{ clearedAt }`; 409 `already_cleared`.

### F4 Platform change
- `POST /api/v1/journeys/{journeyId}/platform-changes/preview` body `{ "stationCode": "GPH", "platformCode": "1" }` : always 200 `{ valid, errors[], warnings[] }` (each `{ code, severity: "Advice"|"High", message, otherJourney: { trainNumber, direction, at } }`).
- `POST /api/v1/journeys/{journeyId}/platform-changes` same body plus `acknowledgeWarnings` : 200 `{ platformCode, changedAt }`; 409 `warnings_not_acknowledged`; 422 `platform_not_at_station`, `stop_not_in_service`, `stop_passed`.

### F5 Disruptions
- `GET /api/v1/disruptions/templates` : the catalogue: `id`, parameters (for example `trainNumber`, `delayMinutes`, `reason`), English text, `si` and `ta` text.
- `GET /api/v1/disruptions?state=active|earlier` : messages with scope, text, places, who and when.
- `POST /api/v1/disruptions/preview` body `{ templateId, parameters, target: { type: "Journey"|"Station"|"Section", ... } }` : 200 `{ rendered: { en, si, ta }, places[] }`; 422 `unknown_template`, `missing_parameter`.
- `POST /api/v1/disruptions` same body plus `places[]` (the unticked ones are left out) : 201 `{ id, publishedAt }`.
- `POST /api/v1/disruptions/{id}/withdraw` : 200 `{ withdrawnAt }`; 409 `already_withdrawn`.

### F6 Line closure
- `POST /api/v1/line-closures` body `{ "lineCode": "MAIN", "fromStationCode": "...", "toStationCode": "...", "reasonCode": "landslide" }` : 201 `{ id, affectedJourneys: [...], disruptionId }`; 422 `invalid_section`, `reason_not_approved`; 409 `section_already_closed` when the range is within an open closure.
- `GET /api/v1/line-closures?state=open|closed`; `POST /api/v1/line-closures/{id}/reopen`.
- `GET /api/v1/line-closures/preview?from=..&to=..` : the affected journeys, for the form.

### F7 Manual event
- `POST /api/v1/journeys/{journeyId}/events/by-controller` : the body of the operator event (spec 03) plus `reason` (required, 1 to 200 characters); stored with `source = Controller`, `created_by` the controller; the same ordering, duplicate and recalculation rules as spec 03 (client event id, idempotent). 201/200 as the operator route; the same reason codes (`order_conflict` and the rest, spec 12 list).

## 5) Data Contract
**One migration `AddControllerActions`** (EF Core, snake_case; default privileges give the app role DML on new tables):
- `journey_override`: as in `smartrail-schema.sql`: `id`, `journey_id` fk, `type` check (`Suspended`, `Cancelled`, `Terminated`), `at_station_id` fk null, `reason_code varchar(30)`, `note varchar(200) null`, `set_at`, `set_by varchar(64)`, `cleared_at`, `cleared_by`; **partial unique index** `ux_override_active (journey_id) where cleared_at is null` (at most one active override). Never updated except to clear; no delete.
- `journey_verification` is **not** a table: a verification is a `train_event` of type `ReasonVerified` (source Controller, payload `{ reasonCode, replaces }`); the current verification is the latest such event of the journey after the latest `DelayReported`. A new delay report after verification makes the reason unverified again (a new exception is raised as today).
- `disruption_message`: `id`, `journey_id` null, `station_id` null, `line_id` null, plus **`from_station_id` and `to_station_id` null** for a section (Q6), `template_id`, `parameters jsonb`, `places jsonb` (the chosen places), `published_at`, `published_by`, `withdrawn_at`, `withdrawn_by`; check that exactly one scope is set; index on `(withdrawn_at, published_at)`.
- `line_closure`: `id`, `line_id`, `from_station_id`, `to_station_id`, `reason_code`, `closed_at`, `closed_by`, `reopened_at`, `reopened_by`, `disruption_id` fk; no overlap of open closures checked in the handler.
- `ExceptionType` gains `PlatformConflict` (F4, severity Critical when opposite directions, otherwise High); the `journey_exception` table already has `related_journey_id` and `platform_id` for it. `ck` on exception type is extended if it exists.
- `train_event`: new rows only (`ReasonVerified`, `OverrideSet`, `OverrideCleared`, `PlatformChanged`, and `Departed`/`Arrived`/`DelayReported` with source Controller). The append-only rule is unchanged.
- Approved lists (reason codes, override reasons, closure reasons, disruption templates) are **code and configuration** (a static catalogue file), not tables, in the style of the existing delay reasons. Sinhala and Tamil text in the catalogue is marked for native-speaker validation (Q7).
- Passenger and display reads gain fields: `override` (type, reason code, at station), `reasonVerified` (code, verifiedAt) and `disruptions[]` (rendered text per language). Only approved codes and rendered template text are public; a delay reason is public **only after verification**; a free-text reason and a note are never public (NFR-12).

## 6) Business Rules
1. **Controller only (D-69, D-74).** Every action stores the actor (token subject) and shows the display name. Any controller can act on any journey (FR-04); there is no ownership check until FR-125.
2. **Verify (FR-52, D-10).** Only a delay reason that is on the approved list can be verified. "Other" free text can never be published: it is locked in the bulk list and in verify, and the controller must **change** it to an approved reason. Verify writes a `ReasonVerified` event, resolves the exception, and from then the public status carries the reason and "Verified by Operations" with the time. Passengers see "reason pending" until then (existing behaviour).
3. **Reject.** Reject resolves the exception as `Rejected`, never publishes the reason, keeps the delay itself (correcting a report is FR-60, not built), and is audited. Q4 asks whether Reject should exist in this form.
4. **Bulk verify.** Items are processed one by one in the order sent; a skipped item does not stop the rest; the result list says why.
5. **Override (FR-54, ADR-05).** One active override per journey. `Terminated` needs a station that is a stop of the journey and not yet passed. `Cancelled` is allowed before the journey completes. Setting or clearing writes the row and an `OverrideSet` / `OverrideCleared` event in one transaction. The override records a decision made through the control channels: it does not stop a train and the screen says so (FR-63).
6. **Override effects on reads.** While active: the passenger live status, search result, station board, platform display and operator app show Suspended, Cancelled or Terminated early with the approved reason; estimates stop being projected past the override; clearing returns to the computed status. A **Cancelled** override on one journey is shown as Cancelled to passengers (this is the "show Cancelled" step that spec 14 Q4 postponed); a timetable cancellation (spec 14) stays absent.
7. **Platform change (FR-57).** The platform must be one of the station's platforms and the stop must still be ahead of the train (or in service). The warning looks at other journeys on the same platform of the same station whose estimated time is within `Controller:PlatformWarningMinutes` (default 10, configurable, NFR-41): advice; when the two journeys run in opposite directions the warning has severity High and says to check with signalling and control; neither blocks (FR-63): the controller confirms with `acknowledgeWarnings`. The change is a `PlatformChanged` event and updates the stop state; displays and the app read it on their next poll (NFR-04, within about 30 seconds).
8. **Automatic platform conflict (FR-57).** When an estimate changes (a delay is reported or an arrival is recorded) the same check runs for the affected stops; a conflict raises a `PlatformConflict` exception (Critical for opposite directions, High otherwise) once per (journey pair, platform), and it appears in the queue with the buttons drawn on `Dash-Exceptions`. It never changes a platform by itself.
9. **Disruptions (FR-55, D-25).** A message is built only from a template and its parameters; there is no free text. The preview shows English, Sinhala and Tamil and the list of places (displays, the passenger app for followers) it will reach; the controller may untick places. Published messages show on the displays and in the passenger app until withdrawn; withdrawing is immediate; nothing is deleted. A message text is passenger-safe by construction (NFR-12).
10. **Line closure (FR-59).** A closure names a section (two stations, both directions). Recording it lists the journeys that pass through the section (planned stops in that range and not yet finished) and creates the matching disruption message. It does **not** change any journey: no automatic suspension (Q6 of this spec's open questions). Reopening withdraws the message.
11. **Manual event (FR-53).** Same ordering and validation as an operator report. The source is recorded and shown; the operator's own later reports still apply (a duplicate is rejected by the existing order rules).
12. **Audit (NFR-17).** Every verify, reject, override set and clear, platform change, publish, withdraw, closure and manual event writes who, when and what, in the same transaction.
13. **Nothing is deleted.** No route deletes an override, a message, a closure or an event.
14. **Safety boundary (FR-63, A-05).** Every screen that records a decision keeps the safety notice; wording says "records", never "stops", "controls", or "prevents a collision".

## 7) Architecture (follow CLAUDE.md, ADR-15; spec 08 for the web)
- **Domain:** `Override` and `OverrideRules` (one active, station not passed), `Verification` (current verification from events), `PlatformConflict` (window, opposite-direction severity; shares the clash maths with spec 13's `PlatformClash` for planned times, uses estimates here), `DisruptionTemplate` catalogue and renderer, `ClosureSection` (stations in a range, affected journeys).
- **Application:** `Features/ControllerJourneys` (list, detail), `Features/Verification` (verify, bulk), `Features/Overrides`, `Features/PlatformChanges`, `Features/Disruptions`, `Features/LineClosures`, `Features/ManualEvents` (reuses the event pipeline of spec 03 with source Controller). The public reads (`GetJourneyStatus`, `FindTrains`, `GetStationBoard`, `GetLineStatus`, the display feed) add the new fields.
- **Infrastructure:** the migration, EF configurations, repositories, the catalogue file, advisory locks per journey for override set and clear (as for events).
- **Api:** one endpoints file per group, all `RequireAuthorization(Policies.Controller)`; added to `AuthConfigurationTests`.
- **Telemetry:** counters `smartrail.controller.actions` (`action` = verify, reject, override_set, override_clear, platform_change, disruption_publish, disruption_withdraw, closure_set, closure_reopen, manual_event; `result` = ok, rejected, conflict), a gauge or log for active overrides; logs carry `journeyId`, `trainNumber`, `actor`, never free-text notes at Information level.
- **Web (`web/ops`):** `features/journey/` (detail, one-track view, event list, actions), `features/verification/`, `features/overrides/` (dialog), `features/platform/` (change page), `features/disruptions/` (list, three-step publish, review), `features/closures/`, `features/manualEvent/`; routes `/journeys/:id`, `/exceptions/:id/verify`, `/exceptions/verify` (bulk), `/journeys/:id/platform`, `/disruptions`, `/disruptions/new`, `/line-closures`; side menu items **Journeys**, **Overrides** (if drawn as a list, Q8), **Disruptions**, **Line closures** are added only when their screens exist. The one-track view is built from separate placed elements, not one SVG (`diagrams.md` rule of the canvas), with a text alternative.
- **Config:** `Controller:PlatformWarningMinutes` (10), `Controller:OverrideReasons`, closure and disruption catalogues; all configurable (NFR-41).

## 8) Edge Cases
| Case | Handling |
|---|---|
| Two controllers verify the same reason | Second gets 409 `already_resolved`; the screen refreshes |
| Operator reports a new delay after verification | The reason is unverified again; a new exception is raised (existing rule) |
| Bulk verify includes a free-text "Other" reason | Skipped with `reason_not_approved`, the rest are verified |
| Override set on a completed journey | 422 `journey_completed` |
| Terminated at a station already passed | 422 `station_not_on_journey` or `stop_passed` |
| Override cleared after the operator kept reporting | Status is recomputed from the events; nothing is lost |
| Passenger is following a suspended train | Sees Suspended with the approved reason, never the note |
| Platform change to a platform of another station | 422 `platform_not_at_station` |
| Opposite-direction train on the same platform within the window | Warning, severity High, "check with signalling and control"; can confirm |
| Platform conflict raised by an estimate and the controller changes the platform | The exception resolves with the change; no duplicate for the same pair |
| Disruption template with a missing parameter | 422 `missing_parameter` |
| Withdraw twice | 409 `already_withdrawn` |
| Closure where "from" equals "to" or reversed order | 422 `invalid_section` (the order is normalised by line sequence when reversed, one station is refused) |
| Closure over journeys already running | Listed as affected; not changed; the controller decides each one |
| Manual event for a journey with no operator | Allowed (FR-12: the controller can enter events) |
| Manual event that conflicts with the operator's order | Rejected with the existing reason code |
| Token role missing | 403 on every route |

## 9) Acceptance Criteria (proposed, to be added to the baseline, Q10)
- [ ] Journey detail shows stops with scheduled and estimated or actual times, state and platform; the event list newest first with source and actor; the current verification and override; the one-track view has a text alternative (FR-62 helped).
- [x] A controller can verify, change or reject a delay reason; only approved reasons can be verified; after verification the passenger status shows the reason and "Verified by Operations" with the time, before it, "reason pending" (FR-52, NFR-12). *(Built and tested. Before verification the passenger app says the reason is pending; the API sends no reason.)*
- [x] Bulk verify handles each item independently and reports skipped items with the reason. *(Built and tested.)*
- [ ] A controller can suspend, cancel or terminate a journey with a reason; at most one active override; clear returns the computed status; both are audited with the actor (FR-54).
- [ ] The passenger live status, search, board, platform display and operator app show the override state and approved reason, never the note.
- [ ] Changing a platform accepts only the station's platforms, warns on a same-platform journey within 10 minutes, marks opposite directions as High with the signalling advice, never blocks, and reaches the displays and the app within 30 seconds on the Compose stack (FR-57, NFR-04).
- [ ] A conflict created by changed estimates raises one Platform conflict exception per pair and platform (FR-57).
- [ ] A controller can publish a disruption message from a template with a preview in three languages and a places list, and withdraw it; it shows on the displays and in the passenger app until withdrawn (FR-55).
- [ ] A controller can record and reopen a line closure; the affected journeys are listed; a disruption message is created and withdrawn with it; no journey is changed automatically (FR-59).
- [ ] A controller can enter an event for an operator; it is stored with source Controller and shown as such (FR-53).
- [x] No route deletes an override, message, closure or event; every action has an audit row with the actor. *(F2 routes only: no route deletes anything, and a verify or reject stores who and when on the closed exception, plus the `ReasonVerified` event with the actor. The other routes do not exist.)*
- [x] Operator, passenger and caterer tokens get 403, no token 401, on every route; unknown ids 404. *(F2 routes only, proved by `VerificationTests`.)*
- [x] No screen says it stops, controls or protects a train; the safety notice stays visible (FR-63). *(F2 screens only: the Exceptions screen keeps the safety notice and says Reject publishes nothing.)*
- [x] Existing unit, integration and web tests of specs 01 to 15 still pass; new tests cover every route and screen built. *(Specs 01 to 15 tests passed on 19 Oct; F2 has integration, web and app tests. F1 and F3 to F7 have none because they are not built.)*
- [x] The report states plainly which of F1 to F7 were built, that Hand over, By controller, Reassign, operator assignment, correcting a report and push notification are **designed, not built**, and where the evidence is (spec and board). *(See section 14.)*

## 10) Test Plan
### 10.1 Fixtures
Demo journeys 8058 (delayed with a reported reason), 8065 (opposite direction, same platform window), 8074 (override), 8021; a free-text "Other" reason case; controller, operator and passenger tokens; a fake clock; the five-station line with distances; the disruption catalogue with three templates.
### 10.2 Unit tests
Verification (current verification from events, unverified after a new report), override rules (one active, station not passed, completed), platform conflict (window boundary, opposite direction severity, same direction, other platform, once per pair), template rendering (parameters, three languages, missing parameter), closure section (range in line order, affected journeys, reversed input), status derivation with an override.
### 10.3 Integration tests (Postgres)
Each route: success, each reason code, role matrix (401/403), audit rows; verify then the public status; bulk with a mix; override set, active conflict, clear, public reads; platform change with and without warnings, the exception from changed estimates; publish, public read of the message on the board and the status, withdraw; closure and its message; manual event with the order rules; migration constraints (one active override, one scope per message); the append-only trigger still refuses edits to events.
### 10.4 Web and end to end
Component tests per screen (journey detail with the one-track view and its text alternative, verify, bulk with a locked row, override dialog, platform change with the warning, disruption steps and review, closure). Playwright against Compose: verify the demo delay and see the passenger status change; suspend and clear a journey; publish and withdraw a message and see it on a display feed.

## 11) References
- Specs 03 (events), 06 and 07 (reports, exceptions queue), 08 (controller web), 09 (displays and boards), 10 (passenger states), 12 (reason codes), 13 to 15 (scheduling and its screens). Requirements FR-52 to FR-63, FR-125, FR-126, NFR-04, NFR-12, NFR-17. D-10, D-25, D-44, D-74 to D-76, ADR-05, ADR-09. `smartrail-schema.sql` (the designed tables). Design canvas boards listed in section 1.

## 12) Open Questions (recommendation first)
- **Q1. Scope and order.** About 8 days (F1 1.0, F2 1.0, F3 1.5, F4 1.25, F5 1.75, F6 0.75, F7 0.75), against one day left before 20 October with other deliverables also open. Recommend building, if anything, **F2 Verify** first (a headline passenger requirement, and the smallest end to end slice: one endpoint, one event, one public field, two screens), then **F1** read-only detail. Everything else is reported as designed, not built, with this spec and the boards as the evidence. Tell me the real budget and I will cut the spec to match; a spec of this size is itself a deliverable.
- **Q2. Overrides need a real status model.** Suspended, Cancelled and Terminated reach four other surfaces (passenger status, search, board, operator app) and the displays. Recommend an override row plus a derived state (rule 6), not a new journey status, so clearing is just a clear. Alternative: new `JourneyStatus` values (faster, but "clear" has to remember the previous status).
- **Q3. The one-track view and "passes near Ragama about 08:56".** Computing a meeting time needs a speed model and reads like collision prediction, which FR-63 says SmartRail is not. Recommend showing positions only (between which stops) and dropping the meeting-time sentence from the board. The board needs a small edit.
- **Q4. What does Reject do?** The board has Reject report next to Verify and Edit. FR-52 says verify or change, not reject; FR-60 (correct a report) is Should. Recommend Reject only resolves the exception, keeps the reason unpublished and is audited (rule 3). Alternative: remove the button until FR-60 exists. I need your decision.
- **Q5. Confirming a high-severity platform warning.** The board has one Confirm. Recommend a deliberate second step for High (a checkbox "I have checked with signalling and control") because the warning says to do exactly that; advice-level warnings need only Confirm. This is a design change.
- **Q6. Line sections in `disruption_message`.** The designed table has journey, station and line scopes but not a section (two stations). Recommend adding `from_station_id` and `to_station_id`. Closures create a section message. No automatic suspension of affected journeys, because SmartRail records, it does not command (FR-63); the affected list is the controller's checklist. Alternative: a "Suspend all affected" button (one click, many audit rows; more to build and more to get wrong).
- **Q7. Template text in Sinhala and Tamil.** I can write English and structure the catalogue; the Sinhala and Tamil sentences should be validated by a native speaker (as with the earlier UX copy). Recommend the catalogue ships with English complete and si/ta marked `needsReview`, shown to passengers as the English text plus the placeholder only if reviewed (otherwise English). Your call on whether unreviewed text may be shown.
- **Q8. Screens that are not drawn.** Journeys list page (the Overview table does this today), Overrides list (the menu shows Overrides with a count and no board), manual event form (F7), disruption steps 1 and 2, withdraw and clear confirmations, empty and error states. Recommend I draw them as boards before building (about eight), as for the timetable. Until then menu items without a screen are not added.
- **Q9. Operator assignment (FR-12, FR-56).** Two Musts with no board, endpoint or screen, and they gate the demo (a newly scheduled train, or a lost device, cannot be handled). Recommend a short separate spec (assign, end assignment, list) after F2, and a board for it. Until then the report says assignment is seed data only.
- **Q10. New acceptance criteria and edge cases.** Same as specs 13 and 14: the baseline has none for overrides, disruptions or closures. Recommend adding sections 8 and 9 as new entries dated 19 October. That changes your baseline, so it is your call.
- **Q11. Overview board versus the built Overview.** The board draws Active overrides and Awaiting verification cards, a Verification column and two buttons (Enter event manually, Publish selected). I have not compared the built page with the board line by line. Recommend a quick check and, where the board is ahead, adding those items only as their features land.

## 13) Owner decisions of 19 Oct 2026 and delivery notes
- **Scope (Q1):** only **F2 Verify** is built now. F1, F3 to F7 stay designed, not built, with this spec and the boards as evidence. Recommendations accepted for Q4 to Q11 as far as F2 needs them.
- **Q4 Reject:** Reject only closes the exception with resolution `Rejected`, publishes nothing and is audited (rule 3).
- **Q7:** English reason names complete; Sinhala and Tamil text in the passenger app is unverified.
- **Q10:** the F2 criteria in section 9 are added as new baseline entries (verify or change, approved reasons only, bulk, public status after verification).
- **Branch:** `claude/feature/controller-actions`, branched from `claude/feature/operator-assignment`.
- **Status:** approved for F2 only.

### F2 as built
- `POST /api/v1/exceptions/{id}/verify` (`verify`, `change` with `reasonCode`, `reject`) and `POST /api/v1/exceptions/verify` (up to 50 ids, each on its own). Errors as in section 4: 404 `exception_not_found`, 409 `already_resolved`, 422 `reason_not_approved`, `not_a_verification_exception`, `invalid_request`.
- **Approved reasons** are the operator's codes except `Unknown`: `SignalIssue`, `TechnicalFault`, `Obstruction`, `Weather` (`VerificationRules`). An `Unknown` report is locked: a controller must **change** it. (The spec's example code `signal_failure` is not used; the codes already in the reports are.)
- A verification is a `ReasonVerified` event (source Controller, payload `reasonCode`, `delayEventId`, `changed`) written in the same transaction as closing the exception and bumping the journey version, so clients holding the older status take the new one. A newer delay report makes the reason unverified again (the verification belongs to one delay report). Resolution values: `ReasonVerified`, `ReasonChanged`, `Rejected`.
- Public status (`journeys/{id}/status`, `trains/{n}/status`) gains `reason { code, verifiedAt }`, **omitted** until verified (D-10 and NFR-12 still hold: the field is absent, not null, so no unverified reason can leak).
- The controller's open queue gains `reportedReason` and `canVerify` per delay item (controller-only).
- Web: the Exceptions screen shows the reported reason, what passengers will see, **Verify and publish reason**, **Edit reason**, **Reject report**, and **Select several to verify** (a locked row cannot be ticked). Passenger app: the live screen shows "Reason: Weather. Verified by Operations at 08:40."
- Not built in F2: the separate `/exceptions/:id/verify` page and the two consistency checks drawn on `Dash-Verify` (the queue panel is used instead).

## 14) Closing report (19 Oct 2026)
- **Built:** F2 Verify only (backend, Exceptions screen, passenger app). Checked by integration tests, web tests, app tests and the Playwright flow `web/ops/e2e/verify.spec.ts` (written; it needs a journey that is not Completed, so it was **not run** against the current database, where today's demo journey is finished).
- **Not built (designed only, evidence is this spec and the boards):** F1 journey detail and the one-track view, F3 overrides (suspend, cancel, terminate) and their reads, F4 platform change and the platform-conflict exception, F5 disruption messages, F6 line closure, F7 entering an event for an operator.
- **Also not built:** Hand over, By controller, Reassign (FR-125, FR-126), correcting a report (FR-60) and push notification. Operator assignment is spec 17. The unticked criteria above are the unbuilt features.
- **Partly different from the design:** `Dash-Verify` is not a separate page: the queue panel does verify, edit and reject, and the two consistency checks drawn there are not built. Sinhala and Tamil text in the app is unverified.
