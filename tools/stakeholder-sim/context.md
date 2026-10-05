# SmartRail: context for stakeholder agents

You are reviewing the design of **SmartRail**, a fictional national railway's digital platform, modelled on a Sri Lankan-style network. Challenge the design below by citing decision IDs (D-xx, ADR-xx, P-xx, A-xx). Do not invent facts about the railway. Raise concerns and questions, not solutions.

## Problem

The railway relies on fragmented systems and lacks a single authoritative source of real-time operational data. Passengers, external providers, and railway staff make decisions on stale, incomplete, or unavailable information. The result: wasted time, inefficient operations, and food waste.

## The system (MVP)

- **Operator app** (Flutter): an on-train **operator** (guard-like; *not* the driver, A-03) reports departures, arrivals at designated stations only (D-08), and delays. Works offline and syncs later.
- **Operations web app** (React): the **controller** verifies delay reasons, handles an exceptions queue, and suspends, cancels, or terminates services.
- **Passenger app** (Flutter, anonymous, no accounts, D-26): live status, ETAs, "Notify me" push (Android only).
- **Station displays** (React): a main board ("Where do I need to go?") and a platform display ("Should I keep waiting?"), shown in Sinhala, Tamil, and English side by side.
- **Catering**: a separate external system. SmartRail forwards orders; the caterer owns menu, payment, refunds, and fulfilment (D-13, ADR-13).
- **Scope:** one line (Main Line), Veyangoda → Gampaha → Ragama → Maradana → Colombo Fort, plus one long-distance train Kandy → Colombo Fort.

## Key rules

**Data and time**
- Operators report facts; the **backend** derives delay, ETA, and journey start/end (P-05). ETA = scheduled time + current delay; operator estimates are *total* delay (D-05, D-07).
- Journey order is judged by **occurrence time**, not received time (D-09). Duplicate reports are detected by a client event ID (D-35).
- A train is "on time" within ±2 minutes. Trains never depart early; an early departure report raises an exception (D-48, D-49).
- The backend is the authority on current time; apps use a server offset (ADR-06).

**Trust and silence**
- Passengers never see silent data (P-02). Two kinds of silence: the **train** is quiet (ETA replaced by "Waiting for update") vs the **viewer** is offline (last known ETA kept, marked). (D-39)
- Overdue is judged against the next expected event, not time since the last one (D-06). Overdue detection pauses while an override is active (D-43).
- Delay timing is published immediately; the **reason** is shown only after the controller verifies it (staged publishing, D-10). Free-text reasons never reach passengers.
- A skipped stop is inferred and is a data-quality matter, not an exception (P-07). Exceptions are only things needing controller action, ordered by severity then age (D-44).

**Disruptions**
- Overrides (Suspend, Cancel, Terminate) stay active until the controller clears them (ADR-05).
- Safety-critical data and emergency response are outside SmartRail; it informs only (A-05).
- If the operator's app can't send, they phone the line desk and the controller enters the event (A-04). Operators can flag their last report; full correction is deferred (D-41).

**Real-time delivery**
- REST snapshot + SignalR push with journey version numbers; connection loss is visible within 30 seconds (ADR-09). Hubs: public (passenger-safe data only), operator (assigned journey only), operations (controllers).
- Operator access token 15 minutes, refresh token 8 hours; queued events are never lost offline.

**Catering**
- Passengers pay the caterer when ordering. Order lifecycle: Pending → Awaiting Payment → Confirmed → Ready → Collected, with Rejected, Expired, Not Collected, and Refunded.
- If the railway cancels or terminates before the collection station, the caterer refunds automatically under a service agreement (D-14).
- Collection only at the passenger's destination or approved long stops (5+ minutes), at a fixed counter with a one-time token (D-15). Ordering cutoff is fixed to the timetable.
- Webhooks both ways with a transactional outbox, idempotent message IDs, and journey and order versions (ADR-12).

**Operations**
- 99.5% monthly availability; no planned maintenance 06:00–09:00 or 16:00–19:00; SmartRail monitors itself; 24/7 on-call support.

## Personas already consulted

Kasuni (daily commuter, Sinhala-speaking, colour-vision deficiency), Nimal (operator), Jayasinghe (controller). Already interviewed: security engineer, railway controller, catering provider, accessibility specialist, product owner.

## Deliberately out of scope (Won't)

Seat reservations and ticketing · GPS tracking (designed only) · a station staff app · speed restrictions and signalling · audio announcements (text only) · caterer handler app · in-coach delivery · machine-learning ETAs · real payments (simulated) · crowd-sourced passenger reports · custom admin UI (Keycloak console used) · additional lines · connections between trains · route changes (deferred).

## Known open or deferred items

Platform changes (Should; the controller sets them), section closures for landslides and floods (Should), shift handover note (Should), station terminal (Could), a formal threat model (planned), contrast and display legibility checks (planned).

## Not yet explored (good topics for new roles)

How a first-time or occasional traveller uses the system; real station-staff workflows; customer-service enquiries when information is wrong; testability and edge cases across the design.
