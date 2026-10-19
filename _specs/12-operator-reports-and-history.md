# Feature Spec: Operator app, Reports tab and previous journeys

branch: claude/feature/operator-reports-history
spec: _specs/12-operator-reports-and-history.md
owner: tharindu
date: 2026-10-16
status: draft

---

## 0) Summary
**One-liner:** Add the third tab, **Reports**, to the operator app. It has two parts. "My reports" lists every report Nimal has made on this phone with its delivery state (Delivered, Not sent yet, Rejected) and opens a detail screen, as drawn in `Nimal-Reports` and `Nimal-RejectedDetail`. "Previous journeys" lists the journeys Nimal worked on earlier, each opening a read-only record of the stops and of his own reports. The first part needs no backend. The second needs two small read endpoints.
**Why now:** The tab bar in the design has three tabs and the app has two (spec 05 Q3 left Reports out because there was no "my reports" endpoint). FR-25 (each report shows its delivery status) is a Must and only the single "Last report" card shows it today. The "previous journeys" part is new in this spec: the design does not draw it.
**Who is this for:** Operator (Nimal). Nobody else can see another operator's journeys.
**Success looks like:**
- With three reports sent (one Delivered, one waiting offline, one refused), the Reports tab lists all three newest first with the right chip, and tapping the refused one opens "Needs Operations review" (AC-E03-05 1, 2, 4).
- After the journey is completed, it appears in "Previous journeys" with train, route, date and the number of reports Nimal made. Opening it shows each stop's scheduled and actual time and his reports with their times.
- Another operator cannot open that journey: the API answers 404 (EC-27 spirit, NFR-10).
- Offline, the report list still works and the journey list shows the last copy with "Last updated 08:42".

Size: medium, about 1.25 days: backend 0.5 (two read endpoints, tests), app 0.75 (tab, list, detail, local retention, tests). The "My reports" half alone is about 0.4 day and can ship first (Q8).

## 1) Traceability
- Requirements (wording read in `docs/smartrail-refined-requirements.md` v1.13): FR-25 (delivery status per report), FR-31 (queue kept, never discarded), FR-13 (time-bounded assignments, used to decide whose journey it was), FR-12, FR-20, FR-38, NFR-10 (role checks), NFR-05, NFR-30 (never colour alone), NFR-32, NFR-33, NFR-17 (audit trail, read only here). FR-27 (flag my last report) is **not built** and not part of this spec.
- Acceptance criteria: AC-E03-05 (1 to 6, shown in the list and detail), AC-E01-03 (own journeys only, extended to history). The previous-journeys part has **no existing acceptance criterion**; section 9 proposes new ones (Q5).
- Edge cases: EC-01, EC-12, EC-19, EC-21, EC-23, EC-24, EC-27 (looked up in `docs/smartrail-edge-cases.md`). EC-07 is relevant to what the Rejected screen may say (section 6, rule 5).
- Decisions: D-35 (client event ID), D-36 (audit trail), D-41 (flag, not built), D-10 (reason only after verification: the operator sees his own report, not a published reason), spec 05 Q2 (storage: drift/SQLite), spec 05 Q3 (Reports tab), spec 07 Q1 (rejected conflicts are not stored).
- Design: `Nimal-Reports`, `Nimal-RejectedDetail` (drawn, built here). `Nimal-NoLongerAssigned` links to this tab ("View my reports") but is **not** built here (Non-Goals). Previous journeys: **not drawn** (gaps G1 to G5, Q4).

## 2) Goals and Non-Goals
### Goals
- Third tab "Reports" in the bottom bar, matching `Nimal-Reports` for the list and `Nimal-RejectedDetail` for the detail of a refused report; the same detail pattern for Delivered and Not-sent reports.
- "My reports": all reports kept on this phone, newest first, live-updating as the sender works (a "Not sent yet" row turns "Delivered" without a refresh).
- "Previous journeys": list and read-only detail, from two new endpoints that return only the signed-in operator's own journeys.
- Local housekeeping: delivered reports older than 7 days are deleted from the phone; waiting and refused ones are never deleted (FR-31).
- The wording of the Rejected screen made true to what the system really does (section 6, rule 5).
### Non-Goals
- **Flagging the last report (FR-27, AC-E03-06):** still its own later slice; there is no flag button in the detail.
- **Rejected reports reaching the controller.** Spec 07 Q1 decided that refused reports are not stored on the server, so no controller queue entry exists for them. This spec does not change that; it changes the words on the screen instead (rule 5, Q1).
- `Nimal-NoLongerAssigned`: the server answers 404 `no_assignment_now` both for "never assigned" and "assignment ended", so the app cannot tell the two apart. That needs a field in a later change. This tab already holds what its button would open.
- Editing, deleting or re-sending a rejected report; undoing a delivered one.
- Search, filters, export, statistics, or other operators' journeys. No new role (the roles stay Passenger, Operator, Controller, Caterer, System Administrator).
- Push notifications, tablet layouts, iOS, Sinhala and Tamil (English only, strings in the one file, as spec 05).
- Rate limiting (NFR-13 is deferred everywhere); the app calls the history endpoints only when the Journeys part is opened or pulled to refresh.

## 3) User Workflow
### Primary workflow: check my reports
1. Nimal taps **Reports**. "My reports" is shown first: rows like `Delay: Signal issue, ~10 min · Delivered · 08:41`, `Arrived Gampaha · Not sent yet · 08:40`, `Departed Veyangoda · Rejected · 08:31`.
2. He taps the refused row. The detail says it needs Operations review, which report it conflicted with when known, and gives the phone number of the Main Line desk (the Help tab number).
3. He goes back; the row for the report that was waiting has changed to Delivered on its own.
### Secondary workflow: previous journeys
1. He taps the **Previous journeys** segment. A list of journeys, newest first: `8058 Veyangoda to Colombo Fort · Thu 15 Oct · Completed · 7 reports`.
2. He taps one. The detail shows the stops with scheduled time and actual arrival and departure, and "Your reports" (type, station, time, delay reason and total), all of which the server recorded. A note says the record cannot be changed.
- **Offline:** "My reports" works fully (it is on the phone). "Previous journeys" shows the last copy and "Last updated 08:42". A journey detail not opened before says it needs a connection.
- **Nothing yet:** both parts have an empty state (G1, G2).

## 4) API Surface
Both routes are new, read-only, `RequireAuthorization(Policies.Operator)`. They use the token subject (`sub`) only; no id of a person is taken from the request.

### 4.1 `GET /api/v1/operator/journeys/history?limit=20&before={instant}`
- Returns the journeys this operator was assigned to that are **past**: the journey is Completed, or this operator's assignment of it has ended (`valid_to` in the past), and not the journey in progress for him now (that one is the Journey tab). Newest assignment first.
- `limit` 1 to 50 (default 20). `before` is an optional UTC instant: only assignments that started before it (keyset paging, no offset). The retention is whatever the database holds; no purge exists.
- Response `200`:
```json
{
  "serverTime": "2026-10-16T08:42:03Z",
  "journeys": [
    {
      "journeyId": "0199...",
      "trainNumber": "8058",
      "fromName": "Veyangoda",
      "toName": "Colombo Fort",
      "operatingDate": "2026-10-15",
      "status": "Completed",
      "assignedFrom": "2026-10-15T02:40:00Z",
      "assignedUntil": null,
      "myReportCount": 7,
      "arrivalDelayMinutes": 10
    }
  ],
  "next": "2026-10-15T02:40:00Z"
}
```
- `status` is the journey's status (`Completed`, or the journey's current state when only the assignment ended). `assignedUntil` is null when he stayed to the end. `myReportCount` counts events created by him (`Departed`, `Arrived`, `DelayReported`) in this journey. `arrivalDelayMinutes` is the actual minus the scheduled arrival at the final stop, null when not yet arrived. `next` is null on the last page.
- Errors: 401 no/invalid token, 403 wrong role, 400 `invalid_limit` or `invalid_before` (ProblemDetails).

### 4.2 `GET /api/v1/operator/journeys/{journeyId}/record`
- Returns the record of one journey **only if** an assignment of this operator exists for it (any time window). Otherwise 404 `journey_not_found` (the same answer for "does not exist" and "not yours", so nothing leaks, EC-27).
- Response `200`:
```json
{
  "serverTime": "2026-10-16T08:42:03Z",
  "journeyId": "0199...",
  "trainNumber": "8058",
  "operatingDate": "2026-10-15",
  "status": "Completed",
  "stops": [
    { "stopId": "...", "stationName": "Veyangoda", "sequence": 1,
      "scheduledArrival": null, "scheduledDeparture": "2026-10-15T02:50:00Z",
      "actualArrival": null, "actualDeparture": "2026-10-15T02:58:00Z", "confirmation": "Reported" }
  ],
  "myReports": [
    { "eventId": "...", "type": "Departed", "stationName": "Veyangoda",
      "occurredAt": "2026-10-15T02:58:00Z", "receivedAt": "2026-10-15T02:58:04Z",
      "delayReason": null, "totalDelayMinutes": null }
  ]
}
```
- `stops[].confirmation` is `Reported`, `Inferred` or `Pending` (the same meaning as the status read, P-07). `myReports` is only events created by this operator, in occurrence order. A delay report carries `delayReason` (the operator's own input, shown to him; it is not a published reason, D-10) and `totalDelayMinutes` (null means unknown).
- No journey data is changed; no audit entry is needed for a read (Q6).
- Example: `curl http://localhost:8080/api/v1/operator/journeys/history -H "Authorization: Bearer $NIMAL_TOKEN"`.

## 5) Data Contract
- **No migration, no new table.** Reads `operator_assignment` (`operator_user_id`, `valid_from`, `valid_to`, journey), `journey` (status, operating date, service), `train_service` and `service_stop` (names, scheduled times), `journey_stop_state` (actual times, confirmation) and `train_event` (`created_by`, `event_type`, `occurred_at`, `received_at`, `payload`). The existing indexes `ix_assignment_operator` and `ix_assignment_journey` serve the queries; check the query plan on a database with a few hundred journeys (Q7).
- **On the phone (drift, version 2 to 3):** no new table. The `reports` table already holds every report with its state and `lastReason`. One new column or lookup is needed for the detail screen of a refused report: the conflicting stop time is **not stored**, so the app derives it from the last status read of the journey when available (rule 5). The journey list and its fetch time are cached as one JSON value in the existing `KeyValues` table. A cleanup runs at start and when the Reports tab opens: delete rows with state delivered and `occurredAt` older than 7 days.
- Demo data: the seeded 8058 journey for today and the operator Nimal. To show a previous journey, complete the journey once (or add a one-off demo journey for yesterday in the demo seeder, Q7).

## 6) Business Rules
1. **Own journeys only (NFR-10, EC-27).** Both endpoints use the token subject. The history lists only journeys with an assignment for that subject. The record returns 404 otherwise. A controller or another operator cannot read it with this route (policy `Operator` only).
2. **Past means finished for him.** A journey is past when it is Completed, or his assignment has ended. A journey in progress under his current assignment is not in the list (the Journey tab is its home). With a crew change mid-journey (FR-13), the earlier operator sees the journey in history with `assignedUntil` set and only his own reports (EC-21).
3. **His reports only.** `myReports` and `myReportCount` come from events he created. Stops show everyone's actual times because they are the journey's facts. Another operator's reports are never listed.
4. **Delivery state (FR-25, AC-E03-05).** "My reports" shows the three states with text, an icon and an outline style, never colour alone (NFR-30): Delivered (check), Not sent yet (dashed outline), Rejected (flag, heavy outline). A pending report that is being retried still says "Not sent yet" (AC-E03-05 2). A duplicate answered by the server shows Delivered (AC-E03-05 3).
5. **What the Rejected screen says (true to the system).** The board says "A controller will review it", but refused reports are not stored or shown to controllers (spec 07 Q1). So the screen must not say that. Proposed wording: title "Not accepted", body "The server did not accept this report because it conflicts with what is already recorded." plus, when the journey's last status read shows the conflicting time, the second line "Recorded: Departed Veyangoda at 08:28." and "You don't need to send it again. Phone the Main Line desk if the train's status is wrong." The chip keeps the required words "Rejected, requires Operations review" only if a controller can actually see it; since one cannot, the chip reads **"Rejected"** and the line under the title says Operations is **not** told automatically. This changes the wording of AC-E03-05 4 and FR-25; the report must say why (Q1). The reason codes map to plain text: `order_conflict`, `stop_has_different_time`, `delay_not_greater`, `not_arrival_station`, `final_stop`, `stop_not_in_service`, `time_out_of_range`, `journey_not_reportable`, `not_assigned`, `client_event_id_reused`, `invalid_request` (all read from `JourneyEndpoints.Describe`), and a generic line for any other code (never the raw code).
6. **Order and live update.** Newest `occurredAt` first; ties by creation order. The list is a stream from the queue, so a state change appears at once. Reports never change order because of their state.
7. **Kept, never lost (FR-31, AC-E03-05 5, 6).** Waiting and refused reports are never deleted. Delivered reports are deleted from the phone after 7 days, because the server has them and the history shows them. Sign-out keeps the queue (spec 05).
8. **Previous journeys are a record, not a control.** No button changes or re-sends anything. The detail says it cannot be changed.
9. **Offline and loading (NFR-07).** The journey list shows its cached copy with "Last updated HH:mm" (the server time of that fetch, not the phone clock). Without a cache and without a connection: "Connect to see your previous journeys". A failed refresh keeps the old list and says so. A journey detail is fetched when opened and not cached.
10. **Fetch rules.** The journey list is fetched when the segment is first opened in a run of the app, on pull-to-refresh, and when a journey completes. Never on a timer. One request at a time. Page size 20, a "Show earlier" row uses `next`.
11. **Times.** All times are shown in Asia/Colombo from the server's instants, using the server-corrected clock the app already keeps (spec 05).
12. **Wording is English, one strings file;** no new role, no new permission.

## 7) Architecture (follow CLAUDE.md and ADR-15)
- **Domain/Application (backend):** `GetOperatorJourneyHistoryHandler` and `GetOperatorJourneyRecordHandler` in `Features/Journeys`; read-only methods on `IJourneyRepository` (`GetHistoryAsync(subject, before, limit)`, `GetRecordAsync(subject, journeyId)`), implemented with `AsNoTracking` queries in `JourneyRepository`. The delay of the final stop is calculated with the same pure functions the status read uses; no new rule.
- **Api:** `JourneyEndpoints` gains two `MapGet` routes under the `Operator` policy; add them to the allow-list test in `AuthConfigurationTests` as authenticated routes.
- **Telemetry:** counter `smartrail.operator.history.reads` with attribute `kind` (`list`, `record`) and `outcome` (`ok`, `not_found`); log the subject through `LogSafe.Clean`, never the token.
- **App (`mobile/operator_app`):** `lib/features/reports/presentation/reports_screen.dart` (segmented control, the two lists), `report_detail_screen.dart`, `lib/features/history/{domain,data,presentation}` (models, `HistoryApi` interface and Dio implementation, repository with the cached list, controller, `journey_record_screen.dart`). `AppBottomBar` gets a third tab (Journey, Reports, Help); the shell keeps its `IndexedStack`. Routes `/report/:clientEventId` and `/history/:journeyId`. Riverpod providers and fakes in tests, as the rest of the app. The queue gets `watchAll()` (stream of every report, newest first) and `deleteDeliveredBefore(DateTime)`.
- **Design:** build the two drawn boards pixel-close; the undrawn screens are drawn first or built from the same components (Q4).

## 8) Edge Cases
| EC | Case | Handling |
|---|---|---|
| EC-01, EC-19 | Offline, queue retrying | Row stays "Not sent yet" and turns Delivered when sent; never dropped (rule 7) |
| EC-12 | Next action derived from the queue | Unchanged; the tab only reads the queue |
| EC-21 | Offline report from a previous operator syncs after handover | He sees it in his own list as Delivered; the High exception for a post-handover report (AC-E04-02 3) is not built; spec 07 does not raise it |
| EC-23 | Device lost, assignment ended | Earlier reports stay on the server; a new device sees them in Previous journeys after sign-in; the lost phone's local list is not reachable |
| EC-24 | Token expired offline | Reports tab still works (local); history says it needs sign-in if the token cannot refresh |
| EC-27 | Opens a journey he was never assigned | 404 `journey_not_found` |
| (new) | Same operator, two phones | "My reports" is per phone; Previous journeys is the same on both (server) |
| (new) | Report refused because the journey is Completed (`journey_not_reportable`) | Rejected detail with the plain reason |
| (new) | No reports yet / no previous journeys | Empty states G1, G2 |
| (new) | History request fails, cache exists | Cached list with "Last updated" and a retry |
| (new) | Journey detail with a skipped stop | Stop shows "Passed (inferred)" with no actual time (P-07) |
| (new) | 100 delivered reports older than 7 days on the phone | Deleted at the next start; nothing else removed |
| (new) | Text scaled to 200 % | Rows grow; chip and time never clipped (NFR-33) |

## 9) Acceptance Criteria
- [ ] The bottom bar has Journey, Reports and Help; Reports opens "My reports" (AC-E03-05 1).
- [ ] A delivered, a waiting and a refused report each show the right chip with text and icon, newest first (AC-E03-05 1, 2, 4; NFR-30).
- [ ] A waiting report turns Delivered in the list without leaving the screen (AC-E03-05 2).
- [x] A report kept waiting across an app restart is still listed and still sent (AC-E03-05 6).
- [ ] The refused-report detail says what conflicted when known and never claims a controller will review it; it offers the desk phone number (rule 5).
- [ ] Delivered reports older than 7 days are removed from the phone; waiting and refused ones are not (test with a fake clock).
- [x] `GET /operator/journeys/history` returns only this operator's past journeys, newest first, with `myReportCount` and `arrivalDelayMinutes`; paging works with `before` and `limit`; bad parameters give 400.
- [x] `GET /operator/journeys/{id}/record` returns the stops and only this operator's reports; a journey of another operator or a missing one gives 404 with the same body.
- [x] No token gives 401; a controller, passenger or caterer token gives 403 on both routes.
- [x] A journey in progress for this operator is not in the history; a completed one is.
- [ ] After a crew change the earlier operator sees the journey with `assignedUntil` and only his reports.
- [x] Offline, "Previous journeys" shows the cached list with "Last updated"; with no cache it says it needs a connection; "My reports" is unaffected.
- [x] The previous-journey detail is read-only and says so.
- [ ] Screen reader order is chip text, report, station, time; text at 200 % does not clip (NFR-32, NFR-33).
- [ ] `dotnet test`, `flutter analyze --fatal-infos` and `flutter test` are green; end to end on the emulator with screenshots.

## 10) Test Plan
### 10.1 Seed and fixture data
Operators Nimal and a second operator token; the seeded 8058; journeys: one in progress, one Completed yesterday with 7 events by Nimal, one reassigned mid-way (assignment ended, events by two operators), one never assigned to Nimal. Phone fixtures: 3 reports (delivered, pending, rejected), 100 old delivered rows, a v2 database file for the migration test.
### 10.2 Unit tests
Backend: past/in-progress selection from assignment windows, report counts per subject, arrival delay calculation, paging cursor, parameter validation. App: report row model (chip text per state), reason-code mapping (including an unknown code), retention rule, the sorting rule, cached-list freshness text.
### 10.3 Integration tests (Postgres container)
History and record happy paths; another operator gets 404 on the record and sees none of Nimal's journeys in the list; controller/passenger/caterer 403; no token 401; crew change; `limit` and `before` boundaries; query count does not grow with the number of journeys (no N+1; Q7).
### 10.4 Widget and end-to-end
Reports tab with a fake queue stream (state change updates the row); detail screens for each state; segmented control; offline and empty states; drift migration test from the v2 file. Emulator run: report, go offline, report, come back, refused report via a conflicting second departure, complete the journey, open it under Previous journeys (screenshots in the PR, as spec 05).

## 11) References
- Specs 05 (app structure, queue, Q2, Q3, tab bar), 06 (reason codes, completion), 07 (Q1: refused reports are not stored), 03 (events, assignment checks); `docs/smartrail-acceptance-criteria.md` E03; FR-25, FR-27, FR-31; ADR-11, ADR-15.
- Existing code reused: `ReportQueue` and `DriftReportQueue`, `delivery_chip.dart`, `report_label.dart`, `JourneyRepository`, `OperatorAssignment`, the status builder's delay functions, `Strings`, `AppBottomBar`.

## 12) Open Questions (recommendation first)
- **Q1. The Rejected screen cannot honestly say a controller will review it.** The board and AC-E03-05 4 say "requires Operations review", but spec 07 Q1 stores nothing for refused reports, so no controller ever sees them. Recommend: change the wording as in rule 5 (the report is not accepted; Operations is not told automatically; phone the desk) and record the deviation from FR-25 and the board. Alternative: store refused reports as events with an outcome and raise an exception (a backend change to specs 06 and 07, about a day, touching the order rules). I would not do it before 20 October.
- **Q2. One tab or two.** Recommend one Reports tab with two segments ("My reports", "Previous journeys"), so the bar keeps the three tabs drawn. Alternative: a fourth tab (breaks the design) or previous journeys under Help (hard to find).
- **Q3. Is "My reports" server data or phone data?** Recommend phone data: it must show Not sent yet and Rejected, which the server never sees. Previous journeys is server data. The cost is that two phones have different "My reports" lists; stated.
- **Q4. Undrawn screens.** Previous journeys has no board. Recommend I draw five before the build: G1 My reports empty, G2 Previous journeys empty, G3 Previous journeys list (and the segmented control on the Reports board), G4 journey record, G5 offline with cached list. Otherwise I build from existing components and flag it as undesigned in the report.
- **Q5. New acceptance criteria.** The previous-journeys part has no AC in the documents. Recommend adding three (own journeys only; record is read-only; offline cached list) to E03 or a new E03 item, dated honestly as added on 16 October. This is your call, since it changes the requirements baseline.
- **Q6. Audit of reads.** Recommend no audit entry for these reads (they change nothing, and the audit trail is for events). Alternative: log each record read (privacy-friendly but adds a table).
- **Q7. Demo data and performance.** Recommend a one-off demo journey for yesterday in the demo seeder (idempotent) so Previous journeys is not empty in the demo, and a quick query-plan check with a few hundred rows. Alternative: complete today's journey by hand before each demo.
- **Q8. Cut order and time.** About 1.25 days. If time is short, ship "My reports" first (0.4 day, no backend, closes FR-25 in the app) and then Previous journeys. Cut within it: paging (`before`), the offline cache of the list, the 7-day cleanup. Never cut: own-journeys-only (404), the three states with text and icon, the honest Rejected wording.

- Update (19 Oct): the owner showed the intended design of the tab: reports grouped by journey, each group a collapsible card (route, train, report count, waiting count, date; the newest open). Built that way for the reports on this phone, using a small summary of each journey kept on the phone (train, route, operating day) so older groups keep their heading. Older journeys here show only the reports still on the phone (delivered ones for 7 days); the server-side Previous journeys (part 2) is still not built.

## 15) Part 2 delivery notes (19 Oct 2026)
- Done: `GET /api/v1/operator/journeys/history` and `/{id}/record` (operator only, own journeys only, same 404 for missing and not yours, 400 for a bad `limit` or `before`, a fixed number of queries per page), counter `smartrail.operator.history.reads`, 12 integration tests, the Previous journeys segment (list, Show earlier, copy kept for offline with Last updated, record screen) and 6 app tests (152 in all). The demo seeder adds yesterday's finished 8058 with three reports by nimal (Q7) unless a journey for that day exists already.
- Not done: crew change test (an event created by a second operator needs a second operator with events; the query filters on the creator, so the rule is in the code but not proved by a test), the query plan check on a few hundred journeys (Q7), a controller or passenger can never call it (proved), emulator run with screenshots. The previous-journeys screens are built from existing components, not from boards.
- Side fix: the startup test of the demo seed expected exactly one journey since spec 10; it now checks at least.
