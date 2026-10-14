# SmartRail – Identified Edge Cases

**Deliverable:** Requirements → Identified edge cases (brief §23)
**Source:** delay walkthrough, five stakeholder simulations and the Gemini stakeholder panel (ten roles) (security, controller, catering, accessibility, product owner), wireframe and design reviews, and the discovery register (FS-, MS-, SEC-, INT-, ACC- items).

An edge case is a situation outside the normal flow that the system must handle deliberately. Each is recorded with the **expected behaviour**, where it is **specified** (an acceptance-criteria section or decision), and how it will be **tested**.

**Handling:** ✅ Handled (decided and specified) · 🔶 Partly handled · ⏸ Deferred (a deliberate decision, with a reason; includes food ordering, designed but not built, D-71) · ❓ Open
**Test layer:** U unit · I integration · E2E automated end-to-end · M manual or evidence-based (screenshots, room test, device)
**Risk:** H / M / L, from impact on passengers or operations multiplied by likelihood.

---

## 1. Operator reporting and connectivity

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-01 | Operator offline when reporting | Queue locally with occurrence time and client event ID; send automatically later | AC-E03-02 (3) | I, E2E | H | ✅ |
| EC-02 | Operator taps the same button repeatedly | Recorded once; shown as Delivered; no error | AC-E03-02 (4), AC-E04-01 | U, I | H | ✅ |
| EC-03 | Same event reported by operator and (by phone) controller | Linked within a 5-minute window, not duplicated | AC-E04-01 (2) | I | M | ✅ |
| EC-04 | Report arrives out of order but occurrence times are valid | Accepted; journey judged by occurrence time | AC-E03-02 (8), AC-E04-02 (4) | U, E2E | H | ✅ |
| EC-05 | Same station reported with a different time | Held for review; exception raised | AC-E03-02 (5) | U, I | M | ✅ |
| EC-06 | Operator forgets a stop's report | Later report accepted; skipped stop inferred; no exception; passengers see normal progress | AC-E03-02 (6) | U, I | M | ✅ |
| EC-07 | Backward report (earlier station after a later one) | Rejected to the exceptions queue | AC-E03-02 (7) | U | M | ✅ |
| EC-08 | Departure reported before scheduled time | Accepted; High exception (D-49) | AC-E03-02 (10) | U | M | ✅ |
| EC-09 | Operator taps the wrong action | No undo; flag the report; controller reviews; full correction is Should | AC-E03-06 | I | M | 🔶 |
| EC-10 | Occurrence time in the future or older than 8 hours | Rejected | AC-E04-02 (1)(2) | U | M | ✅ |
| EC-11 | Device clock is wrong | Server offset applied (ADR-06); plausibility limits | AC-E04-02, FR-38 | U, I | M | ✅ |
| EC-12 | Next action while reports are queued | Derived from server state plus local queue | AC-E03-02 (11) | U | M | ✅ |
| EC-13 | Operator has no journey assigned | "No journey assigned"; no reporting actions | AC-E03-01, AC-E02-01 (5) | U, M | L | ✅ |
| EC-14 | Operator sees a suspension | Shown with reason; Report Delay still available | AC-E03-01 (6) | I | M | ✅ |
| EC-15 | Operator reports while the journey is Unassigned | Refused | AC-E02-01 (5) | I | L | ✅ |
| EC-16 | Delay estimate lower than the current delay | Option not offered; backend flags unusual values | AC-E03-04 (6) | U | M | ✅ |
| EC-17 | Operator doesn't know the delay length | "Unknown" estimate; passengers see it stated honestly | AC-E03-04 (5) | U | M | ✅ |
| EC-18 | Free-text reason entered | Visible to controllers only; never published (Should) | AC-E05-03 | I | M | ⏸ |
| EC-19 | Queued report keeps failing to send | Retried with back-off; never discarded; queue survives an app restart | AC-E03-05 (5)(6) | I | H | ✅ |

## 2. Crew, devices and identity

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-20 | Crew change mid-journey | Time-bounded assignments; events judged by assignment at occurrence time | AC-E01-03 (4) | I | M | ✅ |
| EC-21 | Offline report from a previous operator syncs after handover | Accepted if occurred during their assignment; High exception raised | AC-E04-02 (3) | I | M | ✅ |
| EC-22 | Backdated event to bypass reassignment | Time limits; exception for post-handover events | SEC-18, AC-E04-02 | I | H | ✅ |
| EC-23 | Operator device lost or stolen | Controller ends assignment; further reports rejected; earlier reports retained | AC-E05-07 | I, E2E | H | ✅ |
| EC-24 | Access token expires while offline | Reports keep queueing; refresh or re-authenticate before sync; none lost | AC-E01-01 (3)(4) | I, M | H | ✅ |
| EC-25 | Operator calls a controller-only endpoint | 403 | AC-E01-03 (3) | I | H | ✅ |
| EC-26 | Request with no token | 401 | AC-E01-03 (1) | I | H | ✅ |
| EC-27 | Operator reports for a journey they're not assigned to | Rejected and logged | AC-E01-03 (2) | I, E2E | H | ✅ |

## 3. Live status, ETA and "never silent"

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-30 | No report when the next event is expected | "Waiting for update"; ETA replaced; High exception; no push | AC-E04-04, AC-E06-03 | U, E2E | H | ✅ |
| EC-31 | ETA time has already passed | Shown only as "waiting"; never as a current ETA | AC-E06-03 (1) | U | H | ✅ |
| EC-32 | Phone offline | "You're offline"; last known ETA kept and marked | AC-E06-03 (3) | I, M | H | ✅ |
| EC-33 | Phone online but SmartRail unreachable | "Live updates unavailable" | AC-E06-03 (4) | I | M | ✅ |
| EC-34 | Phone offline long enough that the train is probably overdue | Told the information is likely out of date (device time plus server offset) | AC-E06-03 (5) | U | M | ✅ |
| EC-35 | Train arrives early | Early arrival waits; departure stays scheduled; no negative delay | AC-E04-03 (2) | U | M | ✅ |
| EC-36 | Delay of 1–2 minutes | Shown as On time | AC-E04-03 (3) | U | L | ✅ |
| EC-37 | Delay estimate is total, not additional | 10-minute estimate means 10 minutes in total | AC-E04-03 (4) | U | M | ✅ |
| EC-38 | Overdue detection while a journey is suspended | Paused; restarts from the resume time | AC-E04-04 (3)(4) | U, I | H | ✅ |
| EC-39 | Overdue storm after an outage | Detection paused 10 minutes; stale notifications dropped | AC-E04-04 (5) | I | H | ✅ |
| EC-40 | Kasuni misses her train | Primary ETA switches to destination; accepted limitation | AC-E06-02 (2) | M | L | 🔶 |
| EC-41 | Stop passed without a report shown to passengers | Shown as passed with no "missing" wording | AC-E06-02 (5) | U | L | ✅ |
| EC-42 | Verified reason arrives while the screen is open | Appears; announced to screen readers | AC-E06-05 (5) | I, M | L | ✅ |
| EC-43 | Passenger can't judge how old "current" information is | Last-updated time available on demand (Should) | FR-72 | M | M | ⏸ |

## 4. Operations control

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-50 | Operator event arrives during a suspension | Suspension stays; exception raised | AC-E05-02 (7), AC-E05-05 (2) | I | H | ✅ |
| EC-51 | Override left active after the problem clears | Stays until the controller clears it; never auto-removed | AC-E05-05 (3) | I | M | ✅ |
| EC-52 | Two overrides on one journey | Refused until the first is cleared | AC-E05-05 (5) | I | L | ✅ |
| EC-53 | One report generates two exceptions | Exactly one exception per report | AC-E05-02 (2) | U | M | ✅ |
| EC-54 | Non-actionable items flooding the queue | Forward skips, late deliveries, sync retries and quiet overridden trains raise nothing | AC-E05-02 (5) | U | M | ✅ |
| EC-55 | Controller enters an event that conflicts with journey order | Warned before saving | AC-E05-04 (3) | U | L | ✅ |
| EC-56 | Shift handover loses work in progress | Exception ownership and a handover note (Should) | MP-13 | M | M | ⏸ |
| EC-57 | Landslide or flood on a line section | Section closure affecting all journeys through it (Should) | S-01 | I | M | ⏸ |
| EC-58 | Platform change at short notice | Controller changes platform; displays update (Should) | D-12 | I | M | ⏸ |
| EC-59 | Wrong report standing until later reports correct it | Known MVP limitation; full correction is Should | D-40 | M | M | 🔶 |
| EC-60 | SmartRail itself is down | Operations continue on phone and radio; entries made afterwards with real times | J7 | M | H | ✅ |
| EC-61 | Platform issue noticed by the controller instead of by monitoring | SmartRail monitors itself; the operations view shows a platform-issue banner | NFR-40 | M | M | ✅ |
| EC-62 | A platform change takes too long to appear | Reaches displays and the app within the 30-second target (Should) | FR-57, NFR-04 | I | M | ⏸ |
| EC-63 | A landslide blocks one track only | The MVP closes the section in both directions (A-24); the notice names the section, not the track | FR-59, A-24 | M | M | 🔶 |
| EC-64 | A controller or passenger reads an override as train control | A permanent information-only notice, and override wording that says it records a decision and does not control trains | FR-63, AC-E05-05 | M | H | ✅ |
| EC-65 | Two trains approach each other on the same track | SmartRail neither detects nor prevents conflicts between trains; signalling and control systems do (A-05) | A-05 | M | H | ⏸ |
| EC-66 | The controller enters or selects a platform that does not exist at that station | The platform is chosen from the station's own platforms, so an invalid one cannot be entered; a platform of another station is rejected by the database (composite key) and the API | FR-10, FR-57 | U | M | ⏸ |
| EC-67 | Two journeys are planned or set on the same platform within a few minutes | The controller sees a warning before confirming a platform change (Should) and may still confirm; SmartRail does not prevent it and does not detect train conflicts | FR-57, A-05 | U | M | ⏸ |
| EC-68 | Two journeys running in opposite directions are planned or set on the same platform | A higher-severity platform-conflict warning to the controller (when changing the platform and when estimates change), telling them to check with signalling and control. Advice only: SmartRail holds reported data, so a late or silent train can hide a conflict, and it neither detects collisions nor stops trains (A-05) | FR-57, FR-63, A-05 | U | H | ⏸ |
| EC-69 | An exception arises on a line the controller is not viewing | The line list shows each line's open-exception count, so a Critical or High exception on another line is visible without switching (Should) | FR-107, FR-51 | U | H | ⏸ |
| EC-81 | The chosen line has nothing to show in a tab (no exceptions, overrides or closures) | The tab shows a plain message naming the line, not a blank table | FR-107 | U | L | ⏸ |
| EC-82 | A disruption message is about a station served by several lines (Colombo Fort, Maradana) | It appears under every line that serves the station, and under any line named on the message | FR-107, FR-55 | U | M | ⏸ |
| EC-187 | Two controllers take charge of the same journey at the same moment | One succeeds; the other is told who is in charge and can take over | AC-E05-10 (5) | I | M | ⏸ |
| EC-188 | The controller in charge goes off shift or their account is disabled | The journey shows "No controller in charge"; any controller can still act (FR-04) | AC-E05-10 (6) | I | M | ⏸ |
| EC-189 | A controller who is not in charge acts on a journey | Allowed and recorded with the actor; the journey still shows who is in charge | AC-E05-10 (4) | I | L | ⏸ |
| EC-190 | (Future work, D-76) A senior controller reassigns a journey while the current controller is mid-action | The action in progress is recorded with its own actor; the reassignment applies from that moment and the previous controller is told | AC-E05-11 (2) | I | M | ⏸ |
| EC-191 | (Future work, D-76) A senior controller is removed from the group while signed in | Rights end at the next token refresh; Controller rights stay | AC-E05-11 (5) | I | M | ⏸ |
| EC-192 | (Future work, D-76) A senior controller reassigns to a disabled or non-controller account | Refused; nothing changes | AC-E05-11 (3) | I | L | ⏸ |

## 5. Passenger app and notifications

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-70 | First launch, no language chosen | Language selection first | AC-E06-04 (5) | M | L | ✅ |
| EC-71 | No followed train | Search is first; no empty card | AC-E06-04 (2) | M | L | ✅ |
| EC-72 | Overdue train | No push notification | AC-E07-02 (3) | I | M | ✅ |
| EC-73 | Notification is stale when ready to send | Dropped if older than 5 minutes | AC-E07-02 (4) | I | M | ✅ |
| EC-74 | Delay near the threshold | Regional 5 min, long-distance 10 min | AC-E07-02 (1) | U | M | ✅ |
| EC-75 | App closed when the change happens | Push reaches the device | AC-E07-01 (2) | M (device) | M | ✅ |
| EC-76 | Journey ends | Subscription deleted | AC-E07-01 (3) | I | L | ✅ |
| EC-77 | Push on an emulator vs a real phone | Emulator limitation recorded; a real-device check if one is available | A-11 | M | L | 🔶 |
| EC-78 | Station name missing a translation | Falls back to the English name; test enforces completeness for stations shown to passengers | FR-14 | U | M | ✅ |
| EC-79 | Large text or screen reader on a status change | Text scales without truncation; only meaning changes are announced | AC-E06-05 | M | M | ✅ |
| EC-80 | Screen reader flooded by routine ETA updates | Zero announcements for ETA-only changes; exactly one per meaning change | AC-E06-05 (7) | U | M | ✅ |

## 6. Catering

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-90 | Caterer rejects an order | Pending → Rejected with reason and an alternative where one exists (designed, not built: D-71) | AC-E08-03 (2) | I | M | ⏸ |
| EC-91 | Two passengers order the last item | The caterer accepts one and rejects the other (atomic update) (designed, not built: D-71) | SEC-19 | I (simulated caterer) | M | ⏸ |
| EC-92 | Order not paid | Expires after 15 minutes (designed, not built: D-71) | AC-E08-02 (3) | I | M | ⏸ |
| EC-93 | Order past the cutoff | Rejected with a reason; cutoff fixed to the timetable (designed, not built: D-71) | AC-E08-02 (4) | I | L | ⏸ |
| EC-94 | Scripted fake orders | 5 per device per minute; then 429; unpaid orders never prepared (designed, not built: D-71) | AC-E08-02 (5) | I | M | ⏸ |
| EC-95 | Journey cancelled or terminated before the collection stop | Automatic full refund (designed, not built: D-71) | AC-E08-06 (1) | E2E | H | ⏸ |
| EC-96 | Train delayed but arrives | The caterer receives no event in the MVP (D-72); any refund or change to an order is the caterer's own matter (D-71). The food screen shows the journey's delay | AC-E08-05 (1) | I | M | ⏸ |
| EC-97 | Passenger misses collection | Not Collected; no automatic refund (designed, not built: D-71) | AC-E08-04 (5) | I | M | ⏸ |
| EC-98 | Caterer unreachable when a train is terminated | Outbox retries; railway state unaffected. Designed, not built (D-72) | AC-E08-05 (3) | I | H | ⏸ |
| EC-99 | A train-event message is delivered to the caterer twice (a retry) | The message ID lets the caterer ignore the second delivery; the simulated caterer applies it once. Designed, not built (D-72) | AC-E08-05 (2) | I | H | ⏸ |
| EC-100 | Out-of-order journey messages to the caterer | Version check ignores older ones (order messages are deferred, D-71) | AC-E08-05 (4) | U, I | M | ✅ |
| EC-101 | Passenger has no signal at the counter | Token stored on the device; works offline (designed, not built: D-71) | AC-E08-04 (2) | M | M | ⏸ |
| EC-102 | Token used twice | Second use refused (designed, not built: D-71) | AC-E08-04 (3) | I | M | ⏸ |
| EC-103 | Passenger loses or resets the phone after ordering | Token lost; the counter can resolve it with the order details (MVP) (designed, not built: D-71) | FS-14 | M | L | ⏸ |
| EC-104 | Item sold out after ordering | Replacement or refund choice (Should) | S-03 | I | M | ⏸ |
| EC-105 | Very large delay (e.g. 90 minutes) | Cancellation of orders (Could) | C-04 | M | L | ⏸ |
| EC-106 | A long stop shortened on the day by a delayed train | The departure event closes the collection window; to be validated in the catering slice (designed, not built: D-71) | A-07d | I | M | ⏸ |
| EC-107 | Long-distance train with no stop long enough for food | The menu is listed for the destination only | AC-E08-01 (4) | U | L | ✅ |
| EC-108 | Caterer unreachable long enough to matter | Alert after 15 minutes undelivered or on a growing backlog | AC-E08-05 (7) | I | M | ✅ |
| EC-180 | The caterer's menu feed is silent or the phone is offline | The list stays visible and shows "Menu from [time]" with a note that items and availability may have changed | AC-E08-01 (6) | U, I | L | ✅ |
| EC-181 | Phone is offline when the passenger taps the caterer button | The app says she is offline; no page opens; the list stays | AC-E08-07 (4) | M | L | ✅ |
| EC-182 | The caterer's website is down or its address is wrong | The phone's browser shows its own error; SmartRail cannot tell. The address is a setting, and the demonstration checks the link opens (accepted limitation) | AC-E08-07 (1), (7) | M | M | 🔶 |
| EC-183 | An item is shown as available but is sold out on the caterer's website | Prices and availability are the caterer's; the list says so and the time it was updated (accepted limitation) | AC-E08-01 (6) | M | M | 🔶 |
| EC-184 | A menu item has no Sinhala or Tamil name | Falls back to the English name; a test enforces completeness for items shown to passengers | AC-E08-01 (5), FR-14 | U | M | ✅ |
| EC-185 | The journey is cancelled or terminated before the passenger's food station | The food screen shows the disrupted state, tells her to ask the caterer about any order already placed, and does not show the button; the caterer is not told by SmartRail in the MVP (D-72) | AC-E08-07 (6) | I, M | H | ✅ |
| EC-186 | The link to the caterer's website carries passenger data | It carries at most the station code and train number; no account, device or push-token identifier | AC-E08-07 (2), NFR-21 | I | M | ✅ |

## 7. Station displays

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-110 | Display loses its connection | Within 30 seconds: "Live updates unavailable · showing information from…"; never looks live | AC-E09-03 | I, M | H | ✅ |
| EC-111 | Main board offline | Keeps scheduled trains and platforms, marked not live | AC-E09-01 (4) | I | M | ✅ |
| EC-112 | Reconnect after missing updates | Fresh snapshot, then newer updates only (version check) | AC-E09-03 (3) | I, E2E | H | ✅ |
| EC-113 | Race between connecting and fetching the snapshot | Connect first, buffer updates, apply only those newer than the snapshot | ADR-09 | I | M | ✅ |
| EC-114 | No train due; last train gone | Next service shown; "No more services today · first service tomorrow" | AC-E09-04 | M | L | ✅ |
| EC-115 | Train overdue, suspended, cancelled or terminated | The state itself becomes the largest element | AC-E09-02 | M | M | ✅ |
| EC-116 | Longer Sinhala and Tamil text at display size | Layout tested with all three scripts side by side; room test | NFR-35 | M | M | 🔶 |
| EC-117 | Mistranslated display text | All public text validated by native speakers; placeholders until then | NFR-34 | M | H | ✅ |
| EC-118 | Verification badge beside content that isn't verified | No badge on platform displays | AC-E09-02 (4) | M | L | ✅ |
| EC-119 | Cached snapshot is stale after a version change | Cache invalidated on version change | OPS-10 | I | M | ✅ |
| EC-109 | A passenger leaves the terminal in the middle of a session | After the configured period a countdown is shown; if nobody touches the screen, the language and choices are cleared | FR-85, ACC-06 | M | M | ⏸ |
| EC-133 | The terminal loses its live connection | A banner says live updates are unavailable and scheduled trains are marked "Scheduled, not live" | FR-85, NFR-03 | M | M | ⏸ |

## 8. Security, privacy and operations

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-120 | Anonymous endpoint flood | Per-device rate limits; IP backstop; 429 with Retry-After; cached snapshots | NFR-13 | I | M | ✅ |
| EC-121 | Public hub carrying internal data | Only passenger-safe data; free text and unverified reasons excluded | NFR-12 | I | H | ✅ |
| EC-122 | Operator listening to another train's updates | Operator hub limited to the assigned journey | ADR-09 | I | H | ✅ |
| EC-123 | Simulated clock reachable in production | Not present in production builds | NFR-15 | M | H | ✅ |
| EC-124 | Personal data in logs | IDs only; no names, tokens or device IDs | NFR-24 | M | M | ✅ |
| EC-125 | Push token linked to travel | Deleted after the journey; order references deleted after 30 days | NFR-22 | I | M | ✅ |
| EC-126 | Staff movement data | Visible to controllers and administrators only | NFR-23 | I | M | ✅ |
| EC-127 | Secrets committed to source control | `.env` ignored; no keys in code | NFR-14 | M | M | ✅ |
| EC-128 | Peak-hour deployment | No planned maintenance 06:00–09:00 or 16:00–19:00 | NFR-02 | M | M | ✅ |
| EC-129 | A critical failure at night | Severity tiers; 24/7 on-call; critical failures page at once | NFR-40 | M | M | ✅ |
| EC-130 | Integration backlog grows silently | Undelivered messages visible in logs and monitoring | OPS-02 | I | M | ✅ |
| EC-131 | A restore is never tested | One restore test recorded as evidence | NFR-25 | M | M | ✅ |
| EC-132 | Events altered or deleted through the application | Database refuses UPDATE and DELETE on the events table | AC-E04-02 (5) | I | H | ✅ |

## 9. Reference data and journeys

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-140 | Journey generated twice | One journey per service per date | AC-E02-01 (2) | U | M | ✅ |
| EC-141 | Service doesn't run on the day | No journey generated | AC-E02-01 (3) | U | L | ✅ |
| EC-142 | Express with fewer stops than a slow train | Stops come from the service's own stopping pattern | FR-15 | U | L | ✅ |
| EC-143 | Station on several lines | One station row; per-line order and distance in LINE_STATION | ER diagram | U | L | ✅ |
| EC-144 | Station list from an unreliable source | Verified against the railway's published data before seeding | A-18 | M | M | ✅ |
| EC-145 | Service stops that aren't stations on its line | Application rule with a test | ER diagram | U | M | ✅ |
| EC-146 | Timetable change for a future journey | Cancelling it sends a cancellation to the caterer | INT-06 | I | M | ✅ |

## 10. Ticketing and accounts (Should)

All deferred with the feature: ticketing is a Should (D-60) and is built only if the Musts are done. The behaviour is decided and specified now so the build, if it happens, has no open questions.

| ID | Edge case | Expected behaviour | Specified in | Test | Risk | Handling |
|----|-----------|--------------------|--------------|------|------|----------|
| EC-150 | Two passengers buy the last free seat at the same moment | Exactly one succeeds; the other is told and offered other seats; the database refuses overlapping reservations | NFR-47, AC-E11-04 (3) | I | H | ⏸ |
| EC-151 | Payment succeeds after the hold has expired | Seats confirmed if still free; otherwise automatic refund and a message | AC-E11-04 (5) | I | H | ⏸ |
| EC-152 | Payment declined | Purchase Declined, seats released, retry possible | AC-E11-05 (4) | I | M | ⏸ |
| EC-153 | Passenger abandons the payment page | Hold expires after 10 minutes; no charge | FR-112 | I | M | ⏸ |
| EC-154 | Payment result delivered twice or out of order | Applied once by message ID; older version ignored | NFR-51 | U | H | ⏸ |
| EC-155 | Payment result never arrives | SmartRail asks the provider for the payment status before releasing the hold | AC-E11-04 (4) | I | H | ⏸ |
| EC-156 | Same seat sold for two parts of one journey that do not overlap | Allowed; overlapping ranges refused | AC-E11-03 (2) | U | M | ⏸ |
| EC-157 | Journey cancelled after tickets were sold | Paid tickets before boarding are refunded automatically and the passenger is notified | AC-E11-09 (1) | I | H | ⏸ |
| EC-158 | Journey terminated midway | Tickets whose boarding station was not reached are refunded; others are not refunded automatically (the railway decides compensation, A-29) | AC-E11-09 (4) | I | H | ⏸ |
| EC-159 | Refund cannot be delivered or the provider is down | Outbox retries; ticket stays Cancelled by railway; backlog visible in monitoring | AC-E11-09 (3) | I | H | ⏸ |
| EC-160 | Metro ticket and a cancelled regional train | Ticket is not tied to a train, so it stays valid for the next one; no automatic refund | AC-E11-08 (3) | U | M | ⏸ |
| EC-161 | Metro ticket for two stations that share no line | Not offered | AC-E11-01 (3) | U | M | ⏸ |
| EC-162 | Fare changes between quote and payment | The quoted fare is stored with the purchase and charged | AC-E11-01 (5) | U | M | ⏸ |
| EC-163 | Passenger opens a ticket on a second phone | History appears after sign-in; the ticket code is stored on each phone after it is opened online | AC-E11-07 (4) | M | L | ⏸ |
| EC-164 | Passenger tries to open someone else's ticket | Refused; covered by an authorisation test | AC-E11-07 (3) | I | H | ⏸ |
| EC-165 | Saved card removed or expired | Removed at the provider; an expired card cannot be chosen | AC-E11-06 | U | L | ⏸ |
| EC-166 | Account deleted while a future ticket is paid | Deletion refused with the reason (proposed rule) | AC-E11-11 (2) | U | M | ⏸ |
| EC-167 | One account hoards seats | Limit of 5 holds in 10 minutes; 429 with Retry-After | AC-E11-12 | I | M | ⏸ |
| EC-168 | Card data appears in logs, errors or messages | Never stored; a log scan is part of the evidence | NFR-48 | M | H | ⏸ |
| EC-169 | Payment provider offline at checkout | Clear message; nothing else in the app is affected | AC-E11-05 (6) | I | M | ⏸ |
| EC-170 | Ticket opened with no signal at the station | Code and trip shown from the phone | AC-E11-08 (1) | M | M | ⏸ |
| EC-171 | One of six chosen seats cannot be held | None are held; the passenger chooses again | AC-E11-04 (6) | I | M | ⏸ |
| EC-172 | Terminal or display shows account or ticket information | Never shown | AC-E11-08 (4) | M | M | ⏸ |
| EC-173 | Unreserved capacity of a journey is reached | Passenger offered a seat reservation or another train; no ticket is sold over the limit | AC-E11-13 (4) | I | M | ⏸ |
| EC-174 | Two passengers buy the last unreserved place | Exactly one succeeds (journey row locked during the sale) | AC-E11-13 (5) | I | H | ⏸ |
| EC-175 | Seat shown free in the app but sold at a counter | Cannot happen: counter seats are allocated apart and never offered in the app (A-33) | AC-E11-14 (1) | U | H | ⏸ |
| EC-176 | Passenger bought at a counter and looks in the app | Not shown; the app says nothing about counter tickets | AC-E11-14 (2) | M | L | ⏸ |
| EC-177 | Counter ticket and a cancelled journey | Not refunded by SmartRail; the counter's rules apply and SmartRail cannot see the ticket | AC-E11-14 (3) | M | M | ⏸ |
| EC-178 | Passenger with a no-seat ticket wants a seat later | Not offered in the MVP (a Could: add a seat to an existing ticket) | FR-111 | M | L | ⏸ |

---

## Summary

| Handling | Count |
|----------|-------|
| ✅ Handled | 93 |
| 🔶 Partly handled (known MVP limitations) | 8 |
| ⏸ Deferred (Should, Could, or designed but not built) | 66 |
| ❓ Open | 0 |
| **Total** | **167** |

## High-risk edge cases to automate first

The five end-to-end tests (brief §23) should cover the highest-risk edge cases:

| E2E test | Edge cases covered |
|----------|--------------------|
| #1 Report delay → ETA updates → controller verifies → passenger sees verified delay | EC-17, EC-37, EC-42 |
| #2 Offline departure → sync later → ETA remains correct | EC-01, EC-02, EC-04, EC-24 |
| #3 Train goes silent → exception → controller acts → passenger sees updated information | EC-30, EC-31, EC-38 |
| #4 Delay shown consistently on passenger app, platform display and main board | EC-110, EC-112 |
| #5 Caterer menu feed → food list shows Available and Sold out → feed goes silent, list shows "Menu from [time]" → journey terminated before the food station → disrupted state, no button (changed 12 Oct: D-71 replaced order → ready → collect; D-72 replaced "event reaches the caterer once") | EC-180, EC-183, EC-185 |

## Still to do

- The Gemini stakeholder panel has run for all ten roles. Its findings were checked against the requirements (see the change log in `smartrail-refined-requirements.md`, version 1.1); six edge cases came from it (EC-19, EC-43, EC-62, EC-80, EC-108, EC-132)
- **EC-106** (a long stop shortened by a delayed train) is about collection windows and is deferred with ordering (D-71)
- New cases found during the build are added here and in the register
