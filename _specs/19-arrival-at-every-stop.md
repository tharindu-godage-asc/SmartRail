# Feature Spec: An Arrived button at every in-between stop

branch: claude/feature/arrival-at-every-stop
spec: _specs/19-arrival-at-every-stop.md
owner: tharindu
date: 2026-10-20
status: approved (owner answered the questions on 20 Oct 2026; built the same day)

---

## 1) Problem
Today the operator app offers Arrived only at stops flagged `arrival_report_required` (Gampaha on 8058) and at the final stop. A stop a train merely calls at (Ragama, Maradana) offers only Departed. The owner wants Arrived and Departed at every in-between stop, so that passengers and the platform displays can show "Arrived" at those stations too (the Arrived screen of spec 09).

## 2) A change to the baseline (stated plainly)
This **changes** FR-22 ("the operator shall report arrivals only at designated arrival stations") and decision D-08 ("arrivals reported only at designated stations"), and AC-E03-03 2 ("a station that is not designated offers no arrival action"). The owner approved this on 20 Oct 2026. The documents carry a dated note; the original wording is kept. It also weakens CON-07 (minimal operator effort) a little: one more button at each stop, but it is optional (rule 3).

## 3) Owner decisions of 20 Oct 2026
- **Which stops:** every stop that has a scheduled arrival, so every stop except the first. No per-stop setting in the timetable.
- **Buttons:** Arrived and Departed are shown together at an in-between stop; either can be reported. Reporting the arrival is optional.
- **Baseline:** record this as a changed requirement in the docs.

## 4) Rules
1. The API marks `isArrivalStation` true for every stop with a scheduled arrival (all but the first). `service_stop.arrival_report_required` stays in the data and the timetable is not changed; the flag no longer decides.
2. `POST /journeys/{id}/events` accepts an `Arrived` report at any such stop. At the first stop it is still 422 `not_arrival_station`.
3. A departure never needs its arrival: `Departed` at an in-between stop is accepted with no arrival before it. (Server order rules are unchanged: where both exist at a stop the arrival comes before the departure.)
4. The operator app, at an in-between stop that has not been departed from, shows **Arrived <stop>** (primary) and **Departed <stop>** (secondary) together. After the arrival is reported it shows Departed only. Once the departure is reported the stop is done: Arrived is not offered again for it.
5. The final stop still offers only Arrived, and reporting it completes the journey.
6. The passenger status, the station boards and the platform displays already read arrivals from the stop states, so an Arrived at an in-between stop shows there with no change: the platform display shows its Arrived screen until the train departs.
7. A stop that is departed without an arrival has no arrival time and is treated as before (`Inferred` where the rules say so).

## 5) Data
No migration, no new column. Existing journeys, services and the timetable wizard are unchanged.

## 6) Architecture
- Application: `StopInfo.IsArrivalStation` (`JourneyData.cs`).
- Operator app: `ArriveAt.canDepartInstead` in `progress.dart`, the action card in `journey_screen.dart`, one string.

## 7) Acceptance Criteria
- [x] An arrival at Ragama or Maradana is accepted (201); at the first stop it is still 422 `not_arrival_station`.
- [x] A departure from an in-between stop is accepted without its arrival.
- [x] `GET operator/journey` marks every stop but the first as an arrival station.
- [x] The operator app shows Arrived and Departed together at an in-between stop, Departed only after the arrival, and only Arrived at the final stop.
- [x] The existing backend and operator app tests that encoded the old rule are updated; everything else still passes.
- [ ] Tried on the emulator: Arrived at Ragama appears on the passenger app and as the Arrived screen on `/display/platform/RGM/1`. *(Hand check.)*

## 8) Test Plan
- Integration: `ReportArrivalTests` (in-between arrival, departure without arrival, first stop still refused, the stop flags).
- App: `progress_test.dart` and `journey_screen_test.dart` (both buttons, only Departed after arriving, final stop).

## 9) Not done
- Whether the operator must report the arrival is not configurable per stop. If a service should ever stay departure-only, a per-stop setting would be a new spec.
- No notification or message to passengers (unchanged).
