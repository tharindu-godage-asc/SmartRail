# SmartRail – Requirement Discovery Register

Consolidates discoveries by the categories in brief section 22. Each finding records **where it came from** and **what it led to**. IDs in the Outcome column (A-, P-, D-, ADR-) refer to `smartrail-discovery.md`.

**Sources:** Brief (§ = section) · RDMNS (existing-solution research) · Walkthrough (delay journey scenario) · Persona · Review (challenge of a draft or decision) · AI sim (AI stakeholder simulation; logged in the AI usage log)

**Status:** ✅ resolved by a decision · 🔶 partly addressed · ❓ open / to explore

**D-71 (12 October 2026):** food ordering is designed but not built. The passenger app shows the caterer's menu as a read-only list and a button that opens the caterer's website. Findings that concern the order lifecycle, refunds, tokens, order rate limits or inventory are marked ⏸ and kept as the specification for a later phase. Menu feed stays in the MVP; **D-72 (same day) also defers train events to the caterer** (INT-01, FS-08). The "Discovery Register" Excel workbook has not been updated yet.

---

## 1. Missing actors

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| MA-01 | The brief says "driver / train operator"; a driver cannot safely use an app while driving. A separate on-train **operator** role is needed | Brief §8, Walkthrough | ✅ A-03, D-03 |
| MA-02 | Someone at the station hands food to passengers: a **catering handler** | Review (catering) | ✅ Caterer's employee, uses caterer system (D-13) |
| MA-03 | Brief §9 lists system administration, but no actor manages accounts | Brief §9 | ✅ System Administrator via Keycloak console (D-27) |
| MA-04 | **Passengers without smartphones** | Brief §7 | ✅ Displays (Must), terminal (Could) |
| MA-05 | **Sinhala- and Tamil-speaking passengers** — not mentioned in the brief | RDMNS | ✅ Three languages (D-25) |
| MA-06 | Passengers with **colour-vision deficiency** or visual impairment | Persona | ✅ P-09, D-47, Definition of Done |
| MA-07 | **Station staff** handle platform changes and local questions | Review | 🔶 Designed only; controller covers MVP (D-12) |
| MA-08 | **Timetable / schedule owner** | Brief §9 | ✅ Merged into controller (D-69); scheduling is a controller function |
| MA-09 | **Customer-service representative** | Brief §22 | 🔶 Merged into controller (D-11); ❓ explore in simulation |
| MA-10 | **Emergency services / railway safety systems** | Review (landslide) | ✅ Outside SmartRail (A-05) |

## 2. Missing business processes

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| MP-01 | Delay reporting with reason and estimate | Walkthrough | ✅ D-07 (total delay), reason list |
| MP-02 | Verifying a reason before passengers see it | Walkthrough, RDMNS (unverified data) | ✅ Staged publishing (D-10) |
| MP-03 | Suspend, cancel, terminate, and **resume** a service | Brief §10 | ✅ ADR-05, D-43 |
| MP-04 | Reporting when the app can't connect | Walkthrough | ✅ Phone fallback, controller entry (A-04) |
| MP-05 | Creating each day's **journeys** from the timetable | Review (data model) | ✅ Journey generator (ADR-11) |
| MP-06 | Correcting a mistaken report | Review (wireframe) | ✅ Flag (D-41, Must); full correction (D-40, Should) |
| MP-07 | Refunds when the railway cancels | Review (catering) | ✅ Service agreement, automatic (D-14, D-32) **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| MP-08 | Food collection and confirmation | Brief §12 | ✅ Token at fixed point, eligible stations (D-15) **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| MP-09 | Platform changes | Brief §3 | 🔶 Should (D-12) |
| MP-10 | Closing a line section (landslide, flood) | Review | 🔶 Should (S-01) |
| MP-11 | Push subscription lifecycle and deletion | Review (privacy) | ✅ Deleted after journey (D-31) |
| MP-12 | **Special / temporary services** (festival trains, revised timetables) | RDMNS news feed | ✅ Deferred: timetable remains static seed data |
| MP-13 | **Controller shift handover** (open exceptions, active overrides) | AI sim (controller) | ❓ Needs a handover note, exception ownership, and an optional review time on overrides (a separate pending-work panel would duplicate exceptions and overrides); priority to decide |
| MP-14 | Lost & found, ticketing, reservations | RDMNS | ✅ Won't (out of scope) |
| MP-15 | **Connections** between trains ("available connections from another station") | Brief §3 | ✅ Out of MVP: one seeded line has no interchanges |
| MP-16 | **"Which train should I take?"** — journey planning beyond direct trains | Brief §3 | ✅ Direct-train search answers it on the single seeded line |
| MP-17 | **Route changes / diversions** | Brief §10 | ✅ Should (deferred beyond the first build) |
| MP-18 | **Temporary station closures** | Brief §10 | ✅ Could (station closure override) |
| MP-19 | **Crew change** mid-journey (e.g. new operator from Polgahawela) | AI sim (controller) | ✅ Time-bounded operator assignments; authorisation checked at occurrence time (backdating risk: SEC-18) |

## 3. Missing failure scenarios

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| FS-01 | Operator offline while reporting | Brief §16 | ✅ Offline queue, occurrence time (D-09) |
| FS-02 | Expected report never arrives | Walkthrough | ✅ Overdue detection (D-06) |
| FS-03 | Same report received more than once | Review | ✅ Client event ID (D-35) |
| FS-04 | Reports arrive out of order | Review | ✅ Occurrence-time ordering (P-06) |
| FS-05 | Accidental tap / premature departure | Review (wireframe) | ✅ D-40, D-41, D-49 |
| FS-06 | Phone or display loses connection | Brief §6, §16 | ✅ ADR-09, D-39 |
| FS-07 | Real-time messages missed during reconnect | Review | ✅ Snapshot + push + versions (ADR-09) |
| FS-08 | Caterer unavailable when a train is terminated | Review | ✅ Transactional outbox, retries (ADR-12). **⏸ Designed, not built (D-72)** |
| FS-09 | Webhook delivered twice | Review | ✅ Idempotent receivers (ADR-12) |
| FS-10 | Train breakdown | Review | ✅ Technical fault + Terminate |
| FS-11 | Landslide / flood | Review | 🔶 Section closure (Should) |
| FS-12 | Passenger misses collection; train cancelled before collection station | Brief §12 | ✅ D-14, missed-collection rule |
| FS-13 | Device clock wrong | Review | 🔶 Server offset (ADR-06); ❓ limits to define |
| FS-14 | Passenger loses phone after ordering | Review | 🔶 Counter resolves with order details **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| FS-15 | **Operator's sign-in token expires** during a long offline journey | AI sim (security) | ✅ Token policy (CON-10): capture continues offline; refresh or re-authenticate before sync |
| FS-16 | **SmartRail outage** (backend, identity provider or database down) | AI sim (controller) | 🔶 Operations continue on fallback procedures; operator queues preserve reports; controller enters manual actions afterwards; system recovers via events + snapshot + outbox. Open: FS-21, FS-22 |
| FS-17 | Kasuni misses her train; screen switches to destination | Review | 🔶 Accepted for MVP |
| FS-18 | **Operator device lost or stolen** while signed in | AI sim (security) | 🔶 Impact limited to the assigned journey and traceable; ❓ revocation speed (SEC-12) |
| FS-19 | **Deliberate traffic flood** on anonymous endpoints | AI sim (security) | ❓ SEC-14, SEC-16, OPS-10 |
| FS-20 | **Scripted fake orders** | AI sim (security) | ✅ Unpaid orders never prepared (D-13); 15-minute expiry (MS-11); rate limit (SEC-15) **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| FS-21 | **Recovery storm:** after an outage, overdue detection and notifications fire for every train at once | AI sim (controller) | ❓ Needs a rule like D-43: suppress overdue detection until queues have had time to sync; never send stale notifications |
| FS-22 | **Cross-source duplicates:** same departure entered manually by a controller and synced from an operator queue | AI sim (controller) | ❓ Needs a matching rule across sources |

## 4. Missing states

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| MS-01 | "The railway doesn't know" vs "my screen is offline" | Walkthrough, RDMNS | ✅ Two silences (D-39) |
| MS-02 | Uncertainty as its own status | Walkthrough | ✅ Unknown group (D-47) |
| MS-03 | Suspended, Cancelled, Terminated, Resumed | Brief §10 | ✅ ADR-05, D-43 |
| MS-04 | No train due; end of service day | Review (display) | ✅ D-45 |
| MS-05 | Report Delivered / Not sent yet / Rejected | Review | ✅ Operator stories |
| MS-06 | Full order lifecycle | Brief §12; AI sim (catering) | ✅ Pending → Awaiting Payment → Confirmed → Ready → Collected, with Rejected (from Pending), Expired (from Awaiting Payment), Refunded (from Awaiting Payment, Confirmed, or Ready) — see INT-05, MS-11, INT-09 **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| MS-07 | Stop reported vs inferred | Review | ✅ P-07 (inferred internal only) |
| MS-08 | Connected / Reconnecting / Disconnected | Review | ✅ ADR-09 |
| MS-09 | Loading | Review (components) | ✅ Train card state model |
| MS-10 | **Journey not yet started; no operator assigned** | Gap analysis | ✅ 'Unassigned' status: operators cannot report; controller can assign or enter events (M-17) |
| MS-11 | **Order "Awaiting payment"** with automatic expiry — missing from the order states | AI sim (security) | ✅ Expires after 15 minutes; the caterer expires it and SmartRail mirrors 'Expired'; nothing reserved in SmartRail **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| MS-12 | SmartRail outage vs device offline | AI sim (controller) | ✅ Same state, different wording: "You're offline" (no network) vs "Live updates unavailable" (online, SmartRail not responding) |

## 5. Missing security requirements

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| SEC-01 | Operators may only report for, and read, their assigned journey | Review | ✅ D-28, operator hub groups |
| SEC-02 | Anonymous public hub must carry only passenger-safe data; connection limits | Review | ✅ ADR-09 |
| SEC-03 | Simulated clock must not exist in production | Review | ✅ ADR-06 |
| SEC-04 | Device-supplied times must be validated | Review | 🔶 ❓ limits to define |
| SEC-05 | System-to-system authentication | Review | ✅ Client credentials both ways (ADR-12) |
| SEC-06 | SmartRail never handles payment data | Review | ✅ D-13, ADR-13 |
| SEC-07 | Unverified free text never published | Review | ✅ "Other" to controller only |
| SEC-08 | App separation is not a security control | Review (ADR-07 draft) | ✅ Server-side authorisation |
| SEC-09 | **Threat model** | Brief §17, §23 | ❓ To do |
| SEC-10 | Lost or stolen operator device; token lifetimes | — | ❓ Security engineer simulation |
| SEC-11 | Secrets management, input validation, logging of personal data | Brief §20 | ❓ Security engineer simulation |
| SEC-12 | Lost-device process: who revokes access, how quickly, outside office hours | AI sim (security) | ❓ Revocation possible in Keycloak; process undefined |
| SEC-13 | Token lifetime and refresh policy for offline operators | AI sim (security) | ✅ Access token 15 min, refresh token 8 h; capture never blocked by authentication |
| SEC-14 | Concrete rate limits and connection limits on public endpoints | AI sim (security) | 🔶 Listed in ADR-09; values undefined |
| SEC-15 | Rate limiting of order submission per device | AI sim (security) | ✅ 5 attempts per device per minute; then 429 **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| SEC-16 | Edge protection against deliberate floods | AI sim (security) | 🔶 Deferred beyond MVP |
| SEC-18 | **Backdating risk:** occurrence-time authorisation could let a previous assignee or stolen device bypass reassignment | AI sim (controller) | ❓ Server-corrected time (ADR-06), plausibility limits (SEC-04), exception for post-handover events from previous assignees |
| SEC-19 | **Inventory race condition:** two simultaneous orders for the last menu item | AI sim (catering) | ✅ The caterer's order-acceptance system guarantees only one succeeds (atomic update or optimistic concurrency); entirely inside the caterer's system **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| SEC-17 | **Security test evidence plan:** unauthenticated event rejected; operator limited to assigned journey; invalid events rejected/flagged; audit trail complete; disabled account blocked; offline token behaviour; targeted rate-limit and cache tests; unpaid orders expire; caterer prepares only paid orders | AI sim (security) | 🔶 Plan defined; tests to write |
| PRIV-01 | Device push token + journey is the most identifying passenger data | AI sim (security) | ✅ Deleted after the journey (D-31) |
| PRIV-02 | Operator events form **staff movement data** (employee monitoring) | AI sim (security) | ❓ Who may view it; retention |
| PRIV-03 | **Log content policy:** personal fields in application vs audit logs, masking, access, retention | AI sim (security) | ❓ |
| PRIV-04 | SmartRail holds no passenger names, contacts, or payment data in the MVP (anonymous use, order reference only) | AI sim (security) | ✅ D-26, D-13, ADR-13 |

## 6. Missing operational requirements

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| OPS-01 | Single audit trail for all actions | Review | ✅ D-36 |
| OPS-02 | Undelivered integration messages must be visible | Review | ✅ ADR-12 |
| OPS-03 | Thresholds configurable without code changes | Review | ✅ A-07 |
| OPS-04 | Connection loss visible within 30 seconds | Review | ✅ ADR-09 |
| OPS-05 | **Monitoring, health checks, alerting** | Brief §19; AI sim (product owner) | 🔶 Plan defined: health signals for API, database, SignalR and integration backlog; critical vs non-critical alerts (OPS-11). Implementation is build work |
| OPS-06 | **Backups, data retention, log retention** | Brief §19, §20 | ❓ To do |
| OPS-07 | **Deployment and configuration per environment** | Brief §19 | 🔶 Docker Compose (MVP) |
| OPS-08 | Availability expectations; who supports the system at night | AI sim (product owner) | ✅ 24/7 on-call coverage (OPS-13); severity tiers: critical failures page immediately at any hour, minor failures retry automatically first |
| OPS-09 | **Availability target** (acceptable downtime, by time of day) | AI sim (security, product owner) | ✅ 99.5% monthly for passenger-facing services; peak protected by policy (OPS-12) |
| OPS-10 | Short-lived caching of public snapshots to absorb load | AI sim (security) | ✅ Cache snapshots; invalidate a journey's cache when its version changes; never cache overrides, verification or authentication |
| OPS-11 | SmartRail must detect its own failures; controllers are users, not the monitoring system | AI sim (product owner) | ✅ Monitor API, database, SignalR and integration backlog; critical failures alert an engineer; operations view shows a platform-issue banner |
| OPS-12 | Peak periods protected from planned downtime | AI sim (product owner) | ✅ No planned maintenance or deployments 06:00–09:00 or 16:00–19:00 |
| OPS-13 | Support hours did not cover the 06:00–07:00 morning peak | AI sim (product owner) | ✅ 24/7 support coverage (owner decision); staffing a 24/7 rota is an organisational assumption |

## 7. Missing accessibility requirements

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| ACC-01 | Status never conveyed by colour alone | Persona | ✅ P-09, D-47 |
| ACC-02 | Three languages; side by side on public displays | RDMNS | ✅ D-19, D-25 |
| ACC-03 | Screen readers and text scaling | Persona | ✅ Definition of Done |
| ACC-04 | Contrast to WCAG AA | Review | 🔶 Palette proposed; to verify |
| ACC-05 | Display legibility at platform distance | Review | 🔶 D-50; room test to do |
| ACC-06 | Kiosk reach (wheelchair users), larger targets, reset | Review | 🔶 Terminal (Could) |
| ACC-07 | Large targets, minimal typing for operators | Persona | ✅ Operator wireframes |
| ACC-08 | **Hearing impairment** (announcements are design-only) | Review | 🔶 Displays carry the information |
| ACC-09 | **Plain language / cognitive load**; screen-reader behaviour | AI sim (accessibility) | ✅ Announcement order and wording defined for Kasuni's live status screen in all three states |
| ACC-10 | **Live-region rule** | AI sim (accessibility) | ✅ Announce meaning changes (status, verification, disruption, connectivity); never routine value changes (ETA ticking, timestamps, stop-list refresh) |
| ACC-11 | **Platform-specific accessibility** (React vs Flutter) | AI sim (accessibility) | ✅ React: native elements, headings, landmarks, live regions, keyboard. Flutter: Semantics widgets, labels, focus order, announcements. Wireframe review validates design intent only (D-53) |

## 8. Missing integration cases

| ID | Finding | Source | Outcome |
|----|---------|--------|---------|
| INT-01 | Train events to the caterer, with journey version | Brief §11 | ✅ ADR-12. **⏸ Designed, not built (D-72, 12 Oct 2026).** Brief §11 says the caterer may need railway information: accepted risk |
| INT-02 | Menu availability and order status to SmartRail | Brief §12 | ✅ ADR-12, ADR-13 Menu availability: built (FR-90). Order status: ⏸ deferred (D-71). |
| INT-03 | Ordering through SmartRail, payment on caterer's page | Brief §12 | ✅ ADR-13 **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| INT-04 | Push via Firebase | Review | ✅ D-31 |
| INT-05 | **Caterer rejects an order** (kitchen closed, cutoff passed) | AI sim (catering) | ✅ Pending → Rejected; caterer's reason shown; alternative offered where one exists **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| INT-06 | **Timetable changes** (e.g. tomorrow's service cancelled) reaching the caterer | AI sim (catering) | ❓ Not resolved this session |
| INT-07 | **Collection station unreachable** after a route change | Review | 🔶 Refund rule covers termination; reroutes ❓ **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| INT-08 | Caterer prepares only **paid** orders (SmartRail forwards before payment, hybrid flow) | AI sim (security) | ✅ ADR-13 (confirmed: pay-when-ordering, D-13, not pay-on-collection) **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |
| INT-09 | **Order version** on every caterer-to-SmartRail order message, alongside the message ID | AI sim (catering) | ✅ SmartRail applies only messages newer than its stored version; mirrors the journey-version pattern (ADR-09, ADR-12) **⏸ Deferred (designed, not built, D-71, 12 Oct 2026).** |

## 9. Conflicting requirements

| ID | Conflict | Source | Resolution |
|----|----------|--------|-----------|
| CON-01 | "Driver application" vs safe operation | Brief §8 | Separate operator role (A-03) |
| CON-02 | "Current train locations where appropriate" vs no hardware or GPS | Brief §5, A-02 | Station-level position from events; GPS designed only |
| CON-03 | Caterer is a separate system vs passengers order inside SmartRail | Brief §11, §12 | Hybrid integration (ADR-13) Updated by D-71: the MVP lists the menu and links to the caterer's website; ordering inside SmartRail is designed, not built. |
| CON-04 | Publish quickly vs publish trustworthy information | Walkthrough | Staged publishing (D-10) |
| CON-05 | Reach passengers with notifications vs anonymous, privacy-preserving use | Review | Device tokens, deleted after journey |
| CON-06 | Rich information vs glanceable, trilingual displays | Review | "Words are expensive" (P-11), language-neutral elements |
| CON-07 | Minimal operator effort vs complete data | Persona | Designated arrivals only, inferred stops (D-08, P-07) |
| CON-08 | Correctable reports vs instant propagation | Review | No undo window; flag + controller (D-40, D-41) |
| CON-09 | Breadth of the brief vs one person's capacity (≈3× gap) | Sizing | Thin slices, checkpoint, ranked cut list (D-33) |
| CON-10 | Offline operator resilience (tokens must survive long offline periods) vs fast revocation of a stolen device | AI sim (security) | 15-min access / 8-h refresh; queued events never lost; no passenger tokens (anonymous; Firebase manages push tokens). A stolen device can sync for up to 8 h unless revoked (SEC-12) |

## 10. Ambiguous requirements

| ID | Ambiguity | Source | Interpretation |
|----|-----------|--------|----------------|
| AMB-01 | Who uses the "Driver / Operator" app? | Brief §8 | Operator, not driver (A-03) |
| AMB-02 | "Current train locations **where appropriate**" | Brief §2, §5 | Last reported station and section between stations |
| AMB-03 | "External service providers" (other than catering) | Brief §2 | ❓ Only catering modelled; record as assumption |
| AMB-04 | "Exploring available passenger services" at the terminal | Brief §7 | ❓ Undefined; terminal is Could |
| AMB-05 | Notifications (§13 is one sentence) | Brief §13 | Significant changes only, rules defined |
| AMB-06 | How displays "behave when connectivity is unreliable" | Brief §6 | Last known information, labelled (D-39) |
| AMB-07 | "Appropriate point" for food collection | Brief §12 | Destination or approved long stops (D-15) |
| AMB-08 | Organisational roles listed in §9 | Brief §9 | Operator, Controller (+ customer information), Admin; others designed |
| AMB-09 | "Reporting" as a responsibility | Brief §9 | Historical reporting: Could |
| AMB-10 | **Stopping patterns** must vary | Brief §4 | ✅ Seed at least one express service that skips stops |
| AMB-11 | **Train vehicles** in the domain model | Brief §4 | ✅ Not modelled in the MVP |
| AMB-12 | Displays in **waiting areas** and on **smaller terminal devices** | Brief §6 | ✅ Covered by the two modes: main board (entrances, waiting areas, large screens), platform (platforms, small devices) |
| AMB-13 | What **"station information"** includes (facilities, accessibility, contacts) | Brief §7 | ✅ Should: operational info only (arrivals, departures, disruptions, planned platforms, catering availability, contact number) in passenger app and terminal; no maps, facilities, shops or tourism |

## 11. Important assumptions

See A-01 to A-09 in `smartrail-discovery.md`, plus:

| ID | Assumption |
|----|-----------|
| A-10 | A railway–caterer service agreement defines refunds, cutoffs, and compensation |
| A-11 | Android emulator (Google Play image) is an acceptable test device; real-device check if one can be borrowed |
| A-12 | A native Tamil speaker is available to validate translations in week 3 |
| A-13 | Only the catering provider is modelled as an external service provider |

---

## Completed stakeholder simulations

| Date | Role | Format | Key outcomes |
|------|------|--------|--------------|
| 2 Oct 2026 | Product owner ("Suresh", played by Claude) | Interview | Availability target, peak protection, monitoring ownership, support hours. OPS-08, OPS-09 resolved; OPS-05 partial; new OPS-11 to OPS-13 (24/7 support decided) |
| 2 Oct 2026 | Accessibility specialist ("Nadeesha", played by Claude) | Interview | Screen-reader behaviour for state changes, live-region rule, React vs Flutter implementation. ACC-09 resolved; new ACC-10, ACC-11; decision D-53 |
| 2 Oct 2026 | Catering provider ("Priya", played by Claude) | Interview | Order rejection, inventory race conditions, full order lifecycle, order versioning. New items SEC-19, INT-09; MS-06 and INT-05 resolved |
| 2 Oct 2026 | Railway controller ("Ruwan", played by Claude) | Interview | Shift handover, SmartRail outage, crew changes. New items MP-19, FS-21, FS-22, MS-12, SEC-18; MP-13 and FS-16 updated |
| 1 Oct 2026 | Security engineer ("Dilani", played by Claude) | Interview: AI asks, owner answers from own knowledge | Threats: false operational data, live-service availability, catering fraud. New items CON-10, SEC-12–17, PRIV-01–04, MS-11, OPS-09–10, FS-18–20. Observation: the owner described several already-decided controls as open — reread the decision log before the final review |

## Planned stakeholder simulations

Short, time-boxed AI simulations targeting the open (❓) items. Run each, challenge the output against existing decisions, and log accepted and rejected suggestions.

| Role | Targets |
|------|---------|
| Security engineer | ✅ Done 1 Oct (see above); remaining: SEC-09 threat model write-up, SEC-11 secrets and input validation |
| Accessibility specialist | ✅ Done 2 Oct; remaining work: ACC-04 contrast check, ACC-05 room test |
| Railway controller / station manager | ✅ Done 2 Oct; remaining: MP-13 priority |
| Catering provider | ✅ Done 2 Oct; remaining: INT-06 (timetable changes), INT-07 (reroutes) |
| Customer-service representative | MA-09, AMB-03 |
| Passenger | MP-15, MP-16, AMB-13 |
| QA engineer | Test strategy; edge cases across all categories |
| Product owner | ✅ Done 2 Oct; remaining: AMB-04, priorities at the checkpoint |
