# SmartRail – Identified Assumptions

**Deliverable:** Requirements → Identified assumptions (brief §23)
**Source:** discovery document §2, decision log, discovery register, stakeholder simulations.

An assumption is something the design relies on that has **not been confirmed** with the (fictional) railway organisation. Each entry records why it was made, what it affects, and how it would be confirmed or what happens if it proves wrong. IDs A-01 to A-13 are unchanged from the discovery document; A-14 onwards were made explicit while consolidating the requirements. A-35 was added on 12 October 2026 with decision D-71 (food ordering designed, not built).

**Status values:** *Accepted* – adopted for the MVP · *Working* – adopted for now, to be confirmed by the owner · *Deferred* – belongs to a design that is kept but not built (D-71).

---

## 1. Organisation and context

| ID | Assumption | Why / impact | Confirm or risk if wrong | Status |
|----|-----------|--------------|--------------------------|--------|
| A-01 | SmartRail is a fictional organisation modelled on a Sri Lankan-style national network, with real lines, stations and three languages. | Grounds the design in real geography and real language needs (found through the RDMNS research). | Brief §1 calls the organisation fictional; real data is used for realism only. | Accepted |
| A-06 | A railway–caterer service agreement exists. | Lets refund, cutoff and compensation rules come from a contract rather than from SmartRail or the caterer alone (D-14). | If no agreement exists, refunds depend on caterer policy and passengers may lose money for railway cancellations. | Accepted |
| A-10 | The service agreement defines refunds, cutoffs, collection deadlines and compensation, **including compensation for food prepared but wasted because of a delay (not only a cancellation)**; SmartRail does not enforce these policies (refines A-06). Since D-71 the caterer applies them to the orders on its own website, using its own information (SmartRail sends it no train events in the MVP, D-72). | Keeps payment and refund logic with the caterer (D-13). | If the agreement is silent on delay-related waste, the caterer bears it; to confirm with the caterer (catering panel review). | Accepted |
| A-13 | Only the catering provider is modelled as an external service provider. | The brief (§2) mentions "external service providers" without naming others (AMB-03). | Other providers would need their own integration design. | Accepted |
| A-14 | The railway staffs a 24/7 on-call support rota for SmartRail. | Required by the 24/7 support decision (OPS-13); covers the 06:00 morning peak. | A national railway can staff this; in a smaller organisation, support hours would need to shrink and alerts be re-tiered. | Accepted |

## 2. People and roles

| ID | Assumption | Why / impact | Confirm or risk if wrong | Status |
|----|-----------|--------------|--------------------------|--------|
| A-03 | The **train operator** is a separate on-train role (guard or conductor-like) from the driver. The operator reports events; the driver is not a SmartRail user. | No one reports while driving (safety). The brief says "driver / operator" loosely (§8). | If every train lacks an operator, reports fall back to the controller by phone (A-04) or station staff (designed only). | Accepted |
| A-04 | If the operator's app cannot send, the operator can phone the operations desk for their line, and the controller enters the event on their behalf. | Keeps live data flowing during connectivity loss; linked reports are not duplicated (FS-22). | Depends on voice coverage, which may also fail; railway radio would be the fallback. | Accepted |
| A-15 | Operators are issued railway-owned Android devices. | Supports device management, lost-device revocation (SEC-12) and the Android-only MVP. | If personal phones are used, device security and privacy rules need revisiting. Assumed yes; lowest priority to confirm. | Accepted |
| A-23 | Station staff escalate urgent platform problems to the controller by phone or radio, and make local announcements themselves until the controller's platform change appears. | Station staff have no SmartRail tool in the MVP (D-12), and platform changes are a Should. | If the controller is slow to act, passengers get no local update from SmartRail; accepted risk (station-manager panel review). | Accepted |
| A-12 | A native Tamil speaker is available to validate translations in week 3. | All three languages are implemented (D-25); public text must be validated by native speakers. | If unavailable, Tamil is marked "pending native-speaker validation" in the demo. Assumed yes; lowest priority to confirm. | Accepted |

## 3. Scope, safety and data

| ID | Assumption | Why / impact | Confirm or risk if wrong | Status |
|----|-----------|--------------|--------------------------|--------|
| A-02 | No hardware is implemented; phones, tablets and browsers are the only devices. | Rules out GPS units, PA systems and train sensors; live data comes from operator events (D-04). | GPS tracking is designed for later. | Accepted |
| A-05 | Safety-critical data (speed restrictions, signalling) and emergency response are outside SmartRail; railway radio, signalling and emergency services control train movement. SmartRail informs only. This includes collision avoidance, movement authority and train protection: SmartRail never controls trains or detects conflicts between them. | Keeps SmartRail out of safety-critical scope. | Including safety data would require certification-grade engineering beyond the MVP. | Accepted |
| A-24 | Tracks are not modelled. SmartRail models lines (routes), stations, platforms and services; it does not model physical tracks, single or double track working, crossing loops or track occupancy. A closed line section (FR-59) closes both directions. | Passenger information needs platforms and direction, not tracks; track detail belongs to infrastructure and signalling systems (A-05). | A closure that affects only one track is shown as closing the whole section; delays caused by single-track working appear only as late trains. | Accepted |
| A-25 | A controller can see every line; the line selector in the operations app filters the view and does not restrict access. | The MVP seeds one line (Main), and per-line permissions would need roles or assignments per line, which are not designed. | If controllers must be limited to their own lines, add a line scope to the controller role and to the exceptions queue. | Working (to confirm) |
| A-08 | Planned platforms come from timetable seed data. | The controller records only changes, not every platform (D-12). | — | Accepted |
| A-09 | Payments in the prototype are simulated. Ticket payments use the simulated payment provider (A-27). Food is paid for on the caterer's own website, outside SmartRail (D-71). | No real payment provider. The simulated caterer takes no payments; its stand-in page only gives the link somewhere to open. | — | Accepted |
| A-26 | Fares come from a seeded rate card: a price per kilometre by category (Metro, Long-distance) using the distance on the line, with a minimum fare. No concessions, season passes or group fares. | Gives FR-109 a rule that can be tested without a real tariff. | Real fares are set by the railway; only the rate card would change | Working |
| A-27 | The railway has an agreement with its payment provider covering hosted card entry, tokenised saved cards and refunds, so SmartRail never handles card data. The provider is simulated (D-63, ADR-14). | Keeps SEC-06 true while allowing saved cards. | If the provider cannot tokenise, saving cards is dropped and everything else stands | Working |
| A-28 | Each long-distance service has a seat layout (coaches and seats) in the seed data. A seat is reserved for the passenger's part of the journey only (between boarding and alighting stop), so it can be sold again after they get off. A held seat is released after 10 minutes unpaid. | Needed for FR-111 and FR-112; part-journey sales avoid wasting capacity. | Selling a seat for the whole journey only would be simpler and less efficient | Working |
| A-29 | Refund rules: a journey cancelled or terminated before the passenger boards is refunded in full automatically; a passenger who cancels at least 2 hours before departure is refunded in full (Could). The 2 hours is configurable. | The service agreement decides in reality (as A-10 for food). | Real rules may charge a fee | Working |
| A-30 | Checking tickets on trains or at gates is not built. The ticket shows a code that works offline; no staff app exists (Won't). | There is no ticket-inspection role in the brief. | A railway would add scanning later | Working |
| A-31 | Online, tickets are bought only in the passenger app; station terminals and displays sell nothing. The railway also sells at station counters (A-33). | Keeps card entry off shared public screens (D-66). | A terminal that sells needs its own payment flow | Working |
| A-32 | Passenger accounts live in a separate passenger realm of the identity provider, apart from staff. Name and email stay there; SmartRail holds the account ID. Purchase records are kept 12 months after the journey, then unlinked (real tax rules may require longer). | Keeps personal data out of SmartRail (NFR-49). | Retention may need to be longer in a real system | Working |
| A-33 | The railway also sells tickets at station counters, with its own counter system outside SmartRail. To stop double sales, each long-distance service's seats are split by channel in the seed data: seats allocated to counters never appear in the app's seat map. Counter tickets are not visible in the app and SmartRail cannot check or refund them. | The owner asked that the design always allow for counter sales. Without an integration, a fixed split is the simplest safe rule. | A real railway would share one live seat inventory with the counter system; that integration is not built | Working |
| A-34 | A long-distance ticket without a seat is allowed. Each service has an unreserved capacity (a seeded number); the passenger is told no seat is guaranteed. Seat reservation costs nothing extra by default (a per-seat fee is configurable, default Rs 0). | Seat reservation is optional (D-67); an unreserved ticket still needs a limit so the train is not oversold. | A real tariff may charge for reservation, and may allow standing | Working |
| A-35 | The caterer runs its own public website where passengers order, pay and collect food. SmartRail opens it from a link and neither sees nor stores the orders. The link works without parameters; it may add a station code and train number. The caterer applies its service agreement to its own orders (A-10) using its own information, because SmartRail sends it no train events in the MVP (D-72). | Lets SmartRail leave ordering out (D-71) while still pointing passengers to food. The simulated caterer provides a stand-in page. Prices and availability in SmartRail's menu list come from the caterer's feed and may differ on its website. | If the caterer has no such website, the menu list is information only. If it cannot take the parameters, the passenger finds the station on its site. Confirm the address and parameters with the caterer. | Working |
| A-16 | Timetables are static for the MVP; journeys are generated 7 days ahead, and timetable changes are handled as cancellations. | Special and temporary services are deferred (MP-12); cancelling a future journey triggers caterer refunds (INT-06). | Frequent timetable changes would need a timetable-change workflow. | Accepted |
| A-18 | Station names, order and distances come from the railway's published line data and are verified before seeding. | The pasted station list was AI-generated and inconsistent (claims 86 stops, lists 79). | Unverified data would put stations in the wrong order. | Accepted |

## 4. Technology and environment

| ID | Assumption | Why / impact | Confirm or risk if wrong | Status |
|----|-----------|--------------|--------------------------|--------|
| A-11 | An Android emulator with Google Play is an acceptable test device; a real device is used for a final check if one can be borrowed. | The owner has an iPhone; push notifications need Google Play services. | Background push behaviour may differ on a real device. | Accepted |
| A-19 | Passengers have intermittent mobile connectivity, and mobile carriers place many passengers behind shared IP addresses. | Drives offline states (D-39) and device-keyed rate limits with a high IP backstop (NFR-13). | — | Accepted |
| A-20 | Station displays run a modern web browser at 1080p on the station network, identified by a per-display key. | Displays are a web app (D-23) exempt from passenger rate limits (NFR-13). | Older display hardware may need a lighter page. | Accepted |
| A-21 | The caterer can supply menus with names in three languages, a menu and availability feed, and client-credentials authentication (webhooks from SmartRail to the caterer are designed, not built, D-72). | Required by the integration (ADR-13, D-71); the simulated caterer provides all of them. A hosted payment page is no longer needed (D-71). | A real caterer may need an adapter. | Accepted |
| A-22 | The free tier of the Gemini API is acceptable for stakeholder simulations, because the project data is fictional. | Free-tier inputs may be used to improve Google's products. | Real organisational data would need a paid tier with data protection terms. | Accepted |

## 5. Configurable values (initial settings)

All values are configurable without code changes (A-07, NFR-41).

| ID | Setting | Initial value | Source | Status |
|----|---------|---------------|--------|--------|
| A-07 | Thresholds are configurable; initial values are assumptions to confirm with the railway. | — | Discovery doc §2 | Accepted |
| A-07a | On-time threshold | ±2 minutes | D-48 | Accepted |
| A-07b | Overdue grace period after the next expected event | 3 minutes | Industry: Network Rail attributes delays of 3 minutes or more and publishes station punctuality within 3 minutes | Accepted |
| A-07c | Delay above which a push notification is sent | 5 minutes (regional and commuter); 10 minutes (long-distance) | Industry: Public Performance Measure (PPM) treats a train as on time within 5 minutes (regional) or 10 minutes (long-distance) | Accepted |
| A-07d | Minimum scheduled stop for food collection (since D-71 it decides which stops get a menu list, FR-91) | 5 minutes | D-15 | Accepted |
| A-07e | Overdue detection pause after an outage | 10 minutes | FS-21 | Accepted |
| A-07f | Notifications suppressed for events older than | 5 minutes | FS-21 | Accepted |
| A-07g | Cross-source duplicate window | 5 minutes | FS-22 | Accepted |
| A-07h | Unpaid order expiry (ordering is designed, not built, D-71) | 15 minutes | MS-11 | Deferred |
| A-07i | Journey generation horizon | 7 days | INT-06 | Accepted |
| A-07j | Menu freshness limit: after this long without a menu update, or when offline, the menu list says it may be out of date | 60 minutes (proposed) | FR-90 | Working |

---

## Summary

| Status | Count |
|--------|-------|
| Accepted | 31 |
| Working (to confirm) | 12 |
| Deferred (design kept, not built) | 1 |

**Changes after owner review:** A-17 removed (Kandy is **not** part of the Main Line); A-07b and A-07c set to industry timings; A-12 and A-15 assumed yes (lowest priority to confirm).

**Changes on 12 October 2026 (D-71, food ordering designed, not built):** A-35 added; A-09, A-10, A-21 and A-07d reworded; A-07h marked Deferred; A-07j (menu freshness limit) added.
