# Feature Spec: Controller web, Timetable screens (list, new service, extra train, cancel, amend)

branch: claude/feature/timetable-web
spec: _specs/15-timetable-web.md
owner: tharindu
date: 2026-10-19
status: approved 2026-10-19 (owner: scope W0 to W2, then W3 if it goes well; Q2 to Q10 as recommended; branch from dev)

---

## 0) Summary
**One-liner:** The Timetable section of the controller web app (`web/ops`): a list with the three tabs (Weekly schedule, Extra trains, Cancelled), a service detail with stops, versions and cancellations, a three-step new-service wizard (Details, Stops and times, Review and schedule), the add-extra-train flow, a cancel dialog and an amend flow. It uses only the API built in specs 13 and 14. No backend change.
**Why now:** FR-11A to FR-11D are Musts and exist as API only. A controller cannot use them without screens (spec 13 and 14 section "Not built": every controller screen). The ten missing boards were drawn on 19 October in the Design canvas, so the screens can be built to a design.
**Who is this for:** Controller (Jayasinghe). No new role (D-69). Operators and passengers are unchanged.
**Success looks like:**
- Jayasinghe opens **Timetable** in the side menu and sees the weekly services, with the tab counts, in under 2 seconds.
- He adds weekly service 8072 through the three steps; an error from the server (a time going backwards, a platform of another station) is shown next to the stop it belongs to; advice about a platform within 10 minutes can be confirmed and does not block. After Schedule he lands on the service detail and the API shows the generated journeys.
- He cancels the service for one date with a reason; it appears in the Cancelled tab. Cancelling a date whose journey has reports shows the refused message and changes nothing.
- He amends Gampaha's time from Monday; the detail shows version 3 with "Gampaha arrives 08:42 (was 08:40)" and the old version stays in the list.
- Everything works with the keyboard, labelled, with status never shown by colour alone.

Size: **about 2.4 days**, cut into slices (section 12, Q1): **W0** shared client, types, menu, routes (0.25 day); **W1** list tabs and service detail, read only (0.5); **W2** new weekly service and extra train wizard (0.75); **W3** cancel dialog (0.4); **W4** amend (0.5). W0 to W2 is the minimum that makes FR-11A and FR-11D usable. Whatever is not built is stated as not built in the report.

## 1) Traceability
- Requirements: FR-11A (weekly schedule and extra services, edit before pressing Schedule, "Every stop" option), FR-11B (amend, versions, audit trail), FR-11C (cancel one date or from a date onward with a reason, never deleted), FR-11D (validation shown to the controller, advice only for platforms), FR-14 (station names shown with Sinhala beside the English), FR-107 (line selector, Should: only Main Line exists), NFR-10 (role check; the API checks every call), NFR-17 (the audit is written by the API; the screen names the actor), NFR-30, NFR-33 (labels, keyboard, contrast, 44 px targets), D-69, D-70, FR-63 (safety notice stays on screen).
- Acceptance criteria: the proposed criteria of specs 13 and 14 section 9 (AC-E02-03 onward, owner's call), seen from the screen. This spec adds screen criteria in section 9.
- Edge cases: the proposed table of spec 13 section 8 and spec 14 section 8, as they appear on a screen (section 8 here).
- Builds on: spec 08 (the controller web shell, polling, auth gate, `Button`, `Panel`, `Th`, `cell`, `ResolveDialog` pattern), spec 13 sections 4 to 6, spec 14 sections 4 and 6. API facts below were read from `TimetableEndpoints.cs` and `Timetable.cs` on 19 October.
- Design (Claude Design canvas, boards of 19 October): `Dash-Timetable`, `Dash-Timetable-Extra`, `Dash-Timetable-Cancelled`, `Dash-Timetable-Empty`, `Dash-ServiceDetail`, `Dash-NewService-Details`, `Dash-NewService-Stops`, `Dash-NewService-Review`, `Dash-NewService-Review-Errors`, `Dash-ExtraTrain`, `Dash-CancelService`, `Dash-CancelService-Blocked`, `Dash-AmendService`. Differences between the boards and what the API can do are in section 12 (Q2, Q6, Q7).

## 2) Goals and Non-Goals
### Goals
- Routes `/timetable` (tabs: weekly, extra, cancelled), `/timetable/new` (weekly), `/timetable/extra/new`, `/timetable/:serviceId` (detail), `/timetable/:serviceId/amend`; a **Timetable** item in the side menu of `OpsShell`.
- The wizard with draft kept in memory while the controller moves between steps; the server's `POST /timetable/validate` decides errors and advice (the screen never re-implements the rules).
- Cancel dialog (native `<dialog>`, like `ResolveDialog`) with the refused state; amend using the same step components as create.
- Error handling for every reason code of the API (section 4), in plain words from `strings.ts`.
- A small client change so POST answers can be read (Q10): the validate and create answers carry the data the screen needs.
### Non-Goals
- Any backend change. Any new API route.
- The other menu items in the design (Journeys, Overrides, Disruptions, Line closures): not built, not added to the menu.
- Telling passengers, refunds, caterer messages (not built, specs 13 and 14). The screens must not say or imply that passengers are told: the amend and cancel boards were drawn with honest copy for this reason ("Messages to passengers are not sent yet").
- Un-cancelling; editing or cancelling an individual journey (override, FR-54); amending an extra train (cancel and add another); services past midnight (the API refuses them).
- Operator assignment (FR-12) of the generated journeys: no screen here. A new service's journeys are Unassigned.
- Sinhala and Tamil text for the controller screens (spec 08 keeps English; station names show the Sinhala name from the API, `lang="si"`). A line selector (one line only). Playwright for amend (cancel and create only, Q1).

## 3) User Workflow
### Primary: new weekly service
1. **Timetable**, button **New weekly service**.
2. **Details:** train number, category (Regional / Long-distance), type (Slow / Express), first date it runs, days (M to S toggles). Next.
3. **Stops and times:** **Every stop** fills the line in order; **Choose stops** shows a checkbox per station (first and last always included and locked). A time and a platform per included stop. Next.
4. **Review and schedule:** the screen asks the server (`POST /validate`) and shows errors, or advice with the "I have read the advice" box, plus a read-only summary. **Schedule service** is off while there are errors, or advice that is not confirmed. On success: the service detail opens.
### Secondary
- **Add extra train:** same wizard; step 1 has dates instead of days (a list of date chips, today or later). Several dates create several services; the controller lands on the Extra trains tab with a message "2 extra trains added".
- **Service detail:** Stops, Versions (with the summary of each change), Cancellations (if any), Actions: **Amend service** (weekly only) and **Cancel service**.
- **Cancel:** dialog with "This date only" or "From this date onward", a date, and a reason (1 to 200 characters, counter). Confirm. If a journey has reports: the refused state naming the date, nothing changed.
- **Amend:** the wizard pre-filled with the latest version: "Effective from" (tomorrow or later), days, stops and times; then Review (the same step). On success the detail opens on the new version.
- **Empty states:** no extra trains, no cancelled services, nothing weekly (drawn for Extra trains; the other two follow the same pattern, Q6).
### Errors
Server unavailable or 5xx: the panel shows "Could not load the timetable. Try again" with a Retry button; a failed save keeps the form and the draft. 401/403: the shell already shows the session ended or no access screen.

## 4) API Surface (used, not changed)
All routes are `/api/v1/timetable/...`, controller policy. The screen sends the bearer token through the existing client; errors are ProblemDetails with `reason`.
| Screen need | Call | Notes |
|---|---|---|
| Station and platform choices | `GET /options?line=MAIN` | Line order; `names` has `en`, `si`, `ta`; `platforms` list; cached for the session |
| Tabs | `GET /services?line=MAIN&tab=weekly\|extra\|cancelled` | Weekly and extra return `ServiceRow[]`; cancelled returns `CancellationView[]` |
| Detail | `GET /services/{id}` | `stops[]`, `versions[]` (with `summary`), `cancellations[]`, `runsOn` (null for extra) |
| Check a draft | `POST /validate` | Always 200 with `{ valid, errors[], warnings[] }`, each `{ code, field, message }` |
| Schedule | `POST /services` | 201 `{ serviceIds, trainNumber, version, journeysGenerated, warnings }`; 422 `invalid_service` (with `errors`, `warnings`); 409 `warnings_not_acknowledged`, `train_number_in_use` (with `trainNumber`); 404 `line_not_found` |
| Cancel | `POST /services/{id}/cancel` | 200 `{ cancellationId, cancelledJourneys, from, to }`; 422 `invalid_reason`, `date_in_past`, `invalid_range`, `nothing_to_cancel`; 409 `journey_has_events` (with `date`); 404 `service_not_found` |
| Amend | `POST /services/{id}/amend` | 201 `{ serviceId, version, journeysUpdated, journeysCancelled, journeysGenerated, warnings }`; 422 `amend_extra_not_supported`, `service_ended`, `identity_changed`, `date_in_past`, `effective_date_not_after_current`, `invalid_service`; 409 `journey_has_events`, `warnings_not_acknowledged`, `train_number_in_use` |
Request bodies are those of spec 13 section 4.5 and spec 14 section 4: `kind`, `trainNumber`, `lineCode`, `category` (`Regional` or `LongDistance`), `serviceType` (`Slow` or `Express`), `days` (`Mon` to `Sun`), `dates`, `effectiveFrom`, `stops[]` (`stationCode`, `arrival`, `departure`, `platform`), `acknowledgeWarnings`. The exact enum spellings are taken from the API tests during the build, not from this spec.

**Client change (Q10).** Today `api.post` returns nothing and `ApiError` keeps only `status` and `reason`. The wizard needs the 200/201 body and the `errors`, `warnings`, `date` and `trainNumber` of an error body. So: `post` returns the parsed JSON (or `undefined` for an empty body) and `ApiError` gains `body: unknown`. The existing caller (resolve) ignores both, so it is unchanged.

## 5) Data Contract (screen side)
- `core/api/types.ts` gains the read types: `LineOptions`, `OptionsStation`, `ServiceRow`, `CancellationView`, `ServiceDetail`, `ServiceStopView`, `ServiceVersionView`, `ValidationIssue`, `ValidationResult`, `CreateResult`, `AmendResult`, `CancelResult`. Names and fields follow the API records; dates are `YYYY-MM-DD` strings, times `HH:mm` strings.
- Wizard draft (in memory, one `useReducer`): `{ kind, trainNumber, category, serviceType, days[], dates[], effectiveFrom, stopMode: 'every' | 'choose', stops: [{ stationCode, included, time, platform }] }`. It is turned into the request body in one function, tested alone.
- No local storage, no cookies, nothing about the draft leaves the browser until a call is made. No personal data is handled beyond the signed-in name already in the shell.
- Strings in `core/strings.ts` under `t.timetable`: tabs, step names, field labels, buttons, empty states, one sentence per API reason code, the safety footnotes. An unknown reason code shows the generic failure sentence.

## 6) Business Rules (what the screen does; the rules live in the API)
1. **The server decides.** The screen blocks only what it cannot send (train number empty, no day chosen, no date chosen, an empty time or reason, fewer than two stops included). It never blocks on FR-11D itself; it asks `POST /validate` and shows the answer. The API re-checks everything on Schedule.
2. **Errors beside their field.** An issue's `field` (for example `stops[2].arrival`) is mapped to the row and field it belongs to on step 2, and listed on the Review step with the same words. A summary with a count ("2 errors. Fix them to schedule.") is a `role="alert"` region. Colour is never the only sign: every error and advice row has an icon and a word.
3. **Advice is advice.** Warnings never disable Schedule by themselves; the checkbox "I have read the advice and want to schedule" must be ticked first, and then `acknowledgeWarnings: true` is sent. A 409 `warnings_not_acknowledged` (the data changed under the screen) returns the controller to the Review step with the new advice.
4. **Every stop and Choose stops.** "Every stop" selects all stations of the line in line order and shows no checkboxes. "Choose stops" shows a checkbox per station; the first and last of the selection are included and cannot be unticked. Stations that are not included show "Train does not stop here". Times and platforms are always entered.
5. **Times (Q2).** One "Time" box per included stop. First stop: a departure. Last stop: an arrival. A middle stop: an arrival, and the screen sends the departure as the arrival plus 1 minute (the API requires both for a middle stop, and requires the departure to be later). Format `HH:mm`, 24 hours, a text box with a pattern (Q4).
6. **Platform.** A list of the station's platforms from `/options`, plus "Not set" (null). Never a free text.
7. **Dates (Q3).** Native `<input type="date">`; `min` is tomorrow for an amendment and today otherwise. The server's controllable clock is authoritative: a 422 `date_in_past` is shown in words.
8. **Extra trains.** Each chosen date is sent in `dates`; no `days`. The same stops and times apply to every date.
9. **Amend only the latest version of a weekly service.** On an older version the Amend button is replaced by a link "Go to the current version". Extra trains show "To change an extra train, cancel it and add another" instead of Amend.
10. **Cancel.** Radio "This date only" sends `from = to = the date`; "From this date onward" sends `to = null`. The reason is trimmed, 1 to 200 characters, plain text, shown as text everywhere (never as HTML). The Confirm button is off while the call is out (one request on a double click).
11. **After a change** the screen invalidates the timetable queries and shows a status message (`role="status"`): "Service 8072 scheduled. 5 journeys created.", "Cancelled 1 journey.", "Version 3 saved. 3 journeys updated, 1 cancelled."
12. **Nothing is deleted** and no button says delete (D-70).
13. **Safety boundary (FR-63).** The shell's safety notice and footer stay; no wording suggests the screen controls trains or tells passengers something it does not (the Messages not sent notice stays on the cancel and amend screens until notification is built).
14. **No polling.** The timetable changes only when a controller changes it, so reads are fetched on opening a screen, on invalidation and on window focus (the query default); they are not part of the shell's polled freshness banner (spec 08).

## 7) Architecture (follow CLAUDE.md and the existing `web/ops` layout)
- `src/features/timetable/`
  - `queries.ts` (`useTimetableOptions`, `useTimetableServices(tab)`, `useServiceDetail(id)`, mutations `useValidate`, `useCreateService`, `useCancelService`, `useAmendService`; query keys under `['timetable', ...]`).
  - `TimetablePage.tsx` (header, buttons, tabs as nested routes like `ExceptionsPage`), `WeeklyTab.tsx`, `ExtraTab.tsx`, `CancelledTab.tsx`, `DaysChips.tsx` (read-only M to S chips with an accessible name "Runs on Mon, Tue, ...").
  - `ServiceDetailPage.tsx`, `VersionsPanel.tsx`, `CancellationsPanel.tsx`.
  - `wizard/` : `draft.ts` (reducer and `toRequest`), `NewServicePage.tsx` (weekly, extra and amend modes), `DetailsStep.tsx`, `StopsStep.tsx`, `ReviewStep.tsx`, `issues.ts` (map an issue `field` to a row), `Stepper.tsx`.
  - `CancelDialog.tsx` (native `<dialog>` modelled on `ResolveDialog`: focus in, Esc closes, the page behind is inert).
- `App.tsx` adds the routes inside the existing `OpsShell` route; `OpsShell.tsx` adds `<MenuItem to="/timetable" label={t.nav.timetable} />`. `core/api/client.ts` and `types.ts` change as in section 4. `core/ui.tsx` is reused (`Button`, `Panel`, `Th`, `cell`, `StatusChip`); two small shared pieces are added only if used twice (`Field`, `Callout`).
- Styling: Tailwind 4 with the existing token classes (`text-ink`, `text-muted`, `border-line`, `bg-page`, `bg-surface`, `text-warning`, `bg-warning-bg`, `touch` size). No new colour or size is invented; anything missing from the tokens is added in the token file and the Design canvas first.
- Accessibility: real `<button>`, `<a>`, `<label>`; each input has a visible label; the stepper is a nav with `aria-current="step"`; focus moves to the step heading when the step changes; the error summary receives focus when Review shows errors; tables have `<th scope="col">`; 44 px targets; the days toggles are checkboxes in a fieldset ("Runs on"), not coloured boxes.
- Telemetry: none in the browser (spec 08 has none); the API already counts the changes.
- Config: none new (`config.ts` already has the API base URL).

## 8) Edge Cases
| Case | What the controller sees |
|---|---|
| Server returns errors on Review | Error summary with count, the rows marked on step 2 and Review, Schedule off |
| Only advice | Advice callout, checkbox, Schedule on after ticking |
| Data changed so the 409 `warnings_not_acknowledged` arrives | Back to Review with the new advice, nothing created |
| 409 `train_number_in_use` | "Train 8072 already runs on these dates." with a link back to Details; nothing created |
| Create call fails (network, 5xx) | Message "Could not schedule. Nothing was saved. Try again", the draft is kept; a retry that finds the service already saved gets `train_number_in_use` |
| Two dates in an extra train, one already past | The date field refuses it with 422 `date_in_past`; nothing created |
| Cancel a date the train does not run on | 422 `nothing_to_cancel`: "The service does not run on any date in that range." |
| Cancel a date whose journey has reports | Refused state as drawn, names the date, "Nothing was changed.", Close and Try another date |
| Cancel the same range twice | The API answers 200 with 0 journeys; the screen says "Already cancelled. No journeys changed." |
| Amend: effective date not after the version's start | 422 `effective_date_not_after_current`, shown beside the date |
| Amend an older version or an extra train | No Amend button; the explanation instead (rule 9) |
| Two controllers amend at once | The second sees 422 or builds on the first (spec 14); the screen shows the reason and offers "Reload the service" |
| Browser back or Cancel in the wizard | The draft is dropped without asking (Q5); nothing was saved |
| Long list | The weekly list is short (one line); no paging. If rows exceed the page the table scrolls in its box on a phone width |
| Token expires while on a form | The shell's existing session-ended screen; the draft is lost (stated) |
| Reason with markup (`<b>x</b>`) | Shown as plain text in the Cancelled tab and detail |

## 9) Acceptance Criteria
- [x] **Timetable** appears in the side menu for a controller and opens the Weekly tab; the tabs show their counts and the Extra and Cancelled tabs list their rows (FR-11A, FR-11C).
- [x] The weekly list shows train, category, route, runs-on chips, first departure, stops label, version and an "Amended" chip for version 2 or later; the Cancelled tab shows train, route, date range, reason, who and when.
- [x] Empty states are shown for an empty Extra trains and an empty Cancelled tab, with the Add extra train action on the first.
- [x] A controller can create a weekly service through the three steps; "Every stop" and "Choose stops" both work; first and last stop cannot be unticked; after Schedule the detail opens with the stops and version 1 (FR-11A).
- [x] Errors from the server are shown beside the stop and field they belong to and in the Review summary; Schedule is off while there are errors (FR-11D).
- [x] Advice about a platform near another train is shown, needs the checkbox, never blocks (FR-11D, FR-57).
- [x] A controller can add an extra train for one or more future dates and lands on the Extra trains tab with a message.
- [x] The cancel dialog sends this-date and from-date-onward correctly, needs a reason of 1 to 200 characters, and shows the refused state naming the date when the API answers `journey_has_events`; nothing changes in that case (FR-11C).
- [x] After a cancel the service shows its cancellation on the detail and in the Cancelled tab.
- [x] A controller can amend a weekly service from tomorrow or later; the detail then shows the next version, the old one stays in Versions, with the summary text from the API (FR-11B, D-70).
- [x] No screen offers a delete, none says passengers are notified, and the "not sent yet" notice is shown on cancel and amend.
- [ ] The whole flow works with the keyboard only; focus moves sensibly on step change, dialog open and close; every field has a visible label; error and advice never rely on colour alone; targets are 44 px.
- [x] An operator token on the web app still sees "No dashboard access" (spec 08); the Timetable routes are not reachable without the controller role.
- [x] `npm run lint`, `npm run typecheck`, `npm test` and `npm run build` pass; the existing 30-plus web tests still pass.
- [x] A Playwright test against Compose creates a unique service as `jayasinghe`, finds it in the list, cancels one date and finds it under Cancelled (`web/ops/e2e/timetable.spec.ts`, passed on 19 Oct; it schedules the service through the API, then uses the screens). It writes screenshots of the detail, the cancel dialog and the Cancelled tab to `test-results/` (not committed); Review with errors is not screenshotted.
- [x] The report states plainly which of W0 to W4 were built, that passengers are not notified, that operator assignment of generated journeys is not part of this, and that sample data in the designs was invented.

## 10) Test Plan
### 10.1 Fixture data
A fake API (the pattern of `OpenExceptionsTab.test.tsx` and `helpers.tsx`) with: the five-station options, a weekly list of four rows, an extra list of two, a cancelled list of one, a detail with two versions and one cancellation, validate answers (valid; two errors; one advice), create answers (201, 422, 409 both reasons), cancel answers (200, 409 with a date, each 422), amend answers. Real stack for the end to end test (Compose, dev Keycloak passwords, like `controller-flow.spec.ts`).
### 10.2 Unit tests
`draft.ts` (`toRequest` for weekly, extra, amend; every stop vs choose stops; the departure = arrival + 1 minute rule across an hour boundary; first and last locked), `issues.ts` (field to row mapping for `stops[2].arrival`, `stops[1].platform`, `days`, `effectiveFrom`; unknown field goes to the summary), reason-code wording (every API reason has a sentence; an unknown one falls back), `client.ts` (post returns the body; `ApiError.body` has the problem details; the resolve call unchanged).
### 10.3 Component tests (Vitest, Testing Library, user-event)
Tabs and counts, empty states; wizard: step order, required-field blocks, Every stop and Choose stops, locked first and last, platform lists per station, Review with errors (Schedule off, rows marked), Review with advice (checkbox then Schedule on, `acknowledgeWarnings` sent), 409 and 422 handling, extra train with two dates; detail with two versions and a cancellation; cancel dialog (radios, counter at 200, Esc closes, double click sends one request, refused state names the date); amend (not shown on an older version or an extra; effective date min is tomorrow; summary shown after success); keyboard order and focus targets; an operator token cannot reach the routes.
### 10.4 End to end (Playwright)
One test: sign in as `jayasinghe`, New weekly service with a unique number, Schedule, see the detail, open the list, cancel one future date with a reason, see it under Cancelled; then the API read as `kasuni` shows the train absent for that date. Run against Compose; documented in the web README. Amend stays a component test only (Q1).

## 11) References
- Specs 08 (controller web), 13 and 14 (API and rules), 09 (patterns for screens), 02 (roles). `docs/smartrail-refined-requirements.md` FR-11A to FR-11D, FR-14, FR-63, NFR-30, NFR-33. Design canvas boards listed in section 1. `web/ops/src/features/exceptions/` (list, tabs, dialog pattern), `web/ops/src/core/` (client, ui, strings, time), `web/ops/e2e/controller-flow.spec.ts`.

## 12) Open Questions (recommendation first)
- **Q1. Scope and time.** About 2.4 days; the deadline is tomorrow and the test strategy, threat model, privacy note, AI usage write-up and demo story are also open. Recommend W0 to W2 (list, detail, create), then W3 (cancel) only if time allows; amend (W4) last. If W2 is not built, nobody can use FR-11A without calling the API. Tell me the real budget and I will reduce the spec to match.
- **Q2. One Time box, but the API needs two times for a middle stop.** The boards show a single "Time" per stop. The validator requires both an arrival and a departure at a middle stop and the departure later than the arrival, so one time cannot be sent. Recommend: the box is the arrival, and the screen sends departure = arrival + 1 minute (rule 5), shown to the controller as "Leaves 1 minute later" in the detail. Alternative: two boxes (Arrives, Leaves) per middle stop, which changes the Stops step board. Decide before the build because it changes the design and the detail table.
- **Q3. Date input.** Recommend the native date input (accessible, keyboard, no custom picker to build), unlike the board's field with a calendar icon. The look is the browser's; the label, size and border follow the tokens.
- **Q4. Time input.** Recommend a text box with `HH:mm`, a pattern and a hint "24 hours", because the native time input follows the browser's locale (AM/PM) and a controller working in 24 hours would be surprised. The server validates; a wrong format shows the server's message.
- **Q5. Unsaved draft.** Recommend not asking when the controller leaves the wizard (the draft is dropped). A "Discard changes?" dialog is more to build and test; the drafts are short.
- **Q6. Boards versus what is built.** Differences to settle (my recommendation in brackets): (a) the Service detail board says passengers are told when a service with tickets changes: ticketing and notification do not exist, so reword to "Services are never deleted. Changes create a new version." [yes, I edit that board]; (b) the Amend board has no days control but the API needs the whole service: add "Runs on" toggles to the amend board [yes, small]; (c) there is no board for the success message, a loading state, a failed load or the empty Weekly and Cancelled tabs [build from the existing patterns and flag them as undrawn]; (d) the Review board does not show the extra-train summary with several dates [list the dates in the summary]; (e) no mobile width is drawn [the shell is already responsive; the tables scroll in their box].
- **Q7. Amend scope on screen.** Recommend the amend form edits days, stops, times and platforms only; category, type, train number and line are shown read-only (the API refuses a changed identity). Alternative: allow category and type changes (the API accepts them but treats identity as train number, line and kind).
- **Q8. Menu.** Recommend adding only **Timetable** to the side menu. The design shows Journeys, Overrides, Disruptions and Line closures; they are not built, and a dead item is worse than none.
- **Q9. Journeys after Schedule.** The new service's journeys are Unassigned and nobody can report on them until FR-12 exists. Recommend the success message states the count of journeys created and nothing about operators, and the report says assignment is separate. Alternative: a small assignment screen as the next spec (operator assignment is a Must).
- **Q10. Client change.** Recommend extending `post` to return the body and `ApiError` to keep the problem details (section 4), a few lines with tests. Alternative: a second `postJson` method and leave `post` alone (no risk to the existing caller, slight duplication). Either is small; I prefer the first.

## 13) Owner decisions of 19 Oct 2026
- Scope: **W0 (client, types, routes, menu), W1 (list tabs, detail) and W2 (new weekly service and extra train wizard)**; W3 (cancel dialog) only if time allows; W4 (amend) not built unless time remains, and then stated as not built. Q2 to Q10 as recommended.
- Branch `claude/feature/timetable-web` starts from `dev`, which already holds specs 10 to 14.

## 14) Delivery notes for W0 to W2 (19 Oct 2026)
- **Built:** W0 (client returns the body and `ApiError.body`, timetable types, strings, menu item, routes), W1 (Weekly, Extra and Cancelled tabs with counts, empty and failed states, service detail with stops, versions, summaries and cancellations, read only) and W2 (the three-step wizard for a weekly service and an extra train: Every stop and Choose stops with locked first and last, platform lists, Review asks `POST /validate`, errors beside the stop and in a summary with focus, advice with a confirm box, 409 and 422 handling, a message and the detail or the Extra tab after Schedule). 177 web tests pass (22 new for the timetable, 3 new for the client, lint, typecheck and build clean).
- **Not built:** W3 (cancel dialog) and W4 (amend); the detail therefore shows no Cancel or Amend button (no button that goes nowhere); the Playwright test and screenshots; passenger notification, refunds, caterer messages, operator assignment (generated journeys are Unassigned and the detail says so).
- **Decisions while building:** the wizard has a Direction choice (toward or away from Colombo Fort) because the API derives the direction from the first and last stop and the boards did not show how a controller picks it; the middle-stop departure is sent as the arrival plus 1 minute (Q2); the weekly list shows the newest version of each train number. The server's own message is shown for each error; the screen words only the codes it can not explain.
- **Local note:** `scripts/tokens-generator.test.mjs` fails in a Windows checkout where `tokens.css` has CRLF line endings; running `npm run tokens` fixes the working copy and changes nothing in git. CI (Linux) is not affected.
- **Not ticked:** the keyboard-only, focus and 44 px criterion (focus moves and every field is labelled in tests, but it was not tried by hand), the Playwright criterion and the report criterion.

## 15) W3 and W4 (19 Oct 2026, built after the first delivery)
- **Built:** the cancel dialog (this date only or from a date onward, reason 1 to 200 characters with a counter, the not-sent notice, the refused state naming the date, plain words for each 422, "Already cancelled"), and amend (the wizard filled with the newest version of a weekly service, train number, category and type read only, effective date tomorrow or later, its own route, the new version and the counts after saving, plain words for a blocked or refused amendment). The service detail now has Cancel service and Amend service (a link to the current version on an older one, an explanation for an extra train). Also: Next adds an extra-train date that was picked but not added. 194 web tests pass.
- **Still not built:** the Playwright test and screenshots, and the hand check of keyboard use and 44 px targets. Passengers are still not notified (the screens say so).

## 16) Closing report (19 Oct 2026)
- **Built:** W0 to W4: client and types, the three tabs and the service detail, the three-step wizard (weekly and extra), the cancel dialog and amend.
- **Not built / not done:** passengers are not notified (the screens say so). Operator assignment of generated journeys is spec 17, not this one. The sample data in the designs was invented. The keyboard-only, focus and 44 px criterion was **not checked by hand** and stays unticked.
