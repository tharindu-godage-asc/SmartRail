# SmartRail – Acceptance Criteria

**Deliverable:** Requirements → Acceptance criteria (brief §23)
**Source:** user stories, journeys, decision log, refined requirements.

Every **Must** story in `smartrail-user-stories.md` has a section here, referenced by its **AC ref**. Should and Could stories get criteria when they are built. Criteria are written as *Given / When / Then*, are testable, and use one shared timeline so they double as test data:

- **Train 8058:** Veyangoda 08:20 (dep) → Gampaha 08:30 → Ragama 08:42 → Maradana 08:50 → Colombo Fort 08:55, Main Line, weekdays. Gampaha is a designated arrival station.
- **Long-distance service:** Colombo Fort → Badulla, collection at Badulla (destination).
- **Defaults (configurable):** on-time ±2 min; overdue grace 3 min; push threshold 5 min regional / 10 min long-distance; recovery pause 10 min; duplicate window 5 min; unpaid-order expiry 15 min.

Each criterion carries a **Trace** to the decision or requirement it verifies (D-, ADR-, FR-, NFR-).

---

## E01 — Identity and access

### AC-E01-01 Operator sign-in
1. **Given** Nimal has a seeded operator account, **when** he signs in on the operator app (Authorization Code with PKCE), **then** he receives an access token (15 minutes) and a refresh token (8 hours) and sees his assigned journey. *Trace: FR-01, NFR-11*
2. **Given** invalid credentials, **when** he tries to sign in, **then** access is refused and the message does not reveal which part was wrong. *Trace: NFR-10*
3. **Given** his access token has expired but the refresh token is valid, **when** the app calls the API, **then** the token is refreshed without prompting him. *Trace: NFR-11*
4. **Given** both tokens expired while he was offline with queued reports, **when** he reopens the app, **then** he re-authenticates before synchronising, and none of the queued reports is lost. *Trace: FR-24, CON-10*

### AC-E01-02 Controller sign-in
1. **Given** Jayasinghe has the Controller role, **when** he signs in to the operations web app, **then** he sees the operations view. *Trace: FR-01*
2. **Given** a user with only the Operator role, **when** they open the operations web app, **then** access is refused. *Trace: FR-02*

### AC-E01-03 Authorisation
1. **Given** a request to any event endpoint without a token, **when** it is received, **then** it is rejected with 401 and no event is created. *Trace: NFR-10*
2. **Given** Nimal is not assigned to a journey at an event's occurrence time, **when** he submits an event for it, **then** it is rejected, no event is stored, and the attempt is logged. *Trace: FR-03, D-28*
3. **Given** an operator token, **when** it calls a controller-only action (verify, override, publish), **then** the call is rejected with 403. *Trace: FR-02*
4. **Given** Nimal is assigned at 10:12 and his assignment ends at 10:15, **when** a report he created offline with occurrence time 10:12 syncs at 10:20, **then** it is accepted; **when** he creates a new report at 10:18, **then** it is rejected. *Trace: FR-13, MP-19*

### AC-E01-04 Anonymous passenger use
1. **Given** a first-time passenger with no account, **when** they search trains, view live status, subscribe to a train, and view the food menu, **then** they are never asked for a name, email or phone number. *Trace: FR-05, NFR-20*

---

## E02 — Reference data, journeys, simulator

### AC-E02-01 Journey generation
1. **Given** service 8058 runs on weekdays, **when** the journey generator runs for Tuesday 6 October (using the controllable clock's date), **then** exactly one journey for 8058 on that date exists, with status Unassigned and a stop state for every stop. *Trace: FR-11*
2. **Given** a journey already exists, **when** the generator runs again, **then** no duplicate is created. *Trace: FR-11*
3. **Given** a service does not run on Sundays, **when** the generator runs for a Sunday, **then** no journey is created. *Trace: FR-10*
4. **Given** the generator's horizon is 7 days, **when** it runs, **then** journeys exist for the next 7 days. *Trace: A-07i*
5. **Given** a journey has no assigned operator, **when** Nimal opens the app, **then** he sees "No journey assigned" and cannot report; **when** Jayasinghe assigns him, **then** reporting becomes available. *Trace: FR-12, MS-10*

### AC-E02-02 Simulator
1. **Given** the simulator signs in as a seeded operator, **when** it replays 8058, **then** it uses the same API as the operator app, and its events are recorded with source Simulator. *Trace: FR-105, D-30*
2. **Given** a scenario defines a delay, silence, delayed delivery, or incident, **when** it runs, **then** the same event types appear that a real operator would produce. *Trace: FR-105*
3. **Given** the clock is simulated, **when** the demo is stepped, paused, or sped up, **then** ETAs, overdue detection and freshness all follow the simulated time. *Trace: FR-106, ADR-06*
4. **Given** the simulator is asked to terminate a journey, **when** it runs, **then** it only reports a technical fault; the termination itself remains a controller action. *Trace: ADR-05*
5. **Given** a test needs the operator app to lose its network, **when** the emulator's network is switched off, **then** the app queues reports locally; **given** the simulator is told to deliver events late, **when** it sends them, **then** they arrive with their original occurrence times. *Trace: NFR-45*

---

## E03 — Train operator reporting

### AC-E03-01 View assigned journey
1. **Given** Nimal is assigned 8058, **when** he opens the app, **then** he sees the train number, route, stops and current status. *Trace: FR-20*
2. **Given** 8058 departed Veyangoda at 08:28, **when** the journey screen is viewed, **then** Veyangoda is shown as completed and Gampaha as the next stop. *Trace: FR-20*
3. **Given** a station event has been recorded, **when** the screen refreshes, **then** only the next valid action is offered. *Trace: D-42*
4. **Given** 8058 is in progress, **when** the screen is viewed, **then** "Report delay" is always available alongside the next station action. *Trace: FR-20*
5. **Given** no network, **when** he opens the previously loaded journey, **then** it shows "Offline – last updated [time]". *Trace: P-02*
6. **Given** Operations has suspended 8058, **when** Nimal views his journey, **then** he sees the suspension and its reason, and can still report a delay or fault. *Trace: FR-28, ADR-05*

### AC-E03-02 Report a departure
1. **Main flow.** **Given** 8058 is scheduled to depart Veyangoda at 08:20, **when** Nimal taps "Departed Veyangoda" at 08:28, **then** a departure event is recorded with occurrence time 08:28 and he sees it marked Delivered. *Trace: FR-21*
2. **Passenger-facing update.** **Given** that departure, **when** a passenger or display views 8058, **then** the status shows "Departed Veyangoda" and the Gampaha ETA is 08:38 (+8 min). *Trace: FR-39*
3. **Offline.** **Given** no connectivity, **when** he taps "Departed Veyangoda" at 08:28, **then** the event is stored with occurrence time 08:28 and shows "Not sent yet – will send automatically"; **when** connectivity returns, **then** it is sent and shows Delivered. *Trace: FR-24, FR-25*
4. **Duplicate.** **Given** the departure was already submitted, **when** the same event is received again (retries, repeated taps, delayed sync), **then** only one event is recorded, it is marked Delivered, and no error is shown. *Trace: FR-35, D-35*
5. **Conflicting time, same station.** **Given** a departure from Veyangoda at 08:28 is recorded, **when** a different departure from Veyangoda with occurrence time 08:31 is reported, **then** it is not applied automatically, it is added to the exceptions queue, and Nimal is told it is awaiting review. *Trace: FR-36*
6. **Forward skip.** **Given** a departure from Veyangoda is recorded and none from Gampaha, **when** Nimal reports a departure from Ragama at 08:52, **then** the Ragama departure is accepted, Gampaha is recorded as passed (inferred), no exception is raised, and passengers see normal progress. *Trace: FR-36, P-07*
7. **Backward conflict.** **Given** a departure from Ragama is recorded with occurrence time 08:52, **when** a departure from Veyangoda with occurrence time later than 08:52 is reported, **then** it is rejected and added to the exceptions queue. *Trace: FR-36*
8. **Late-arriving, correct order.** **Given** a departure from Ragama (occurred 08:52) has been received, **when** a departure from Veyangoda that occurred at 08:28 arrives at 08:53 after offline sync, **then** it is accepted because order is judged by occurrence time, and the received time is kept for audit. *Trace: FR-36, D-09*
9. **Next valid action only.** **Given** the Veyangoda departure is recorded, **when** Nimal views the journey, **then** "Departed Veyangoda" is no longer offered and the next valid action is shown. *Trace: D-42*
10. **Early departure.** **Given** Gampaha's scheduled departure is 08:30, **when** Nimal reports "Departed Gampaha" with occurrence time 08:27, **then** it is accepted and a High exception "Departure reported before scheduled time" is raised. *Trace: FR-40, D-49*
11. **Offline next action.** **Given** Nimal's "Departed Gampaha" is waiting in the local queue, **when** he views the journey, **then** the next action is already for Ragama. *Trace: FR-26*

### AC-E03-03 Report an arrival
1. **Given** Gampaha is a designated arrival station, **when** Nimal taps "Arrived Gampaha" at 08:40, **then** the arrival is recorded with occurrence time 08:40 and passengers see "Arrived at Gampaha 08:40". *Trace: FR-22*
2. **Given** a station is not designated, **when** Nimal views his actions, **then** no arrival action is offered and departure reporting remains. *Trace: D-08*
3. **Given** he is offline, **when** he records the arrival, **then** it is stored with its occurrence time and syncs later. *Trace: FR-24*
4. **Given** the arrival at the final station is accepted, **when** it is processed, **then** the journey is marked Completed without Nimal tapping anything else. *Trace: P-05*

### AC-E03-04 Report a delay
1. **Given** 8058 is delayed, **when** Nimal selects "Signal issue" and an estimated total delay of ~10 minutes, **then** a delay event is recorded and passengers immediately see "Delayed (~10 min)". *Trace: FR-23, FR-42*
2. **Given** a total delay of ~10 minutes, **when** ETAs are recalculated, **then** Gampaha (scheduled 08:30) shows 08:40. *Trace: D-07, FR-39*
3. **Given** the delay reason is awaiting review, **when** passengers view 8058, **then** the reason is not shown. *Trace: FR-42*
4. **Given** Jayasinghe verifies the reason, **when** verification completes, **then** passengers see "Delayed (~10 min) · Reason: Signal issue · Verified by Operations". *Trace: FR-52*
5. **Given** Nimal does not know the likely duration, **when** he selects Unknown, **then** passengers see "Train delayed. Delay duration currently unknown." *Trace: FR-23*
6. **Given** 8058 is already running +8 min late, **when** Nimal opens the estimate options, **then** only options greater than +8 are offered (+10, +15, +20) together with Unknown. *Trace: D-07*
7. **Given** a technical fault, obstruction or weather reason, **when** the report is made, **then** the resulting exception is Critical. *Trace: D-44*

### AC-E03-05 Delivery status
1. **Given** a report is submitted and received, **when** transmission completes, **then** it shows "Delivered". *Trace: FR-25*
2. **Given** a report was recorded offline, **when** it is awaiting sync, **then** it shows "Not sent yet – will send automatically", and **when** sync succeeds it changes to "Delivered". *Trace: FR-25*
3. **Given** the same report is transmitted several times, **when** duplicates are detected, **then** one event exists and the status shows Delivered. *Trace: FR-35*
4. **Given** a report is received but conflicts with the recorded journey history, **when** it is rejected, **then** it shows "Rejected – requires Operations review". *Trace: FR-25*
5. **Given** a queued report fails to send, **when** the app retries, **then** it waits longer after each failure (starting at 5 seconds, doubling to a 5-minute cap, with random jitter), and **never discards** the report. *Trace: FR-31*
6. **Given** reports are waiting in the queue, **when** the app is closed and reopened, **then** the queue is intact and sending resumes. *Trace: FR-31, NFR-05*

### AC-E03-06 Flag my last report
1. **Given** Nimal's last report shows Delivered, **when** he taps "Something's wrong with my last report", **then** a High exception "Operator correction requested" is created, linked to that report, and the journey state is unchanged. *Trace: FR-27, D-41*
2. **Given** the flag is created, **when** it is stored, **then** the audit trail records it as an operator event. *Trace: D-36*
3. **Given** he is offline, **when** he flags the report, **then** the flag is queued and sent when connectivity returns. *Trace: FR-24*

---

## E04 — Live status, ETA and event processing

### AC-E04-01 Duplicates
1. **Given** an event with an existing journey and client event ID, **when** it arrives again, **then** it is acknowledged as already recorded and creates no second event. *Trace: FR-35*
2. **Given** a controller entered "Departed Gampaha 08:42" on Nimal's behalf, **when** Nimal's own report of the same departure (08:42) syncs within 5 minutes, **then** the two are linked as one event, not duplicated. *Trace: FR-37, FS-22*
3. **Given** two reports for the same station more than 5 minutes apart, **when** the second arrives, **then** it is treated as a conflicting time and sent to the exceptions queue. *Trace: FR-36*

### AC-E04-02 Event ordering and validation
1. **Given** an occurrence time more than 1 minute in the future, **when** an event is received, **then** it is rejected. *Trace: FR-38*
2. **Given** an occurrence time older than 8 hours, **when** an event is received, **then** it is rejected. *Trace: FR-38*
3. **Given** Nimal's assignment ended before an event's occurrence time, **when** it arrives after a handover, **then** it is rejected; **given** it occurred while he was assigned but arrives after the handover, **then** it is accepted and a High exception is raised for review. *Trace: FR-38, SEC-18*
4. **Given** events arrive out of order but with valid occurrence times, **when** they are processed, **then** the journey state matches what would have resulted had they arrived in order. *Trace: D-09*
5. **Given** the application's database account, **when** it tries to update or delete a row in the events table, **then** the database refuses; inserts and reads succeed. *Trace: NFR-17*
6. **Given** a journey's events, **when** its state is rebuilt from them in occurrence order, **then** the result equals the live state (checked by an automated test, Should). *Trace: NFR-46*

### AC-E04-03 ETA calculation
1. **Given** a +8 min delay after Veyangoda, **when** ETAs are calculated, **then** every remaining stop is its scheduled time plus 8 minutes (Gampaha 08:38, Ragama 08:50, Maradana 08:58, Colombo Fort 09:03). *Trace: FR-39, D-05*
2. **Given** a train arrives at Gampaha at 08:28 and the scheduled departure is 08:30, **when** downstream times are calculated, **then** the departure remains 08:30 and no negative delay is carried forward. *Trace: D-48*
3. **Given** a train is within ±2 minutes of schedule, **when** its status is derived, **then** it is "On time"; at +3 it is "Delayed". *Trace: FR-40*
4. **Given** the operator estimates a ~10 minute total delay, **when** the estimate is applied, **then** the delay equals 10 minutes in total, not 10 minutes added to the current delay. *Trace: D-07*
5. **Given** the backend derives presentation state, **when** a snapshot is requested, **then** it contains the status group, status key, primary ETA and station, Level 1 content key, last known position, and expected-next-event time. *Trace: FR-44, D-52*

### AC-E04-04 Overdue detection
1. **Given** Gampaha is expected at 08:40 and the grace period is 3 minutes, **when** no event has arrived by 08:43, **then** the journey is marked "Waiting for update", the ETAs are removed, and a High exception is raised. *Trace: FR-41, D-06*
2. **Given** a new event then arrives, **when** it is processed, **then** a recalculated ETA replaces the waiting state. *Trace: FR-41*
3. **Given** a journey has an active override, **when** its expected time passes, **then** no overdue exception is raised. *Trace: D-43*
4. **Given** a suspension was set at 08:35 and cleared at 09:10, **when** it is cleared, **then** expected times are recalculated from the resume time and no flood of overdue exceptions follows. *Trace: D-43*
5. **Given** SmartRail has just recovered from an outage, **when** it restarts, **then** overdue detection is paused for 10 minutes and notifications for events older than 5 minutes are not sent. *Trace: FR-41, FR-76, FS-21*
6. **Given** a journey is overdue, **when** the state changes, **then** no push notification is sent. *Trace: FR-76*

---

## E05 — Operations control

### AC-E05-01 Network overview
1. **Given** 8058 is active, **when** Jayasinghe opens the overview, **then** he sees its last event, current delay, next expected stop, and time since the last update. *Trace: FR-50*
2. **Given** a new event is accepted for any active journey, **when** it is recorded, **then** the overview updates without a manual refresh. *Trace: FR-50*
3. **Given** a status comes from an operator report, controller entry, inference, or override, **when** it is displayed, **then** the source is shown with a label and icon, not colour alone. *Trace: NFR-30*
4. **Given** there are open exceptions, **when** the overview is shown, **then** the count is always visible. *Trace: FR-50*
5. **Given** a journey has an active override, **when** it appears in the list, **then** it shows the override state, not its delay. *Trace: ADR-05*
6. **Given** a journey with a reported reason, **when** it appears in the overview, **then** its verification state (verified or awaiting verification) is shown. *Trace: FR-50*
7. **Given** any screen of the operations app, **when** it is shown, **then** a permanent notice says SmartRail is an information system and not a safety system. *Trace: FR-63, A-05*

### AC-E05-02 Exceptions queue
1. **Given** a technical fault, obstruction or weather report awaits verification, **when** it arrives, **then** it appears as Critical; **given** a signal issue or Unknown reason awaits verification, an overdue journey, an operator correction request, a backward conflict, a same-station/different-time conflict, or an early departure, **then** it appears as High. *Trace: FR-51, D-44*
2. **Given** one report, **when** it generates an exception, **then** exactly one exception exists for it (a technical fault report is one exception, not two). *Trace: D-44*
3. **Given** several exceptions, **when** the queue is shown, **then** it is ordered by severity, then oldest first. *Trace: D-44*
4. **Given** each exception row, **when** it is displayed, **then** it shows the journey, station, type, and age (e.g. "8058 · Gampaha · Reason awaiting verification · 3 min"). *Trace: P-15*
5. **Given** a forward skip, a late event delivered in correct order, a normal sync retry, or no movement during an override, **when** it occurs, **then** no exception is raised. *Trace: P-07, D-43*
6. **Given** Jayasinghe resolves an exception, **when** he selects an action, **then** it leaves the queue and the action, time and controller are audited. *Trace: FR-43*
7. **Given** a suspended journey, **when** an operator event for it arrives, **then** the suspension stays active and an "operator activity on suspended service" exception is raised. *Trace: ADR-05*

### AC-E05-03 Verify delay reason
1. **Given** Nimal reported "Signal issue" for 8058, **when** Jayasinghe opens the report, **then** he sees the reason, estimate, operator, and occurrence time. *Trace: FR-52*
2. **Given** the reason is correct, **when** he verifies it, **then** passengers see "Reason: Signal issue · Verified by Operations". *Trace: D-10*
3. **Given** the reason is not verified, **when** passengers view 8058, **then** they see the delay and ETA but no reason. *Trace: D-10*
4. **Given** a reason is wrong, **when** Jayasinghe changes it before verifying, **then** only the corrected reason is ever published. *Trace: FR-52*
5. **Given** the reason is verified, **when** passengers view it, **then** the verification time is shown with "Verified by Operations". *Trace: FR-52*

### AC-E05-04 Enter an event on behalf of an operator
1. **Given** Nimal phones in "Departed Veyangoda 08:28", **when** Jayasinghe records it, **then** the event is stored with occurrence time 08:28 and source "Controller entry, reported by operator by phone". *Trace: FR-53, A-04*
2. **Given** Jayasinghe entered it, **when** Nimal's own offline report syncs later, **then** it is linked to the existing event with no duplicate. *Trace: FR-37*
3. **Given** the entry conflicts with journey order, **when** he submits it, **then** he is warned before it is saved. *Trace: FR-36*

### AC-E05-05 Service-state override
1. **Given** 8058 is running, **when** Jayasinghe sets "Suspended at Ragama" with a reason, **then** the passenger app, displays, and (if built) terminal show "Service suspended" and no ETAs. *Trace: FR-54*
2. **Given** 8058 is suspended, **when** Nimal reports "Departed Ragama", **then** the suspension remains active and passengers keep seeing "Service suspended". *Trace: ADR-05*
3. **Given** Jayasinghe selects Resume, **when** the override is cleared, **then** passengers see the current status from the latest operator events. *Trace: ADR-05, D-43*
4. **Given** any override is set or cleared, **when** the action completes, **then** the controller, time and reason are in the audit trail. *Trace: FR-54*
5. **Given** a journey, **when** a second override is set, **then** it is refused until the first is cleared (at most one active override). *Trace: ADR-05*
6. **Given** a journey is terminated or cancelled, **when** the override is set, **then** the passenger's food screen shows the disrupted state (AC-E08-07 (6)). Queuing a message for the caterer in the same transaction is designed, not built (FR-95, D-72). *Trace: FR-95, ADR-12*
7. **Given** any override screen (suspend, cancel, terminate or resume), **when** it is shown, **then** it states that it records a decision and does not control trains. *Trace: FR-54, FR-63*

### AC-E05-06 Disruption message
1. **Given** a signal failure affects journeys between Veyangoda and Gampaha, **when** Jayasinghe publishes a message for the affected journeys and stations, **then** it appears on the passenger app and affected displays. *Trace: FR-55*
2. **Given** a passenger chose Sinhala, Tamil or English, **when** they view the message, **then** it is in their language. *Trace: D-25*
3. **Given** the disruption ends, **when** he withdraws the message, **then** it disappears from all channels. *Trace: FR-55*
4. **Given** a message is built from a template, **when** it is published, **then** no unreviewed free text reaches passengers. *Trace: D-10*

### AC-E05-07 End an operator's assignment
1. **Given** Nimal's phone is reported lost, **when** Jayasinghe ends his assignment, **then** every further event from that device for the journey is rejected immediately. *Trace: FR-56, SEC-12*
2. **Given** reports Nimal made before the assignment ended, **when** the assignment ends, **then** they are retained. *Trace: D-36*
3. **Given** the assignment ends, **when** Jayasinghe assigns a replacement, **then** the replacement can report from that point. *Trace: FR-13*
4. **Given** the action, **when** it completes, **then** it is audited. *Trace: FR-54*

### AC-E05-08 Change a platform (Should)
1. **Given** 8058 is due at Gampaha on Platform 2, **when** Jayasinghe opens Change platform, **then** only Gampaha's platforms are offered. *Trace: FR-10, FR-57*
2. **Given** no other journey is planned on Platform 3 within 10 minutes of 8058, **when** he confirms Platform 3, **then** the main board, the platform displays and the passenger app show Platform 3 within 30 seconds. *Trace: FR-57, NFR-04*
3. **Given** 8052 (same direction) is planned on Platform 3 at 08:46, **when** he picks Platform 3 for 8058, **then** a warning names both trains and he can still confirm. *Trace: FR-57*
4. **Given** 8065 (opposite direction) is planned on Platform 1 at 08:44, **when** he picks Platform 1 for 8058 (08:47), **then** a high-severity warning names both trains and says to check with signalling and control, and he can still confirm. *Trace: FR-57, FR-63*
5. **Given** an estimate changes so that two journeys share a platform within the window, **when** the estimate is applied, **then** a platform-conflict exception appears in the queue, Critical if the journeys run in opposite directions and High otherwise. *Trace: FR-57, FR-51*
6. **Given** a platform that belongs to another station is submitted, **when** the change is made, **then** it is refused and nothing changes. *Trace: FR-10, EC-66*
7. **Given** any platform change, **when** it completes, **then** the controller, time, old platform and new platform are in the audit trail. *Trace: D-36*
8. **Given** a conflict warning or exception, **when** it is shown, **then** it states that it is advice based on reported data and that SmartRail does not detect collisions. *Trace: FR-63, A-05*

### AC-E05-09 Choose a line (Should)
1. **Given** the operations app, **when** Jayasinghe selects the line icon in the navigation, **then** a list opens showing each line with seeded services and its open-exception count. *Trace: FR-107*
2. **Given** he selects the Coastal Line, **when** the list closes, **then** the navigation and every screen title name the Coastal Line, and the overview, exceptions, journeys, overrides, disruptions and line closures show only its data. *Trace: FR-107*
3. **Given** a Critical exception on the Coastal Line, **when** he is viewing the Main Line, **then** the Coastal Line's count shows in the list without switching. *Trace: FR-107, FR-51*
4. **Given** the chosen line has no exceptions, overrides or closures, **when** that tab is shown, **then** it shows a plain message such as "No open exceptions on the Kelani Valley Line", not an empty table. *Trace: FR-107*
5. **Given** the MVP seeds only the Main Line, **when** the list opens, **then** it shows the Main Line only. *Trace: FR-107, A-25*
6. **Given** a disruption message about a station served by several lines, **when** a line is chosen, **then** the message appears under each line that serves the station. *Trace: FR-107, FR-55*

### AC-E05-10 Controller in charge (Should)
1. **Given** 8058 has no controller in charge, **when** Jayasinghe opens the journey, **then** the Crew card shows "No controller in charge" and a Take charge button, and he can still act on the journey. *Trace: FR-125, FR-04*
2. **Given** he presses Take charge at 08:05, **when** the action completes, **then** the card shows "Jayasinghe, since 08:05" and the action is in the audit trail. *Trace: FR-125, D-36*
3. **Given** Jayasinghe is in charge, **when** he hands the journey to Perera with a handover note, **then** his assignment ends, Perera's starts at the same moment, and the note is shown to Perera. *Trace: FR-125, FR-58*
4. **Given** Perera, who is not in charge, verifies a delay reason on 8058, **when** it is saved, **then** it is recorded with Perera as the actor, and the journey still shows Jayasinghe in charge. *Trace: FR-125, FR-04*
5. **Given** two controllers press Take charge at the same moment, **when** both requests arrive, **then** exactly one succeeds and the other is told who is now in charge and can choose to take over. *Trace: FR-125, ADR-09*
6. **Given** the in-charge controller's account is disabled, **when** the controller list refreshes, **then** the journey shows "No controller in charge". *Trace: FR-125, FR-06*
7. **Given** an operator or a passenger, **when** they call take-charge or handover, **then** the call is refused with 403 (a passenger is anonymous and gets 401). *Trace: FR-02, NFR-10*

---

### AC-E05-11 Senior controller (Deferred: future work, D-76)
1. **Given** Perera is in the identity provider group `senior-controller`, **when** he opens the dashboard, **then** a "By controller" view lists each controller with the journeys they are in charge of and their recent recorded actions. *Trace: FR-126, FR-06*
2. **Given** Jayasinghe is in charge of 8058, **when** Perera reassigns it to Silva with a note, **then** Silva is in charge from that moment, Jayasinghe is told, and the audit trail shows Perera as the actor. *Trace: FR-126, FR-125*
3. **Given** Perera tries to reassign a journey to a disabled account or to someone who is not a controller, **when** he confirms, **then** it is refused and nothing changes. *Trace: FR-126, FR-06*
4. **Given** a controller who is not in the group, **when** they call the reassign or by-controller endpoints, **then** the call is refused with 403. *Trace: FR-126, NFR-10*
5. **Given** Perera is removed from the group, **when** his token is next refreshed, **then** the senior-controller view and reassign disappear and his Controller rights stay. *Trace: FR-126, NFR-11*
6. **Given** Perera reassigns journeys, **when** the audit trail is read, **then** no approval step was needed from anyone, and no one's action was blocked. *Trace: FR-126*

---

## E06 — Passenger journey information

### AC-E06-01 Find trains
1. **Given** Kasuni selects Gampaha to Colombo Fort, **when** she searches, **then** she sees upcoming trains with scheduled time, status and live ETA at Gampaha. *Trace: FR-65*
2. **Given** 8058 is running +8 min, **when** it appears in results, **then** it shows 08:38 alongside the scheduled 08:30. *Trace: FR-39*
3. **Given** a train is cancelled or suspended, **when** it appears, **then** its status is shown with text and an icon, not colour alone. *Trace: NFR-30*
4. **Given** she searches again later, **when** the app opens, **then** her origin station is remembered on the device, with no location permission. *Trace: FR-65*

### AC-E06-02 Live status
1. **Given** 8058 departed Veyangoda at 08:28 and Kasuni is waiting at Gampaha, **when** she views it, **then** the primary ETA is "Arrives at Gampaha 08:38 (+8 min)". *Trace: D-38, FR-66*
2. **Given** the train departs Gampaha, **when** the screen updates, **then** the primary ETA switches to her destination ("Arrives at Colombo Fort 09:03"). *Trace: D-38*
3. **Given** Nimal reported a ~10 min delay and the reason is unverified, **when** she views the train, **then** she sees "Delayed (~10 min), ETA 08:40" with no reason. *Trace: D-10*
4. **Given** Jayasinghe verified the reason, **when** she views the train, **then** she sees "Reason: Signal issue · Verified by Operations". *Trace: D-10*
5. **Given** Gampaha was passed without an operator report, **when** she views the stop list, **then** Gampaha shows as passed with no "missing confirmation" wording. *Trace: P-07*
6. **Given** one stop list, **when** it is shown, **then** it combines progress and ETA per stop, with no duplicate list. *Trace: D-38*

### AC-E06-03 Never silent
1. **Given** Gampaha's ETA is 08:40 and no event has arrived by 08:43, **when** Kasuni views the train, **then** the ETA is replaced with "Waiting for update · Last seen: departed Veyangoda 08:28". *Trace: FR-41, D-39*
2. **Given** a new event arrives, **when** the status updates, **then** a recalculated ETA replaces the waiting message. *Trace: FR-41*
3. **Given** her phone has no network, **when** she views the train, **then** she sees "You're offline · Showing information from [time]" and the last known ETA, marked. *Trace: FR-67, D-39*
4. **Given** the phone is online but SmartRail is unreachable, **when** she views the train, **then** the message is "Live updates unavailable". *Trace: MS-12*
5. **Given** her phone is offline and the snapshot's expected-next-event time has passed, **when** she views the train, **then** she is told the information is likely out of date. *Trace: FR-67*
6. **Given** 8058 is suspended, **when** she views it, **then** she sees "Service suspended" with the verified reason and no ETA. *Trace: ADR-05*
7. **Given** loss of connection, **when** it occurs, **then** it is visible within 30 seconds. *Trace: NFR-03*

### AC-E06-04 Home screen
1. **Given** Kasuni follows 8058, **when** she opens the app, **then** the first card is her followed train with its ETA, status, and freshness or connection indicator when not current and connected. *Trace: FR-68, D-52*
2. **Given** she follows no train, **when** she opens the app, **then** search is first and no empty followed-train card appears. *Trace: FR-68*
3. **Given** service alerts, **when** they are shown, **then** they are disruption messages published by Operations, not automatic per-train statuses. *Trace: FR-55*
4. **Given** the home screen, **when** it is shown, **then** it contains no "trains at my station" section. *Trace: FR-68*
5. **Given** the first launch, **when** the app opens, **then** it asks for a language before anything else. *Trace: FR-69*

### AC-E06-05 Language and accessibility
1. **Given** Kasuni selects Sinhala, **when** she uses the app, **then** all interface text, statuses and disruption messages are in Sinhala. *Trace: FR-69, NFR-34*
2. **Given** a train is delayed, cancelled, or on time, **when** its status is shown, **then** each state is distinguishable by text and icon without relying on colour. *Trace: NFR-30*
3. **Given** she increases her phone's text size, **when** she views the app, **then** text scales without truncating times or statuses. *Trace: NFR-33*
4. **Given** a screen reader is on, **when** she opens a train, **then** it reads train, destination, ETA, status, reason and verification, in that order. *Trace: NFR-32*
5. **Given** a meaning change occurs (on time → delayed, delayed → waiting for update, reason verified, cancellation, suspension, termination, offline or live updates unavailable), **when** it happens, **then** the screen reader announces it, and disruptions (cancellation, suspension, termination) are announced with priority; **given** only an ETA ticks from 08:40 to 08:41, **then** nothing is announced. *Trace: D-54, NFR-32*
6. **Given** the status colours, **when** tested, **then** they meet WCAG AA contrast and remain distinguishable in a colour-blindness simulation. *Trace: NFR-31*
7. **Given** an automated accessibility test (Should), **when** an ETA changes from 08:40 to 08:41, **then** zero announcements are made; **when** each listed trigger occurs, **then** exactly one announcement is made. *Trace: NFR-36*

---

## E07 — Notifications

### AC-E07-01 Subscribe
1. **Given** Kasuni taps "Notify me" on 8058, **when** the subscription is made, **then** the server stores her device push token for that journey, with no account. *Trace: FR-75*
2. **Given** the app is closed, **when** 8058 is cancelled, **then** she receives a push notification. *Trace: FR-76*
3. **Given** the journey has ended, **when** the end is recorded, **then** her subscription is deleted. *Trace: D-31, NFR-22*

### AC-E07-02 Significant changes only
1. **Given** a regional journey, **when** its delay rises above 5 minutes, **then** she receives one notification with the new ETA; **given** a long-distance journey, **then** the threshold is 10 minutes. *Trace: A-07c*
2. **Given** a cancellation, suspension or termination, **when** it is recorded, **then** she receives a notification. *Trace: FR-76*
3. **Given** a journey is merely overdue, **when** the waiting state appears, **then** no notification is sent. *Trace: FR-76*
4. **Given** a notification is more than 5 minutes old at the time it would be sent, **when** it is processed, **then** it is dropped. *Trace: FS-21*
5. **Given** she turns notifications off for the train, **when** a change occurs, **then** she receives nothing for it. *Trace: FR-75*

---

## E08 — Catering

**Decision D-71 (12 October 2026): food ordering is designed but not built.** AC-E08-01, AC-E08-05 and AC-E08-07 are in the MVP test scope. AC-E08-02, AC-E08-03, AC-E08-04 and AC-E08-06 keep their wording as the specification for a later phase and are not built, tested or demonstrated.

### AC-E08-01 See the food menu
1. **Given** Kasuni's long-distance journey Colombo Fort → Badulla, **when** she opens the food menu, **then** she sees a list of the caterer's items for the stations where food is available (Badulla in the seed data), each with name, price and whether it is available. *Trace: FR-90, FR-91*
2. **Given** the caterer marks an item unavailable, **when** the menu refreshes, **then** the item shows Sold out with text and an icon, not colour alone. *Trace: FR-90, NFR-30*
3. **Given** the list, **when** it is shown, **then** it has no quantity control, basket or Order button; the only action is the link to the caterer (AC-E08-07). *Trace: FR-90*
4. **Given** a stop of less than 5 minutes that is not her destination, **when** stations with a menu are listed, **then** it has none. *Trace: D-15, FR-91*
5. **Given** Kasuni chose Sinhala, **when** the list is shown, **then** item names are in Sinhala, and an item with no translation shows its English name. *Trace: FR-14, FR-90*
6. **Given** the phone is offline, or no menu update has arrived for longer than the menu freshness limit (proposed 60 minutes, configurable), **when** the list is shown, **then** it shows "Menu from [time]" with a note that items and availability may have changed; **given** a current menu, **then** the time is shown without the warning. *Trace: FR-90, P-02, NFR-07*
7. **Given** a journey with no station that has catering facilities and is eligible for food, **when** Kasuni opens it, **then** no food menu entry is shown. *Trace: FR-91*

### AC-E08-02 Order and pay
*Status: designed, not built (D-71). Kept as the specification for a later phase; not part of the MVP test scope.*
1. **Given** she places an order, **when** it is forwarded, **then** its status is Pending and SmartRail stores only the order ID, journey, stop, status and device installation ID. *Trace: FR-92*
2. **Given** the caterer accepts, **when** it returns a payment link, **then** the status is Awaiting Payment, and payment takes place on the caterer's page; SmartRail never sees payment data. *Trace: FR-92, NFR-20*
3. **Given** she does not pay within 15 minutes, **when** the window ends, **then** the order shows Expired. *Trace: MS-11*
4. **Given** the ordering cutoff for the station has passed (fixed to the timetable), **when** she tries to order, **then** the caterer rejects it with a reason. *Trace: FR-94*
5. **Given** more than 5 order attempts from one device in a minute, **when** the sixth arrives, **then** it receives 429 Too Many Requests. *Trace: FR-99*

### AC-E08-03 Order status
*Status: designed, not built (D-71). Kept as the specification for a later phase; not part of the MVP test scope.*
1. **Given** an order, **when** its status changes, **then** the app shows Pending, Awaiting Payment, Confirmed, Ready, Collected, Not Collected, Rejected (with reason), Expired, or Refunded. *Trace: FR-93*
2. **Given** an order is rejected, **when** the status arrives, **then** Kasuni sees the caterer's reason and an alternative where one exists. *Trace: INT-05*
3. **Given** a "Collected" message (order version 5) arrives before a delayed "Confirmed" (version 3), **when** the second arrives, **then** it is ignored. *Trace: FR-96, INT-09*
4. **Given** the same message ID arrives twice, **when** it is processed, **then** the second is ignored. *Trace: FR-96*

### AC-E08-04 Collection token
*Status: designed, not built (D-71). Kept as the specification for a later phase; not part of the MVP test scope.*
1. **Given** the order is Confirmed, **when** the token is issued, **then** the QR and code are stored on Kasuni's device. *Trace: FR-98*
2. **Given** no signal at the counter, **when** she opens the token, **then** it displays. *Trace: FR-98*
3. **Given** the handler scans it, **when** it is valid, **then** the order becomes Collected; **given** the same token again, **then** it is refused. *Trace: D-15*
4. **Given** the handler needs to identify her, **when** collecting, **then** only the token and items are used, not her name or phone. *Trace: NFR-21*
5. **Given** the collection window ends without a scan, **when** it ends, **then** the order shows Not Collected with no automatic refund. *Trace: D-14*

### AC-E08-05 Train events to the caterer
*Status: designed, not built (D-72, 12 October 2026). Kept as the specification for a later phase; not part of the MVP test scope.*
1. **Given** a delay, cancellation or termination, **when** it is recorded, **then** a message is written to the outbox in the same transaction and delivered after commit. *Trace: ADR-12*
2. **Given** each message, **when** it is sent, **then** it carries a message ID and the journey version. *Trace: FR-95*
3. **Given** the caterer is unreachable, **when** delivery fails, **then** it is retried in the background and the railway's state is unaffected. *Trace: ADR-12*
4. **Given** an older journey version reaches the caterer after a newer one, **when** it is processed, **then** it is ignored. *Trace: FR-95*
5. **Given** SmartRail and the caterer authenticate each other, **when** either calls the other, **then** it uses a client-credentials token and unauthenticated calls are refused. *Trace: FR-07 (the caterer-to-SmartRail direction is built, see AC-E08-08)*
6. **Given** the caterer is reachable, **when** an event is accepted, **then** the message is delivered within 30 seconds. *Trace: NFR-08*
7. **Given** a message has been undelivered for more than 15 minutes, or the backlog is growing, **when** this is detected, **then** an alert is raised. *Trace: NFR-08, OPS-02*

### AC-E08-06 Automatic refund
*Status: designed, not built (D-71). Kept as the specification for a later phase; not part of the MVP test scope.*
1. **Given** Kasuni's order is Confirmed, **when** the journey is cancelled or terminated before her collection stop, **then** the caterer refunds in full automatically and the status becomes Refunded. *Trace: FR-97, D-14*
2. **Given** an alternative compensation is offered, **when** she does not choose it, **then** the default is a full refund. *Trace: D-14*
3. **Given** the train is delayed but still reaches her stop, **when** the delay is recorded, **then** no refund is made and preparation follows the live ETA. *Trace: D-14*
4. **Given** the refund completes, **when** the caterer reports it, **then** Kasuni sees "Refunded". *Trace: FR-93*

### AC-E08-07 Go to the caterer's website
1. **Given** the food list, **when** Kasuni taps "Order on the caterer's website", **then** the caterer's website opens outside SmartRail, at the address in the settings. *Trace: FR-124*
2. **Given** the link, **when** it opens, **then** its address carries at most the station code and train number, and no account, device, push-token or personal identifier. *Trace: FR-124, NFR-21*
3. **Given** the screen, **when** it is shown, **then** it says that ordering, payment and collection happen on the caterer's website and that SmartRail does not see the order. *Trace: FR-124, A-35*
4. **Given** no network, **when** she taps the button, **then** the app says she is offline and no page opens; the list stays visible. *Trace: FR-124, D-39*
5. **Given** a screen reader is on, **when** the button is focused, **then** it is announced as a link that opens outside SmartRail. *Trace: NFR-32*
6. **Given** the journey is cancelled or terminated before her food station, **when** she opens the food screen, **then** it shows the disrupted state, tells her to ask the caterer about any order already placed, and does not show the button. *Trace: FR-124, ADR-05*
7. **Given** the demonstration setup, **when** the link is tapped, **then** the simulated caterer's stand-in page opens; it takes no orders or payments. *Trace: FR-100*

---

## E09 — Station experience

### AC-E09-01 Main board
1. **Given** upcoming trains, **when** the board is shown, **then** each row shows time, destination, platform, and status. *Trace: FR-80*
2. **Given** a delay, **when** it is recorded, **then** the board updates without refresh. *Trace: NFR-04*
3. **Given** a disruption message affecting the station, **when** it is published, **then** it appears on the board with the verification badge. *Trace: D-46*
4. **Given** the board loses its connection, **when** 30 seconds pass, **then** it keeps showing scheduled trains and platforms, clearly marked as not live, with the last synchronised time. *Trace: FR-82, ADR-09*

### AC-E09-02 Platform display
1. **Given** 8058 is due at Gampaha, **when** the platform display is shown, **then** the largest element is the answer to "when is my train arriving?" (08:40) with the train number, destination in three languages, and status. *Trace: FR-81, D-50*
2. **Given** the train is overdue, **when** the display updates, **then** the largest element is "Waiting for update" with the last-seen position, and no ETA. *Trace: D-50, D-39*
3. **Given** the train is suspended, cancelled or terminated, **when** the display updates, **then** that state is the largest element. *Trace: D-50*
4. **Given** the platform display, **when** it is shown, **then** it has no verification badge. *Trace: D-46*

### AC-E09-03 Display offline
1. **Given** a display loses its connection, **when** 30 seconds pass, **then** it shows "Live updates unavailable · showing information from [time]". *Trace: NFR-03*
2. **Given** a display is offline, **when** the last known ETA is shown, **then** the train identity stays visible and the ETA is marked as last known. *Trace: D-39*
3. **Given** it reconnects, **when** a fresh snapshot is fetched and updates applied, **then** the display returns to live information. *Trace: ADR-09*

### AC-E09-04 No train due
1. **Given** no train is due on a platform, **when** the display is shown, **then** it shows "No trains until 09:25" and the next service. *Trace: D-45*
2. **Given** the last train has gone, **when** the display is shown, **then** it shows "No more services today" and the first service tomorrow. *Trace: D-45*

### AC-E09-05 Three languages
1. **Given** a public display, **when** it is shown, **then** Sinhala, Tamil and English appear side by side, not rotating. *Trace: FR-83, D-19*
2. **Given** times, platform numbers, train numbers and icons, **when** shown, **then** they are language-neutral. *Trace: P-11*
3. **Given** a status, **when** shown, **then** it uses text and an icon, never colour alone. *Trace: NFR-30*
4. **Given** the display is viewed from across a platform, **when** tested in a room test, **then** Level 1 is legible. *Trace: NFR-35*

### AC-E09-06 Station terminal (Could)
1. **Given** nobody is using the terminal, **when** a passenger touches it, **then** it asks for a language (Sinhala, Tamil or English) before anything else. *Trace: FR-85*
2. **Given** a language is chosen, **when** the passenger picks a destination, **then** they choose from large buttons for common places or an A to Z list, and no keyboard is needed. *Trace: FR-85*
3. **Given** a destination, **when** results are shown, **then** each of the next trains shows time, platform and status from the backend's state, and the terminal calculates no ETA of its own. *Trace: FR-85, P-01*
4. **Given** a train is chosen, **when** its details are shown, **then** they list stops, status, last update, and the verified reason with its verification time when one is published. *Trace: FR-85*
5. **Given** the passenger chooses service alerts, **when** they are shown, **then** they list the station's published messages with their verification times. *Trace: FR-85, FR-55*
6. **Given** the terminal loses its live connection for 30 seconds, **when** results are shown, **then** a banner says "Live updates unavailable" and the scheduled trains are marked "Scheduled, not live". *Trace: FR-85, NFR-03*
7. **Given** nobody touches the screen for the configured period, **when** the period ends, **then** a countdown is shown, a touch continues the session, and at zero the language and choices are cleared. *Trace: FR-85, ACC-06*
8. **Given** the terminal at its real size, **when** it is checked, **then** every control sits in the lower half of the screen and meets the proposed minimum target size of 88 px on a 1080 by 1920 screen. *Trace: FR-85, ACC-06*

## E11 — Ticketing and passenger accounts (Should)

Timeline for these criteria: **Metro** Veyangoda → Colombo Fort (train 8058, 08:20); **long-distance** Colombo Fort → Badulla. Hold time 10 minutes; at most 6 seats per purchase; at most 5 holds per account in 10 minutes. Not part of the Must scope (D-60).

### AC-E11-01 Buy a Metro ticket
1. **Given** Kasuni has chosen Veyangoda to Colombo Fort for today, **when** she taps Buy ticket, **then** she sees a Metro ticket with the fare and the note that it is valid on any regional train between those stations today. *Trace: FR-108, D-61*
2. **Given** the quote, **when** it is shown, **then** the fare equals the distance between the stations times the Metro rate, or the minimum fare if higher. *Trace: FR-109, A-26*
3. **Given** two stations that share no line, **when** she tries to buy, **then** the ticket is not offered. *Trace: FR-10, FR-108*
4. **Given** payment succeeds, **when** the result arrives, **then** the ticket is Paid and appears in her history. *Trace: FR-113, FR-115*
5. **Given** the fare changed after the quote, **when** she pays, **then** she pays the quoted fare. *Trace: FR-109*

### AC-E11-02 Account only at purchase
1. **Given** a passenger who is not signed in, **when** she browses, tracks, subscribes or orders food, **then** she is never asked to sign in. *Trace: FR-05, FR-110, D-62*
2. **Given** she taps Buy, **when** she is not signed in, **then** she is offered Sign in or Create account, and her chosen trip is kept. *Trace: FR-110*
3. **Given** she creates an account, **when** it completes, **then** SmartRail stores only the identity provider's account ID; name and email stay in the identity provider. *Trace: NFR-49*
4. **Given** she signs in, **when** she returns to checkout, **then** the same trip and fare are shown. *Trace: FR-110*

### AC-E11-03 Choose seats (optional)
1. **Given** a long-distance journey, **when** she selects Reserve a seat, **then** she sees coaches and seats, with seats taken, held for counters or sold for her part of the trip unavailable. *Trace: FR-111, FR-123*
2. **Given** a seat sold only from Colombo Fort to Anuradhapura, **when** she travels from Anuradhapura onward, **then** that seat is available to her. *Trace: A-28, FR-112*
3. **Given** she selects seats, **when** she tries to select a seventh, **then** it is refused with a message. *Trace: FR-111*
4. **Given** the seat map, **when** a seat's state is shown, **then** it uses a label and a pattern as well as colour. *Trace: NFR-30*

### AC-E11-04 Hold a seat
1. **Given** she confirms her seats, **when** the hold starts, **then** the seats are held for 10 minutes and a countdown is shown. *Trace: FR-112*
2. **Given** another passenger tries the same seat for an overlapping part of the journey, **when** the hold is active, **then** it is refused. *Trace: FR-112, NFR-47*
3. **Given** two passengers confirm the last free seat at the same moment, **when** both requests arrive, **then** exactly one succeeds and the other is told and offered other seats. *Trace: NFR-47, SEC-19*
4. **Given** the 10 minutes pass unpaid, **when** SmartRail checks the payment status with the provider, **then** an unpaid purchase is expired and the seats are released. *Trace: FR-112*
5. **Given** payment succeeds just after the hold expired, **when** the result arrives, **then** the seats are confirmed if still free, and otherwise the payment is refunded automatically and she is told. *Trace: FR-112, FR-117*
6. **Given** she holds six seats and one cannot be held, **when** the hold is made, **then** none are held. *Trace: FR-112*

### AC-E11-05 Pay and optionally save a card
1. **Given** she confirms, **when** she is sent to payment, **then** the hosted page of the payment provider opens and SmartRail has sent only an amount and an order reference. *Trace: FR-113, D-63*
2. **Given** the hosted page, **when** she ticks Save this card, **then** SmartRail afterwards stores the token reference, brand, last four and expiry only. *Trace: FR-114, NFR-48*
3. **Given** she does not tick it, **when** she pays, **then** nothing about the card is stored. *Trace: FR-114*
4. **Given** the payment is declined, **when** the result arrives, **then** the purchase shows Declined, the seats are released and she can try again. *Trace: FR-121*
5. **Given** the same payment message arrives twice or out of order, **when** it is processed, **then** it is applied once and an older version is ignored. *Trace: NFR-51*
6. **Given** the provider is unreachable, **when** she taps Pay, **then** she sees a clear message and the rest of the app works. *Trace: FR-113*

### AC-E11-06 Saved cards
1. **Given** a saved card, **when** she opens Account, **then** she sees brand, last four and expiry. *Trace: FR-114*
2. **Given** she removes it, **when** she confirms, **then** the token is deleted at the provider and the card no longer appears. *Trace: FR-114, D-63*
3. **Given** a saved card has expired, **when** she checks out, **then** it is marked expired and cannot be chosen. *Trace: FR-114*
4. **Given** a saved card, **when** she pays with it, **then** the provider still asks for confirmation on its hosted page. *Trace: FR-113*

### AC-E11-07 Purchase history
1. **Given** she is signed in, **when** she opens My tickets, **then** upcoming and past tickets are listed, most recent first, each with trip, date, type, seat, price and status. *Trace: FR-115*
2. **Given** a ticket that was refunded, **when** the list is shown, **then** it shows Cancelled by railway then Refunded. *Trace: FR-115, FR-117*
3. **Given** another passenger's ticket ID, **when** she requests it, **then** the request is refused. *Trace: NFR-50*
4. **Given** she signs in on a second phone, **when** she opens My tickets, **then** the same history appears. *Trace: FR-115*

### AC-E11-08 Use a ticket
1. **Given** a Paid ticket, **when** the ticket is opened with no network, **then** its code and trip are shown. *Trace: FR-116*
2. **Given** a ticket for a long-distance journey, **when** it is opened, **then** it shows the live status of that journey with the same wording as the live status screen. *Trace: FR-116*
3. **Given** a Metro ticket and a cancelled regional train, **when** the ticket is opened, **then** it stays valid for the next train and is not refunded automatically. *Trace: FR-108, A-29*
4. **Given** a station terminal or public display, **when** it is shown, **then** it displays no account or ticket information. *Trace: FR-120, D-66*

### AC-E11-09 Automatic refund
1. **Given** a Paid long-distance ticket, **when** the journey is cancelled or terminated before her boarding station, **then** the ticket becomes Cancelled by railway and a refund is requested automatically. *Trace: FR-117, D-65*
2. **Given** the provider confirms the refund, **when** the message is processed, **then** the ticket shows Refunded and she is notified. *Trace: FR-117, NFR-51*
3. **Given** the refund cannot be delivered, **when** the retries continue, **then** the ticket stays Cancelled by railway and the backlog is visible in monitoring. *Trace: NFR-08, OPS-02*
4. **Given** a journey terminated after she has passed her boarding station, **when** it is terminated, **then** her ticket is not refunded automatically. *Trace: A-29*

### AC-E11-10 Cancel my ticket (Could)
1. **Given** a Paid ticket more than 2 hours before departure, **when** she cancels, **then** a full refund is requested. *Trace: FR-118, A-29*
2. **Given** less than 2 hours remain, **when** she opens the ticket, **then** cancellation is not offered. *Trace: A-29*

### AC-E11-11 Delete my account
1. **Given** an account with no paid ticket for a future journey, **when** she deletes it, **then** saved cards are removed at the provider and the link between her and her purchase records is removed. *Trace: FR-119, NFR-49*
2. **Given** a paid ticket for a future journey, **when** she tries to delete, **then** deletion is refused with the reason. *Trace: FR-119*
3. **Given** the account is deleted, **when** the same email signs up again, **then** the new account has no history. *Trace: FR-119*

### AC-E11-12 Limit seat hoarding
1. **Given** one account has made 5 holds in 10 minutes, **when** it makes a sixth, **then** it receives 429 Too Many Requests with Retry-After. *Trace: NFR-52*
2. **Given** one device, **when** it makes many purchase attempts, **then** the device limit applies as well. *Trace: NFR-13, NFR-52*

### AC-E11-13 Ticket without a seat (Should)
1. **Given** a long-distance journey, **when** she taps Buy, **then** she is offered Reserve a seat and Travel without a reserved seat, and either can be chosen. *Trace: FR-108, FR-111, D-67*
2. **Given** she chooses to travel without a seat, **when** she reviews the order, **then** the fare is shown with the note that no seat is guaranteed, and no seat hold or countdown starts. *Trace: FR-122, A-34*
3. **Given** her ticket is Paid, **when** she opens it, **then** it shows No seat reserved with the same code and live status as a reserved ticket. *Trace: FR-116, FR-122*
4. **Given** the journey's unreserved capacity has been reached, **when** she tries to buy without a seat, **then** she is offered a seat reservation or another train. *Trace: FR-122*
5. **Given** two passengers buy the last unreserved place at the same moment, **when** both requests arrive, **then** exactly one succeeds. *Trace: NFR-47*

### AC-E11-14 Counter sales (Should)
1. **Given** a service whose coaches 5 and 6 are allocated to counter sales, **when** a passenger opens the seat map, **then** those seats are shown unavailable and are never selectable. *Trace: FR-123, A-33*
2. **Given** a ticket bought at a counter, **when** the passenger opens My tickets, **then** it does not appear, and the app says nothing about it. *Trace: FR-123*
3. **Given** a journey is cancelled, **when** refunds run, **then** only tickets bought in the app are refunded by SmartRail; counter tickets follow the counter's own rules. *Trace: FR-117, A-33*

### AC-E08-08 Menu feed from the caterer
1. **Given** the caterer's system sends a menu or availability update, **when** it calls SmartRail, **then** it uses a client-credentials token and a call without a valid token is refused. *Trace: FR-07*
2. **Given** the same menu message arrives twice (same message ID), **when** it is processed, **then** the second delivery changes nothing. *Trace: ADR-12*
3. **Given** an update that is older than the stored menu version, **when** it arrives, **then** it is ignored. *Trace: FR-90*
4. **Given** the caterer marks an item sold out, **when** the update is accepted, **then** the passenger list shows Sold out at the next refresh. *Trace: FR-90*
*Added 12 October 2026 (D-72): this is the working integration with the simulated caterer.*

---

## Change log

- **14 October 2026:** Ragama time in the shared timeline changed from 08:40 to 08:42 to match the seed data (`01-database-foundation.md`, accepted 13 October), and AC-E04-03 (1) changed from Ragama 08:48 to 08:50 (08:42 plus 8 minutes). Gampaha 08:38, Maradana 08:58 and Colombo Fort 09:03 are unchanged.
