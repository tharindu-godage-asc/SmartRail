# Feature Spec: Setting the platform of a stop (spec 16 F4, reduced)

branch: claude/feature/platform-assignment
spec: _specs/20-platform-assignment.md
owner: tharindu
date: 2026-10-21
status: approved (owner asked for it on 21 Oct 2026; built the same day)

---

## 1) Problem
A train scheduled with no platform (the timetable wizard allows "Not set") appears on the main boards with a dash, but **never on a platform display**, because a platform display lists only the trains whose platform equals its own. The controller had no way to set a platform on a journey. This is the platform change of spec 16 F4 (FR-57), built here in a reduced form.

## 2) Scope
Built: set or clear the platform of one stop of one journey, with advice about other trains on that platform; a Set platform control on the journey detail page; a hint in the timetable wizard; "TBA" on the main board for a train with no platform. Not built (still spec 16 F4): the automatic **Platform conflict** exception when estimates change (rule 8 there), and the one-click bulk change.

## 3) Requirements used (quoted)
- FR-57: the controller may change a train's platform at a station by choosing one of that station's platforms; two trains on one platform within a few minutes is a warning to the controller, never a block.
- FR-63 and A-05: SmartRail records decisions; it does not control trains or detect collisions. Wording says "records", never "controls".
- NFR-04: a change reaches the displays and apps within about 30 seconds (their next poll).
- NFR-17: every action stores who, when and what.

## 4) Owner decisions of 21 Oct 2026
- Build the "set platform on the journey page" and the wizard warning. A train with no platform shows **TBA** on the main board (the owner did not choose between a dash and a message; TBA was my recommendation, the word is the same abbreviation in all three languages and is not translated).
- A platform display does **not** list a train that has no platform: its promise is "trains on this platform".

## 5) Data
No migration. `journey_stop_state.platform_id` (the current platform, already read by the status, board and search reads) and the `PlatformChanged` event type (already allowed by the check constraint) are used. The change writes one `train_event` (source Controller, payload `{ station, platform, previous }`) and bumps the journey version, in one transaction.

## 6) Rules
1. **Controller only** (401 anonymous, 403 operator or passenger). The actor is the token subject.
2. `POST /api/v1/journeys/{journeyId}/stops/{serviceStopId}/platform` with `{ platformCode, acknowledgeWarnings }`. `platformCode` null (or empty) clears the choice, so the planned platform applies again.
3. The platform must be one of the stop's station: 422 `platform_not_at_station`. Codes are matched without case.
4. A stop the train has **left** cannot be changed (409 `stop_already_departed`): it has a departure, or a later stop has an arrival or departure. A journey that is Completed or Cancelled is 409 `journey_not_changeable`. Unknown journey 404 `journey_not_found`, a stop of another service 404 `stop_not_found`.
5. **Advice, never a block:** another live journey at the same station using the target platform (its current, else planned platform) within `Scheduling:PlatformWarningMinutes` (10, the existing setting; window inclusive) gives 409 `warnings_not_acknowledged` with `warnings[]` (`trainNumber`, `minutesApart`, `oppositeDirection`). Repeating with `acknowledgeWarnings: true` makes the change. Opposite directions are worded more strongly in the web app and ask for a tick that signalling and control were checked.
6. The version conflict (an operator report in the meantime) is retried up to three times, then 409 `version_conflict`.
7. A later operator report never changes the platform: the stop state's platform is only written here.
8. The controller's journey detail lists the event (`PlatformChanged`, with the platform) and shows the current and the planned platform.
9. Counter `smartrail.platform.changes` with `result` ok, needs_confirmation or rejected.

## 7) Acceptance Criteria
- [x] A controller can set platform 2 at a stop; the journey detail, the platform 2 board and the status reads show it, and the platform 1 board no longer lists the train.
- [x] Clearing the platform (null) goes back to the planned platform.
- [x] A platform of another station is refused and nothing is stored.
- [x] A stop the train has left cannot be changed.
- [x] Another train on that platform within 10 minutes is advice that is confirmed with `acknowledgeWarnings`, and trains in opposite directions are marked; it never blocks.
- [x] A change moves the journey version and writes one `PlatformChanged` event with the actor; a later operator report keeps the platform.
- [x] Operator, passenger and anonymous callers are refused; unknown journey or stop is 404.
- [x] The journey page offers Set platform only for stops not yet left on a journey that is not Completed or Cancelled; it shows the advice with a tick before Change anyway, and the safety wording.
- [x] The main board shows TBA for a train with no platform; the wizard says what a missing platform means.
- [ ] Seen on the emulator and a real display: the platform change appears on the passenger app and the platform display within 30 seconds. *(Hand check.)*

## 8) Test Plan
Unit: `PlatformChangeRulesTests` (window edges, direction, order). Integration: `PlatformChangeTests` (10 tests). Web: `JourneyDetailPage.test.tsx` (the dialog, the advice and the tick, completed journeys, the event text) and the main board test.

## 9) Not done
- The automatic Platform conflict exception when estimates change (spec 16 rule 8) and the exceptions queue entry.
- Un-translated: the Set platform texts are English only (the controller web app is English).
- Hand over, overrides, disruptions, closures and manual events of spec 16 remain not built.
