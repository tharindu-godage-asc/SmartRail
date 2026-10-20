# SmartRail – Refined Requirements (MVP)

**Version:** 1.13, 13 October 2026 (v1.13 defers the senior controller, FR-126, D-76; v1.12 adds the senior controller, FR-126, D-75; v1.11 adds the controller in charge, FR-125, D-74; v1.10, 12 October, also defers train events to the caterer, FR-95 and NFR-08, D-72; v1.9 makes food ordering designed, not built and adds the food menu list and the link to the caterer's website, FR-124 and D-71; v1.8, 9 October, adds scheduling by the controller, FR-11A to FR-11D; v1.1 added 8 requirements and tightened 6 after the stakeholder-panel review; v1.2, 5 October, made FR-01 technology-neutral; v1.3 added a safety boundary and track and direction changes; v1.4 models platforms as a table and tightens FR-10 and FR-57; v1.5 adds the line selector (FR-107) and defines the station terminal (FR-85); v1.6 adds ticketing and passenger accounts as a Should (FR-108 to FR-121, NFR-47 to NFR-52; v1.7 makes seat reservation optional and adds FR-122 and FR-123 for unreserved tickets and counter sales); see the change log in §9).
**Status:** reviewed by the owner (FR-68, NFR-04 and NFR-13 confirmed); consolidated from the discovery document, the discovery register, ADR-05 to ADR-13, decisions D-01 to D-76, and the MoSCoW backlog. No new decisions are introduced; anything not yet decided is marked **Proposed**.

**Conventions**
- **FR-** functional requirement, **NFR-** non-functional requirement. Each is written so it can be tested.
- **Priority:** Must / Should / Could (MoSCoW, discovery doc §15). Won't items are listed in §5.
- **Trace:** the decision (D-, ADR-, P-, A-) or register item (MP-, FS-, MS-, SEC-, PRIV-, OPS-, ACC-, INT-, AMB-) the requirement comes from.
- **Actors:** Passenger (Kasuni), Operator (Nimal), Controller (Jayasinghe), Caterer (external system), System Administrator.

---

## 1. Scope

SmartRail provides a single authoritative source of live operational data for one railway line (the Main Line, Veyangoda → Gampaha → Ragama → Maradana → Colombo Fort, plus one long-distance train, Colombo Fort → Badulla), and distributes it to passengers, station displays, operations staff, and an external catering provider. The MVP consists of a backend API, an operator app, an operations web app, a passenger app, station displays, a simulator, and a simulated caterer. Food ordering is designed but not built: the passenger app lists the caterer's menu and links to the caterer's own website, where the passenger orders, pays and collects (D-71).

---

## 2. Functional requirements

### 2.1 Identity and access (E01)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-01 | Operators and controllers shall sign in through a standards-based identity provider (OpenID Connect and OAuth 2.0) that includes a built-in administration console; mobile and web apps use Authorization Code with PKCE. The product chosen, and the alternatives considered, are recorded in ADR-08. | Must | ADR-08, M-01 |
| FR-02 | The API shall enforce role-based permissions for the Operator and Controller roles on every staff endpoint. | Must | M-02, D-28 |
| FR-03 | An operator shall only create or read events for a journey they were assigned to at the event's occurrence time. | Must | D-28, MP-19 |
| FR-04 | Controllers shall be able to view and act on all journeys on the seeded line (line-based access is designed, not built). | Must | D-28 |
| FR-05 | Passengers shall use the passenger app and station displays without an account. | Must | D-26 |
| FR-06 | Staff accounts and roles shall be administered in the identity provider's admin console; SmartRail has no custom admin interface. | Must | D-27 |
| FR-07 | The caterer's system shall authenticate to SmartRail with client-credentials tokens from the identity provider when it sends the menu and availability feed. Authentication in the other direction (SmartRail to the caterer) is designed, not built, with FR-95 (D-72). | Must | ADR-12, D-72 |
| FR-08 | Passengers may optionally create an account. An account is needed only to buy a ticket (FR-110); browsing, tracking, subscribing and viewing the food menu stay anonymous. | Should | D-26, D-62 |

### 2.2 Network reference data and journeys (E02)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-10 | SmartRail shall model lines, stations (a station may be on several lines, with its order and distance on each), services with their stopping patterns, category (regional or long-distance) and direction (outbound or inbound, relative to the line's first station), the platforms of each station (the planned and current platform of a stop refer to one of that station's platforms), designated arrival stops, and collection-eligible stops. | Must | ER diagram, D-08, D-15, AMB-10 |
| FR-11 | SmartRail shall generate one journey per service per operating day, at least 7 days ahead, using the controllable clock's date. | Must | ADR-11, INT-06 |
| FR-11A | The controller shall schedule train services: a weekly schedule (the days of the week a service runs) and extra services for single dates, which can be added at any time. Before pressing Schedule the controller may edit the service's journeys, times, stops and platforms; an Every stop option adds every station on the line in line order. Scheduling generates the journeys (FR-11). | Must | D-69, owner request 9 Oct |
| FR-11B | The controller shall amend a published service. Each amendment creates a new service version: passengers see the latest version, earlier versions stay in the audit trail, affected passengers are notified. Sending the new version to the caterer is designed, not built (FR-95, D-72). | Must | D-70, owner request 9 Oct |
| FR-11C | The controller shall cancel a published service, for one date or from a date onward, with a reason. A service is never deleted: cancellation keeps its history and audit trail, notifies passengers, starts ticket refunds automatically (FR-117) and, once train events are built, sends the cancellation to the caterer (FR-95, deferred D-72). | Must | D-65, D-70, owner request 9 Oct |
| FR-11D | SmartRail shall validate a service before it is scheduled or amended: not in the past, stops in line order, times increasing, each planned platform a platform of its station, and a warning (advice only, as FR-57) if another journey is planned on the same platform within the FR-57 window. | Must | D-69, FR-57 |
| FR-12 | A journey without an assigned operator shall show as Unassigned; operators cannot report on it, and the controller can assign an operator or enter events. | Must | MS-10 |
| FR-13 | Operator assignments shall be time-bounded, so a crew change mid-journey is represented as consecutive assignments. | Must | MP-19 |
| FR-14 | Every station, line and menu item shown to passengers shall have an English name and Sinhala and Tamil translations. | Must | D-25 |
| FR-15 | The seed data shall include at least one express service that skips stops. | Must | AMB-10 |

### 2.3 Operator reporting (E03)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-20 | The operator app shall show the assigned journey (train, route, stops, status) and only the next valid station action; "Report delay" is always available. | Must | M-05, D-42 |
| FR-21 | The operator shall report a departure with one action; the event records its occurrence time (server-corrected). | Must | M-06, ADR-06 |
| FR-22 | The operator shall report arrivals only at designated arrival stations. **CHANGED 20 Oct 2026 (owner, spec 19): arrivals can be reported at every stop that has a scheduled arrival; reporting one is optional. Original wording kept.** | Must | D-08, M-07 |
| FR-23 | The operator shall report a delay with a reason (Signal issue, Technical fault, Obstruction, Weather/natural hazard, Unknown) and an estimated **total** delay or Unknown. | Must | D-07, M-08 |
| FR-24 | Reports made offline shall be queued on the device with a client event ID and occurrence time, sent automatically when connectivity returns, and never lost because a token expired. | Must | D-09, D-35, CON-10 |
| FR-25 | Each report shall show its delivery status: Delivered, Not sent yet, or Rejected (requires Operations review). | Must | M-09 |
| FR-26 | While offline, the next valid action shall be derived from the last server state plus the local queue. | Must | D-42 |
| FR-27 | The operator shall be able to flag their last report as wrong, creating an exception for the controller. | Must | D-41 |
| FR-28 | An active suspension shall be shown to the operator; delay and fault reporting remain available during it. | Must | ADR-05 |
| FR-29 | A phone-fallback screen shall show the operations desk number for the journey's line. | Should | A-04, S-02 |
| FR-30 | The operator may enter a free-text "Other" reason, visible to controllers only. | Should | D-10 |
| FR-31 | The operator app shall retry queued reports automatically with exponential back-off (proposed: start at 5 seconds, double up to a 5-minute cap, with random jitter; values configurable), shall never discard a queued report, and shall keep the queue across app restarts. | Must | FR-24, NFR-05, QA panel review |

### 2.4 Live status, ETA and event processing (E04)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-35 | A report received more than once (same journey and client event ID) shall be recorded once and acknowledged as delivered. | Must | D-35 |
| FR-36 | Journey order shall be judged by occurrence time: forward skips are accepted (skipped stops recorded as inferred); backward conflicts are rejected to the exceptions queue; a second report for the same station with a different time is held for review. | Must | D-09, P-07, discovery doc §8 |
| FR-37 | The same journey, station and event type reported by different sources within 5 minutes shall be treated as one event, with the later report linked for audit. | Must | FS-22 |
| FR-38 | Occurrence times more than 1 minute in the future or older than 8 hours shall be rejected; reports from a previous operator arriving after a handover raise a High exception. | Must | SEC-18, SEC-04 |
| FR-39 | The ETA at each remaining stop shall be the scheduled time plus the current delay. Trains never depart early; negative delay is not carried forward. | Must | D-05, D-48 |
| FR-40 | A train within ±2 minutes (configurable) of schedule shall show as On time; otherwise Delayed. A departure reported before the scheduled time raises a High exception. | Must | D-48, D-49 |
| FR-41 | When a journey's next expected event (plus a grace period) is overdue, SmartRail shall show "Waiting for update" with the last known position and raise an exception. Overdue detection pauses during overrides, restarts from the resume time, and pauses for 10 minutes after a SmartRail outage. | Must | D-06, D-43, FS-21 |
| FR-42 | Delay timing shall be published to passengers immediately; the delay reason only after a controller verifies it. | Must | D-10 |
| FR-43 | All events, including controller actions, shall be stored append-only with source (Operator, Controller, Simulator), author, occurrence time and received time; the events table is protected from alteration (NFR-17). | Must | D-36, P-06 |
| FR-44 | The backend shall provide derived presentation state (status group, status and Level 1 keys, primary ETA and station, last known position, expected next event time) so clients only render it. | Must | D-52 |

### 2.5 Operations control (E05)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-50 | The controller shall see all active journeys with status, source of the latest information, verification state (verified or awaiting verification), time since last update, and the open exception count, updating without refresh. | Must | M-14 |
| FR-51 | The exceptions queue shall hold one exception per report, with severity derived from the reason, ordered by severity then age, limited to the types in discovery doc §19.4. | Must | D-44, P-15 |
| FR-52 | The controller shall verify (or change) a reported delay reason; on verification passengers see it marked "Verified by Operations" with the verification time. | Must | D-10, M-16 |
| FR-53 | The controller shall enter an event on an operator's behalf, recorded with its source. | Must | M-17, A-04 |
| FR-54 | The controller shall suspend, cancel or terminate a journey with a reason; the override stays active until cleared, and every set and clear is audited. An override records a decision made through the railway's control channels; it does not control any train (FR-63). | Must | ADR-05, M-18 |
| FR-55 | The controller shall publish and withdraw disruption messages built from pre-translated templates. | Must | M-19, D-25 |
| FR-56 | The controller shall end an operator's assignment immediately (e.g. lost device), blocking further reports. | Must | SEC-12 |
| FR-57 | The controller may change a train's platform at a station by choosing one of that station's platforms; the change shall reach the displays and the passenger app within the 30-second target in NFR-04. If another journey is planned or set on the same platform within a few minutes (the window is configurable; 10 minutes proposed), the controller sees a warning before confirming and may still confirm; when the two journeys run in opposite directions the warning is marked higher severity and says to check with signalling and control. The same check runs when estimates change and raises a platform-conflict exception for the controller. These are advice based on the data SmartRail holds; SmartRail does not prevent conflicts or detect collisions (FR-63). | Should | D-12, A-05, platform review 8 Oct |
| FR-58 | Controllers may take ownership of an exception and leave a shift handover note. | Should | MP-13 |
| FR-125 | Each journey shall show the controller in charge and since when. A controller takes charge of a journey, or hands it to another controller (with an optional handover note, FR-58); the previous assignment ends at that moment. A journey with nobody in charge shows "No controller in charge", and any controller can still act on it (FR-04); an action by a controller who is not in charge is recorded with the actor and shown. Every take and handover is audited. This adds no role: the roles stay Passenger, Operator, Controller, Caterer and System Administrator. A senior controller can also reassign (FR-126). | Should | D-74, D-75, FR-04, FR-58, owner request 13 Oct |
| FR-126 | A controller who is a member of the identity provider's `senior-controller` group (a senior controller) shall be able to reassign any journey to another active controller, with or without the current controller's agreement; the previous controller is told, an optional handover note is kept, and the change is audited with the actor. A senior controller shall also see journeys and recorded actions grouped by controller. A senior controller keeps all Controller rights and does not approve other controllers' actions. The group is managed in the identity provider's console (FR-06); no role is added. Group membership is read from the sign-in token, so a removal takes effect when the token is next refreshed. | Deferred (designed, not built) | D-75, D-76, FR-125, FR-06, owner request 13 Oct |
| FR-59 | The controller may close a line section (landslide, flood), affecting every journey through it, in both directions (tracks are not modelled, A-24). | Should | S-01 |
| FR-60 | The controller may correct a wrong report with full recalculation of journey state. | Should | D-40 |
| FR-61 | Historical reporting. | Could | C-02 |
| FR-62 | The controller may see exactly what passengers currently see for a journey (status, ETA, reason and verification state). | Could | D-11, customer-service panel review |
| FR-63 | The operations app shall show a permanent notice that SmartRail is an information system and not a safety system, and the override screens shall state that they record a decision and do not control trains. | Must | A-05, A-24, owner question on collisions |
| FR-107 | The operations app shall let the controller choose a line from a collapsible list in the navigation. The overview, exceptions, journeys, overrides, disruptions and line closures then show only that line's data, the chosen line is named on every screen, and the list shows each line's open-exception count so that a problem on another line is never hidden. The list contains the lines that have seeded services (the MVP seeds the Main Line). | Should | FR-50, FR-51, A-25, owner request 8 Oct |

### 2.6 Passenger journey information (E06)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-65 | The passenger shall find upcoming trains between two stations, showing scheduled time, status and live ETA; the origin is remembered on the device. | Must | M-21 |
| FR-66 | The live status screen shall show the ETA at the passenger's boarding station, switching to the destination once the train departs it, with a single stop list. | Must | D-38, M-23 |
| FR-67 | The app shall distinguish train silence ("Waiting for update") from viewer silence ("You're offline, showing information from…"), and warn when offline information is likely out of date. | Must | D-39, M-24 |
| FR-68 | The home screen shall show the followed train, search, and service alerts. ("Trains at my station" is not shown: passengers at the station see it on the main board and platform displays, and passengers away from it can use search.) | Must | P-13 |
| FR-69 | The passenger shall choose Sinhala, Tamil or English. | Must | D-25 |
| FR-70 | The passenger may save a regular journey. | Should | M-22 |
| FR-71 | The passenger may view operational station information (departures, disruptions, platforms, catering availability, contact number). | Should | AMB-13 |
| FR-72 | The passenger shall be able to see, on demand, when the information for a journey was last updated (the time of the latest accepted event), even when the information is current. | Should | NFR-07, passenger panel review |

### 2.7 Notifications (E07)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-75 | The passenger shall subscribe to a journey with "Notify me", without an account; the subscription is deleted when the journey ends. | Must | D-31 |
| FR-76 | Android push notifications shall be sent for cancellation, suspension, termination, and delay above a threshold (5 minutes regional, 10 minutes long-distance, A-07c); never for an overdue report, and never for an event older than 5 minutes. | Must | D-31, FS-21 |
| FR-77 | iOS push, notification preferences, and platform-change notifications. | Should | D-31, D-12 |

### 2.8 Station experience (E09)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-80 | A main board mode shall list upcoming trains with time, destination, platform and status. | Must | D-18, M-33 |
| FR-81 | A platform display mode shall show one train, with the answer to "When is my train arriving?" as the largest element. | Must | D-50, M-32 |
| FR-82 | Displays shall handle connected, train overdue, display offline, disrupted, no train due, and end-of-service states. | Must | D-39, D-45, M-34 |
| FR-83 | Public displays shall show Sinhala, Tamil and English side by side, use language-neutral elements where possible, and show no verification badge. | Must | D-19, D-46 |
| FR-84 | Announcement text shall be generated from events. | Should | D-21 |
| FR-85 | A station terminal, built as a mode of the display web app, for passengers without the app: the passenger chooses a language, then a destination without typing (large buttons for common places, or an A to Z list), then sees the next trains with time, platform and status, and can open a train's details (stops, status, last update, verified reason when published) or the station's service alerts. Controls sit in the lower half of a large portrait touchscreen. When live updates are lost it shows scheduled trains marked not live. After a configurable period without touch it shows a countdown, then clears the language and choices. It shows the backend's state and never calculates its own ETA. | Could | C-01, P-01, ACC-06, AMB-04 |

### 2.9 Catering (E08)

**Decision D-71 (12 October 2026): food ordering is designed but not built.** In the MVP the passenger app shows a read-only list of the caterer's menu items and a button that opens the caterer's own website, where the passenger orders, pays and collects. SmartRail keeps the one integration direction that needs no orders: the menu and availability feed from the caterer (FR-90), which is the working integration with the simulated caterer (brief section 23). **D-72 (12 October 2026) also defers the other direction:** train events to the caterer (FR-95, NFR-08) are designed, not built, so the caterer receives nothing from SmartRail in the MVP and the website link carries only a station code and train number. Brief section 11 says the caterer may need railway information; this is a risk the owner accepted (see the cut record). Requirements marked **Deferred (designed)** keep their wording as the specification for a later phase; they are not built, tested or demonstrated. SmartRail does not know which orders exist (A-35).

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-90 | Passengers shall see a read-only list of the caterer's menu items for the food stations on their journey (FR-91): the name in the chosen language (English when there is no translation), the price for information, and available or sold out shown with text and an icon. The list cannot be used to select or order. Availability comes from the caterer's feed; the screen shows when the menu was last updated and, when the phone is offline or the feed has been silent, says the menu may be out of date (P-02). | Must | ADR-13, M-27, D-71 |
| FR-91 | Menus shall be listed only for the passenger's destination and approved long stops (5+ minutes) with catering facilities, the same stations as the collection rule (D-15). Cutoffs, ordering and collection are the caterer's, on its own website. | Must | D-15, D-71 |
| FR-92 | Orders shall be forwarded to the caterer with journey and collection station; payment happens on the caterer's hosted page; SmartRail stores only an order reference. | Deferred (designed) | ADR-13, D-13, D-71 |
| FR-93 | The app shall show order status: Pending, Awaiting Payment, Confirmed, Ready, Collected, Not Collected, Rejected (with reason), Expired, Refunded. | Deferred (designed) | MS-06, D-71 |
| FR-94 | The ordering cutoff shall be fixed to the timetable. | Deferred (designed) | D-16, D-71 |
| FR-95 | Train events (delays, ETAs, cancellations and terminations, including future journeys) shall be sent to the caterer through a transactional outbox with retries (policy in NFR-08), a message ID, and the journey version. The caterer uses them for its own preparation and for any refunds under the service agreement; SmartRail does not know which orders exist (D-71). | Deferred (designed) | ADR-12, INT-06, D-72 |
| FR-96 | Order status messages from the caterer shall be applied once (message ID) and only if newer than the stored order version. | Deferred (designed) | INT-09, D-71 |
| FR-97 | If the railway cancels or terminates a journey before the collection station, the caterer shall refund automatically. | Deferred (designed) | D-14, D-32, D-71 |
| FR-98 | A one-time collection token (QR and code) shall be stored on the device so it works offline. | Deferred (designed) | D-15, M-30, D-71 |
| FR-99 | Order attempts shall be limited to 5 per device per minute. | Deferred (designed) | SEC-15, D-71 |
| FR-100 | The simulated caterer shall provide a menu and availability feed with a way to change availability, and a stand-in page for the caterer's website that the passenger app can open. It takes no orders or payments and, since D-72, receives no train events (its event log, applying each message ID once and ignoring an older journey version, is designed, not built). Order screens, a hosted payment page, automatic refunds, 15-minute expiry of unpaid orders and protection against selling the last item twice are designed but not built (D-71). | Must | MS-11, SEC-19, D-71 |
| FR-101 | Item unavailable after ordering: replacement or refund choice. | Deferred (designed) | S-03, D-71 |
| FR-102 | Cancellation of orders for very large delays. | Deferred (designed) | C-04, D-71 |
| FR-124 | The food menu screen shall have one button that opens the caterer's own website outside SmartRail (the address is a setting), with a note that ordering, payment and collection happen there and that SmartRail does not see the order. The link may carry the station code and train number, never an account, device or passenger identifier. When the phone is offline the button explains this instead of opening a page. When the journey is cancelled or terminated before the food station, the screen says so, tells the passenger to ask the caterer about any order already placed, and the button is not shown (proposed). | Must | D-71, A-35, NFR-21 |

### 2.11 Ticketing and passenger accounts (E11)

*Priority: Should (D-60). Specified, designed and wireframed; built only after the Must slices (§8, rank 0).*

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-108 | SmartRail shall sell two kinds of ticket, matching the service category. A **Metro** ticket (data value Regional) is for a station pair on a chosen date, is valid on any regional service between those stations that day, and has no seat. A **long-distance** ticket is for one journey and station pair; a reserved seat is an optional extra, never required. | Should | D-61 |
| FR-109 | The fare shall be calculated from the distance between the two stations on the line and the service category, using a seeded rate card, and shown in full before payment. | Should | D-61, A-26 |
| FR-110 | Passengers shall be asked to sign in or create an account only when they choose to buy; the chosen trip is kept through sign-in. | Should | D-62 |
| FR-111 | For a long-distance ticket the passenger shall be offered, and may skip, seat reservation. If they reserve, they shall see the train's coaches and seats, with seats already taken, held back for counter sales or sold for their part of the trip unavailable, and choose one seat per ticket (up to six) before payment. | Should | D-61, A-28 |
| FR-112 | A chosen seat shall be held for the passenger for 10 minutes while they pay; the same seat shall never be sold twice for overlapping parts of a journey, and an unpaid hold shall release the seat when it expires. | Should | D-64, SEC-19 |
| FR-113 | Payment shall take place on the simulated payment provider's hosted page; SmartRail shall send the amount and an order reference and receive the result, and shall never see card numbers. | Should | D-63, SEC-06 |
| FR-114 | On the hosted payment page the passenger may optionally choose to save the card for later. SmartRail shall store only the provider's token reference, card brand, last four digits and expiry month and year. The passenger can pay without saving, can see saved cards in the account screen and can remove one at any time. | Should | D-63 |
| FR-115 | A signed-in passenger shall see their purchase history: upcoming and past tickets, most recent first, each with trip, date, ticket type, seat, price paid and status (Held, Paid, Cancelled by railway, Refunded, Expired, Used or past). | Should | D-62 |
| FR-116 | A paid ticket shall show a code that is readable without a network connection, and shall link to the live status of its journey. Checking tickets on trains or at gates is not built (A-30). | Should | D-61, A-30 |
| FR-117 | If the railway cancels or terminates a journey before the passenger's boarding station, the ticket shall be refunded automatically through the payment provider, the ticket shall show Cancelled by railway then Refunded, and the passenger shall be notified, using a transactional outbox (ADR-12). | Should | D-65, A-29 |
| FR-118 | A passenger may cancel their own ticket before departure and be refunded under the refund rule in A-29. | Could | D-65 |
| FR-119 | A passenger may delete their account. Saved cards shall be removed at the provider, and purchase records shall be kept without the link to the person for the period in NFR-49. | Should | D-62, PRIV-04 |
| FR-120 | Station terminals and public displays shall not show ticket or account information; the terminal may show the fare for a trip but sells nothing. | Should | D-66, A-31 |
| FR-121 | The simulated payment provider shall provide a hosted payment page, a token store for saved cards, automatic refunds, a way to simulate a declined or abandoned payment, and 10-minute expiry of unpaid payments. | Should | D-63, ADR-14 |
| FR-122 | A long-distance ticket without a seat reservation shall be sold up to the journey's unreserved capacity (a seeded number); the passenger is told that no seat is guaranteed, and when the capacity is reached is offered a seat reservation or another train. | Should | D-67, A-34 |
| FR-123 | Tickets shall not have to be bought in the app: the railway also sells at station counters, outside SmartRail (A-33). Seats allocated to counter sales shall never be offered in the app's seat map, so a seat is not sold twice across channels. Tickets bought at counters do not appear in the app's history. | Should | D-68, A-33 |

### 2.10 Simulator and time (E02)

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| FR-105 | A simulator shall replay journeys through the same API as the operator app, using data-defined scenarios (delay, silence, delayed delivery, incident). | Must | D-30, M-04 |
| FR-106 | All time-dependent logic shall use a controllable clock; the backend is the authority on current time and apps use a server offset; demos support step and pause. | Must | ADR-06 |

---

## 3. Non-functional requirements

### 3.1 Availability and reliability

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| NFR-01 | Passenger-facing services shall achieve 99.5% monthly availability. | Must | D-55, OPS-09 |
| NFR-02 | No planned maintenance or deployments 06:00–09:00 or 16:00–19:00. | Must | OPS-12 |
| NFR-03 | Loss of connection shall be visible to users within 30 seconds. | Must | ADR-09 |
| NFR-04 | When connected, an accepted event shall be reflected in the passenger app and on displays within **30 seconds**, and journey status shown as live shall never be more than **90 seconds** old (otherwise it is shown as not current). Published disruption messages shall reach all channels within the same 30 seconds. Aligned with the GTFS Realtime best practices (refresh at least every 30 seconds; trip updates no older than 90 seconds; service alerts no older than 10 minutes). | Must | ADR-09, GTFS Realtime best practices |
| NFR-05 | No accepted operational event shall be lost during an outage or recovery; the operator's offline queue shall survive app restarts. | Must | FS-16, D-35 |
| NFR-06 | After an outage, clients shall recover to the correct state automatically (snapshot + push), without manual reconstruction. | Must | FS-16, ADR-09, ADR-12 |
| NFR-07 | Clients shall never present stale information as current (never-silent principle). | Must | P-02 |
| NFR-08 | (Deferred with FR-95, D-72.) When the caterer is reachable, train events shall be delivered to it within 30 seconds of being accepted. Failed deliveries shall retry with exponential back-off (proposed values, configurable); a message undelivered for more than 15 minutes, or a growing backlog, shall raise an alert. | Deferred (designed) | ADR-12, OPS-02, QA and catering panel review, D-72 |

### 3.2 Security

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| NFR-10 | Every staff and system endpoint shall require a valid token; authorisation is enforced server-side (app separation is not a security control). | Must | ADR-07, D-28 |
| NFR-11 | Operator access tokens shall last 15 minutes and refresh tokens 8 hours. | Must | CON-10, SEC-13 |
| NFR-12 | The public hub and public endpoints shall carry passenger-safe data only (never free-text reasons, unverified reasons or internal exceptions). | Must | ADR-09 |
| NFR-13 | Every endpoint shall be rate-limited, following OWASP API4:2023 (Unrestricted Resource Consumption), with limits keyed on a token or device identity rather than IP alone, responding **429 Too Many Requests** with a Retry-After header. MVP values (derived from expected usage, since there is no numeric industry standard): passenger app 60 requests per minute per device installation ID and 2 live connections per device; an IP backstop of 600 requests per minute (mobile carriers put many passengers behind one IP); displays identified by a per-display key and exempt from passenger limits; order submission 5 per device per minute (FR-99, designed with ordering, not built: D-71); request bodies limited in size; sign-in protected by the identity provider's brute-force detection; cached snapshots absorb repeated reads (OPS-10). | Must | SEC-14, SEC-15, OPS-10, OWASP API4:2023 |
| NFR-14 | Secrets shall never be committed to source control, and all API input shall be validated. | Must | SEC-11 |
| NFR-15 | The simulated clock shall not exist in production builds. | Must | ADR-06 |
| NFR-16 | A STRIDE threat model shall be produced, and the security tests in SEC-17 shall pass. | Must | SEC-09, SEC-17 |
| NFR-17 | The application's database account shall not be able to update or delete rows in the events table (insert and read only), so the audit trail cannot be altered through the application. | Must | D-36, FR-43, security panel review |

### 3.3 Privacy and data retention

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| NFR-20 | SmartRail shall hold no payment card data in any form. Anonymous passengers (browsing, tracking, viewing the food menu) leave no name or contact details; a passenger who buys a ticket is identified in SmartRail only by the identity provider's account ID (NFR-49). | Must | PRIV-04, SEC-06, D-13, D-62 |
| NFR-21 | Data shared with the caterer shall be limited to journey, station and timing data (and order data, if ordering is built later); no passenger identity. The link to the caterer's website carries no passenger, device or account data (FR-124). | Must | ADR-13, D-71 |
| NFR-22 | Push subscriptions shall be deleted when the journey ends; order references (including the device installation ID), if ordering is built later, 30 days after the journey (none exist in the MVP, D-71); ticket and purchase records follow NFR-49. | Must | D-31, OPS-06 |
| NFR-23 | Operational events shall be kept 12 months, then the operator ID removed; staff movement data is visible to controllers and administrators only, and operators see only their own. | Must | PRIV-02 |
| NFR-24 | Application logs shall contain no personal data (IDs only) and be kept 30 days, accessible to engineers only. | Must | PRIV-03 |
| NFR-25 | The database shall be backed up daily, backups kept 30 days, with one restore test recorded as evidence. | Must | OPS-06 |

### 3.4 Accessibility and localisation

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| NFR-30 | Status shall never be conveyed by colour alone (icon + text + colour); one icon, one meaning. | Must | P-09, P-10, D-47, D-51 |
| NFR-31 | Text and status colours shall meet WCAG AA contrast, verified with tools. | Must | ACC-04 |
| NFR-32 | Screen readers shall be supported (native HTML/ARIA in React, Semantics in Flutter). Only meaning changes are announced automatically: on time → delayed, delayed → waiting for update, reason verified, cancellation, suspension, termination, and offline or live updates unavailable. Disruptions (cancellation, suspension, termination) are announced with priority; routine value changes such as an ETA ticking are never announced. | Must | D-53, D-54 |
| NFR-33 | Text shall scale without truncating times or statuses. | Must | M-25 |
| NFR-34 | All passenger-facing text shall be available in Sinhala, Tamil and English and validated by native speakers. | Must | D-25 |
| NFR-35 | Display text shall be legible at platform distance, verified by a room test. | Must | D-50, ACC-05 |
| NFR-36 | An automated test shall show that a change in ETA alone causes no screen-reader announcement, and that each trigger listed in NFR-32 causes exactly one. | Should | D-54, accessibility panel review |

### 3.5 Operability, maintainability and testability

| ID | Requirement | Priority | Trace |
|----|-------------|----------|-------|
| NFR-40 | SmartRail shall monitor API, database, SignalR and integration backlog health; critical failures alert an on-call engineer 24/7. | Must | OPS-05, OPS-11, OPS-13 |
| NFR-41 | Thresholds (on-time, grace period, notification threshold, long-stop dwell, recovery pause, duplicate window) shall be configurable without code changes. | Must | A-07 |
| NFR-42 | The controllable clock shall make time-based behaviour testable without waiting; five critical flows shall have automated end-to-end tests. | Must | ADR-06, M-36 |
| NFR-43 | The MVP shall run locally with Docker Compose (API, identity provider, database, simulated caterer). | Must | OPS-07 |
| NFR-44 | The apps shall run on Android (emulator with Google Play, plus one real-device check if available); displays on a modern browser at 1080p. iOS is designed only. | Must | A-11, D-31 |
| NFR-45 | Network failure shall be injectable in tests: the emulator's network can be switched off to exercise the operator app's offline queue, and the simulator can deliver events late with their original occurrence times. | Must | ADR-06, M-36, QA panel review |
| NFR-46 | Rebuilding a journey's state from its events in occurrence order shall give the same state as the live journey state, and this shall be checked by a test. | Should | ADR-11, QA panel review |
| NFR-47 | A test shall show that two passengers buying the last free seat at the same moment produce exactly one paid ticket, that two buying the last unreserved place do the same, and that an expired hold releases its seat. | Should | FR-112, SEC-19 |
| NFR-48 | SmartRail shall store, for a saved card, only the provider's token reference, brand, last four digits and expiry; no card number, security code or cardholder data shall appear in the database, logs or messages. | Should | SEC-06, FR-114 |
| NFR-49 | For a ticket buyer SmartRail shall hold only the identity provider's account ID (name and email stay in the identity provider). Purchase records shall be kept 12 months after the journey date, then the account link shall be removed; on account deletion the link is removed at once. | Should | PRIV-04, A-32 |
| NFR-50 | Ticket, history and saved-card endpoints shall require sign-in and return only the caller's own records; a test shall show that one passenger cannot read or change another's ticket. | Should | NFR-10, D-62 |
| NFR-51 | Payment and refund messages between SmartRail and the payment provider shall be signed, applied once (message ID) and ignored if older than the stored version, as for the caterer (INT-09). | Should | ADR-12, ADR-14 |
| NFR-52 | Purchase attempts shall be limited per account and per device (at most 5 holds per account in 10 minutes) with a 429 and Retry-After, so seats cannot be hoarded. | Should | NFR-13, FR-99 |

---

## 4. Constraints and assumptions

See A-01 to A-13 in the discovery document. Key ones: SmartRail is fictional, modelled on a Sri Lankan-style network (A-01); no hardware (A-02); the operator is not the driver (A-03); safety systems and emergency response are outside SmartRail (A-05); a railway–caterer service agreement exists (A-06, A-10); payments are simulated (A-09). Food ordering is not built; the caterer's own website takes food orders (D-71, A-35). Tracks are not modelled (A-24). The safety boundary is stated in A-05: SmartRail is information only.

## 5. Out of scope (Won't, this time)

Real payments and real card handling · staff ticket inspection, gates and paper tickets · season passes, concessions and group fares · ticket resale or transfer · journeys with changes between trains · seat choice on Metro tickets · GPS tracking (designed only) · station staff app · driver as a SmartRail user · speed restrictions, signalling, emergency coordination, collision avoidance, movement authority and train protection · audio announcements and PA systems · caterer handler app · in-coach delivery · machine-learning ETAs · crowd-sourced passenger reports · custom admin UI · "trains at my station" on the passenger home screen (shown on station displays instead) · additional lines · connections between trains · route changes (Should, deferred) · news feed.

Also out of scope this time (designed, not built, D-71): **food ordering inside SmartRail**: placing and paying for an order, order status, collection tokens and refunds shown in the app (FR-92 to FR-94, FR-96 to FR-99, FR-101, FR-102). The caterer's own website takes food orders.

Also out of scope: **track-level modelling** (tracks, single and double track working, crossing loops, track occupancy), because passenger information needs platforms and direction, not tracks (A-24).

## 6. Open points to confirm

| Item | Needed by |
|------|-----------|
| ER questions 3–4 (exception owner column, JSON payload) | Before Slice 1 |
| Back-off values (FR-31, NFR-08) and the 15-minute alert threshold: proposed, to confirm | Before Slice 2 |
| Cut order (§8): to approve at the checkpoint | 9 October |

## 7. Summary

| Group | Must | Should | Could | Deferred (designed, not built) |
|-------|------|--------|-------|--------------------------------|
| Functional | 62 | 29 | 4 | 11 |
| Non-functional | 33 | 8 | 0 | 1 |
| **Total (148)** | **95** | **37** | **4** | **12** |

Every requirement traces to a decision or register item; the register (Requirement Discovery tab) holds the discovery history behind each one.


---

## 8. Cut order for the 9 October checkpoint

**Purpose:** if progress at the checkpoint shows the Musts will not fit, this list says what to cut, in what order, so the decision is quick and traceable. It is a **proposal for the owner to approve at the checkpoint**, not a decision made now.

**Rules**
1. Never cut a deliverable the brief requires (§23): the operator, operations and passenger apps, the station experience, the simulator, the catering integration, the five end-to-end tests, the threat model and security evidence, and the AI usage record. Thin them instead.
2. Protect the **spine** first: operator report → event → ETA → passenger app and display → controller verification. Cut away from it, never into it.
3. Cut breadth before depth: finish one path fully before adding variants.
4. Cut in the order below until the estimated remaining effort fits the remaining days.
5. Record every cut: requirement ID, new priority (Should or Could), reason, and the problem statement or point of view it affects.
6. **Trigger:** at the checkpoint, if Slices 0–4 are not working, or measured speed shows the remaining Musts will not fit.

**Ranked cuts (first to cut → last)**

| Rank | Cut | Requirements | Action | Relative saving | Harm to the core problem |
|------|-----|--------------|--------|-----------------|--------------------------|
| 0 | **Ticketing build** (decided Should, D-60) | FR-108 to FR-121, NFR-47 to NFR-52 | Not built unless Slices 0 to 7 are done; the specification, schema and wireframes stay as deliverables. Order if time allows: Metro purchase with accounts and history, then seat reservation, then saved cards, then passenger cancellation | Highest (about 4 to 6 days) | None: the core problem does not depend on it |
| 1 | Android push notifications | FR-75, FR-76 | Demote to Should; keep the in-app change banner while the app is open | High (about 2.5 days, from the epic sizing) | Medium: passengers aren't reached with the app closed, but the information problem is solved on every other channel |
| 2 | ~~Collection token QR code~~ | FR-98 | Superseded by D-71: no collection token is built | None | None |
| 3 | ~~Simulated caterer extras~~ | FR-100 | Superseded by D-71: the extras belong to ordering, which is not built | None | None |
| 4 | Extra display states | FR-82 | Demote "no train due" and "end of service day" to Should | Low | Low: affects idle platforms only |
| 5 | Flag my last report | FR-27 | Demote to Should; the phone route remains | Low | Low: controllers already see conflicts |
| 6 | Remembered origin station | part of FR-65 | Drop; choose both stations each time | Low | Low |
| 7 | Detailed public limits | NFR-13 | Keep the order limit (FR-99) and a basic per-device limit; drop the IP backstop and connection limits | Medium | Medium: the security evidence still shows one rate limit working |
| 8 | Main board offline behaviour | FR-80, FR-82 | Main board shows live data only; offline handling stays on the platform display | Low to medium | Medium |

**Protected (not cut):** operator reporting and event processing (FR-20 to FR-26, FR-35 to FR-44), identity and authorisation (FR-01 to FR-03, NFR-10 to NFR-12), controller verification and overrides (FR-50 to FR-54), passenger live status and never-silent states (FR-65 to FR-67, NFR-03, NFR-07), the platform display (FR-81), the catering integration core (menu list and link FR-90, FR-91, FR-124; the menu and availability feed is the working integration), the simulator and clock (FR-105, FR-106), and the end-to-end tests and threat model (NFR-16, NFR-42, NFR-45).

**Cut record (to complete at the checkpoint)**

| Date | Requirement | Old priority | New priority | Reason | Problem or POV affected |
|------|-------------|--------------|--------------|--------|-------------------------|
| 12 Oct 2026 | FR-92, FR-93, FR-94, FR-96, FR-97, FR-98, FR-99, FR-101, FR-102 | Must (FR-101 Should, FR-102 Could) | Deferred (designed) | Owner decision D-71: food ordering moves to the caterer's own website; SmartRail shows the menu list and a link. Reduces the catering build; the saving is not yet re-estimated | Kasuni's goal of collecting food reliably is reduced to information and a link; the integration the brief requires stays (FR-95, FR-90) |
| 12 Oct 2026 | FR-95, NFR-08 | Must | Deferred (designed) | Owner decision D-72: train events to the caterer are not built. The menu and availability feed (FR-90) and the website button (FR-124) remain. Brief section 11 says the caterer may need railway information, so this thins a brief-required area: the owner accepted the risk. Saving not re-estimated | Kasuni's food screen cannot warn the caterer of delays; the caterer works from its own timetable |

---

## 9. Change log

**Version 1.1, 5 October 2026.** Prompted by the Gemini stakeholder-panel review (ten roles). Findings were checked against this document before any change; claims the panel got wrong (for example, that text scaling or the assigned-journey rule were missing) were rejected. No requirement was removed.

**New requirements (8)**

| ID | Priority | Source of the finding |
|----|----------|-----------------------|
| FR-31 | Must | QA engineer: no retry policy for the operator's offline queue |
| NFR-08 | Must | QA engineer and catering provider: no delivery target or retry policy for the outbox |
| NFR-17 | Must | Security engineer: the audit trail could be altered |
| NFR-45 | Must | QA engineer: no way to inject network failure in tests (also needed for end-to-end test #2) |
| NFR-36 | Should | Accessibility specialist: announcement behaviour untested |
| NFR-46 | Should | QA engineer: no proof that recovery is correct |
| FR-62 | Could | Customer-service representative: no view of what passengers see |
| FR-72 | Should | Passenger: no way to see how old current information is |

**Tightened requirements (6): same ID, wording extended**

| ID | Previous wording | Reason |
|----|------------------|--------|
| FR-43 | All events, including controller actions, shall be stored append-only with source (Operator, Controller, Simulator), author, occurrence time and received time. | Audit trail must be protected (security) |
| FR-50 | The controller shall see all active journeys with status, source of the latest information, time since last update, and the open exception count, updating without refresh. | Controllers need verification state in the overview (customer service) |
| FR-52 | The controller shall verify (or change) a reported delay reason; on verification passengers see it marked "Verified by Operations". | Verification time was in the design but not the requirement (passenger) |
| FR-57 | The controller may change a train's platform at a station. | Platform changes need a propagation target (station manager) |
| FR-95 | Train events (delays, ETAs, cancellations and terminations, including future journeys) shall be sent to the caterer through a transactional outbox with retries, a message ID, and the journey version. | Links the retry policy (QA, catering) |
| NFR-32 | Screen readers shall be supported (native HTML/ARIA in React, Semantics in Flutter); only meaning changes are announced automatically. | Exact announcement triggers and priority (accessibility, passenger) |

**Version 1.2 (review comment).** FR-01 no longer names a product. The requirement now states the capability the design depends on (a standards-based identity provider with a built-in administration console, which FR-06 relies on), and the product choice is left to ADR-08.

| ID | Previous wording | Reason |
|----|------------------|--------|
| FR-01 | Operators and controllers shall sign in through the identity provider (Keycloak); mobile and web apps use Authorization Code with PKCE. | Requirements should say what is needed, not which product; evaluators may ask how the choice was made, and that belongs in ADR-08 |

**Version 1.3, 7 October 2026 (safety boundary, tracks and direction).** Prompted by the owner's questions about tracks and about two trains approaching each other. No requirement was removed.

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-63 | New (Must) | none | State the safety boundary in the product: information only, overrides record a decision and do not control trains |
| FR-10 | Tightened | SmartRail shall model lines, stations (a station may be on several lines, with its order and distance on each), services with their stopping patterns and category (regional or long-distance), planned platforms, designated arrival stops, and collection-eligible stops. | Services carry a direction |
| FR-54 | Tightened | The controller shall suspend, cancel or terminate a journey with a reason; the override stays active until cleared, and every set and clear is audited. | An override records a decision; it does not control trains |
| FR-59 | Tightened | The controller may close a line section (landslide, flood), affecting every journey through it. | Tracks are not modelled (A-24), so a closure applies to both directions |

**Version 1.4, 8 October 2026 (platforms).** Prompted by the owner's question on how to tell which train is on which platform. No requirement was removed or added; counts are unchanged (118).

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-10 | Tightened | SmartRail shall model lines, stations (a station may be on several lines, with its order and distance on each), services with their stopping patterns, category (regional or long-distance) and direction (outbound or inbound, relative to the line's first station), planned platforms, designated arrival stops, and collection-eligible stops. | Platforms are records of a station, and a stop's planned and current platform refer to them |
| FR-57 | Tightened | The controller may change a train's platform at a station; the change shall reach the displays and the passenger app within the 30-second target in NFR-04. | The new platform must be one of that station's platforms; the controller is warned of a same-platform conflict within a few minutes (advice only) |

**Version 1.4 addendum, 8 October 2026 (opposite directions).** The owner asked for a collision alert mechanism when two trains from opposite directions are headed to the same platform. FR-57 was extended; no new requirement.

| ID | Change | Previous wording (v1.4) | Reason |
|----|--------|-------------------------|--------|
| FR-57 | Tightened | ...may change a train's platform at a station by choosing one of that station's platforms; the change shall reach the displays and the passenger app within the 30-second target in NFR-04. If another journey is planned or set on the same platform within a few minutes (the window is configurable; 10 minutes proposed), the controller sees a warning before confirming and may still confirm. The warning is advice; SmartRail does not prevent conflicts (FR-63). | Opposite directions on one platform are marked higher severity (uses the direction field); the check also runs when estimates change and raises an exception. It stays advice: SmartRail only knows what is reported, so a missing warning is not proof of safety (A-05) |

**Version 1.5, 8 October 2026 (line selector and station terminal).** Prompted by reviewing the operations dashboard and station terminal wireframes. No requirement was removed; one was added.

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-107 | New (Should) | none | The operations app covers more than one line; the controller needs to choose a line, and a problem on another line must not be hidden |
| FR-85 | Tightened | A three-screen station terminal. | The wireframes define the terminal's interaction rules (language, no typing, lower-half controls, offline state, reset after inactivity), previously only in the discovery document |

**Version 1.6, 8 October 2026 (ticketing and passenger accounts).** The owner asked why tickets were not considered and asked for them, with two kinds of train (metro and long-distance, with seat reservation on long-distance), accounts required at purchase, optional saved payment details and purchase history. Decisions D-60 to D-66, ADR-14. Priority is Should: specified and designed, built only after the Musts. Nothing was removed; four existing statements changed.

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-108 to FR-121 | New (13 Should, 1 Could) | none | Ticketing, seats, payment, saved cards, history, refunds, account deletion |
| NFR-47 to NFR-52 | New (6 Should) | none | Last-seat race, card data, account data and retention, own-records-only access, signed payment messages, purchase limits |
| FR-08 | Tightened | Passengers may optionally create an account. | An account is now needed to buy a ticket, and only for that |
| NFR-20 | Tightened | SmartRail shall hold no passenger names, contact details or payment data in the MVP. | Ticket buyers need an account. Card data is still never held; names and email stay in the identity provider and SmartRail keeps only the account ID |
| NFR-22 | Extended | ...order references (including the device installation ID) 30 days after the journey. | Ticket and purchase records have their own rule (NFR-49) |
| §5 Won't | Revised | "Seat reservations and ticketing" and "real payments" were out of scope (MP-14) | Ticketing is now a Should. Real payments, staff ticket inspection, season passes, resale and changes between trains stay out. The Won't line also listed "line selector", which contradicted FR-107 (Should); it was removed |
| §8 cut order | Extended | ranks 1 to 8 | Rank 0 added: ticketing is built last |

**Version 1.7, 8 October 2026 (seat reservation optional, counter sales).** The owner asked whether seat reservation on long-distance trains is optional (it was not: FR-108 and FR-111 required a seat) and asked that the design always allow for buying at station counters. Decisions D-67 and D-68.

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-108 | Tightened | ...A long-distance ticket is for one journey and station pair and carries a reserved seat. | A passenger can buy a long-distance ticket without a seat |
| FR-111 | Tightened | For a long-distance ticket the passenger shall see the train's coaches and seats, with seats already taken for their part of the trip unavailable, and choose one or more seats (up to six) before payment. | Seat choice is now an optional step; counter-held seats are also unavailable |
| NFR-47 | Extended | A test shall show that two passengers buying the last free seat at the same moment produce exactly one paid ticket, and that an expired hold releases its seat. | The unreserved capacity has the same last-one race |
| FR-122, FR-123 | New (Should) | none | Unreserved long-distance tickets with a capacity; counter sales as another channel outside SmartRail |

**Version 1.13, 13 October 2026 (senior controller deferred).** The owner decided to keep the senior controller as future work (D-76). FR-126 moves from Should to Deferred (designed, not built); nothing was removed or added. Counts: functional Should 30 to 29, deferred 10 to 11; total stays 148 (Should 37, Deferred 12).

**Version 1.12, 13 October 2026 (senior controller).** The owner asked for someone responsible for the other controllers' work (D-75). One requirement was added, FR-126 (Should); nothing was removed. Counts: functional Should 29 to 30; total 147 to 148. No role is added: a senior controller is a Controller in the identity provider group `senior-controller`.

**Version 1.11, 13 October 2026 (controller in charge).** The owner asked for the controller in charge of each journey to be shown next to the crew (D-74). One requirement was added, FR-125 (Should); nothing was changed or removed. Counts: functional Should 28 to 29; total 146 to 147. No role is added: System Administrator accounts and roles stay in the identity provider's console (FR-06), so controllers take and hand over journeys themselves.

**Version 1.10, 12 October 2026 (train events to the caterer deferred).** The owner decided that train events to the caterer are designed, not built (D-72). FR-95 and NFR-08 move from Must to Deferred (designed); FR-07, FR-100, NFR-06, FR-11B, FR-11C and FR-117 are reworded. No requirement was deleted or added.

**Version 1.9, 12 October 2026 (food ordering deferred).** The owner decided that food ordering is designed but not built: the app shows the menu items as a list and a button sends the passenger to the caterer's website (D-71). Nothing was deleted; the ordering requirements keep their wording and are marked Deferred (designed). One requirement was added.

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-90 | Tightened | Passengers shall browse menus only for collection-eligible stations on their journey; availability comes from the caterer and unavailable items cannot be ordered. | The menu is now a read-only list with its last-updated time |
| FR-91 | Tightened | Collection shall be offered only at the passenger's destination or approved long stops (5+ minutes) with catering facilities. | SmartRail no longer offers collection; the rule only decides which menus are listed |
| FR-92, FR-93, FR-94, FR-96, FR-97, FR-98, FR-99 | Deferred (designed), were Must | Order forwarding, order status, ordering cutoff, order status messages, automatic refund, collection token, order rate limit | The caterer's own website takes orders, payment and collection |
| FR-101, FR-102 | Deferred (designed), were Should and Could | Replacement or refund choice; cancellation for large delays | Both act on orders |
| FR-95 | Extended | Train events ... shall be sent to the caterer through a transactional outbox ... | Says the caterer uses them for its own preparation and refunds; kept as the working integration |
| FR-100 | Tightened | The simulated caterer shall provide order and menu screens, a hosted payment page, automatic refunds, 15-minute expiry of unpaid orders, and protection against selling the last item twice. | Now a menu feed, an event log and a stand-in website; no orders or payments |
| FR-124 | New (Must) | none | The button to the caterer's website, with its offline and cancelled-journey behaviour |
| FR-08, FR-11C, FR-117, NFR-20, NFR-21, NFR-22, NFR-13 | Wording only | Mentioned food orders, food-order refunds or order data | Aligned with D-71 |

Counts: functional Must 69 to 63 (seven deferred, one added), Should 29 to 28, Could 5 to 4; nine deferred; total 145 to 146. The cut-order ranks 2 and 3 are superseded. The saving in build effort is not yet re-estimated; see the cut record in section 8.

**Rejected panel claims (already covered):** text scaling (NFR-33), assigned-journey authorisation (FR-03), the 3× capacity gap quantified (discovery §16), conflict handling (FR-36, FR-37), out-of-order behaviour (AC-E03-02), the offline-versus-silent distinction (D-39), message versioning (ADR-12, INT-09), and the maximum age of current data (NFR-04, FR-41).

**Assumptions added or extended:** A-10 (waste compensation) and A-23 (station staff escalate urgent platform issues to the controller by phone or radio); see `smartrail-assumptions.md`.

**Version 1.8, 9 October 2026 (scheduling by the controller).** The owner asked for the controller to schedule trains (a weekly schedule, extra trains at any time, editing before scheduling, an Every stop option) and decided that scheduling stays with the controller rather than a new role (D-69), with published schedules versioned and never deleted (D-70). No requirement was removed; four were added.

| ID | Change | Previous wording | Reason |
|----|--------|------------------|--------|
| FR-11A | New (Must) | none | Timetable was seed data (FR-11 only generated journeys); the controller now creates the weekly schedule and extra services |
| FR-11B | New (Must) | none | Amending a published service must be versioned so passengers, tickets and the caterer see one consistent latest version |
| FR-11C | New (Must) | none | A service is cancelled, never deleted, so history and refunds are preserved |
| FR-11D | New (Must) | none | A bad schedule reaches every passenger, so it is checked first; platform clashes are advice only, in line with FR-57 and FR-63 |

Counts: functional Must 65 to 69; total 141 to 145. Not yet defined: how affected tickets are reviewed when stops are added or removed, the push-notification type for a schedule amendment (FR-76 lists only cancellation, suspension, termination and delay), and platforms taken out of service (not modelled). Feature-freeze risk: four new Musts with the freeze on 15 October; see the cut order in section 8.
