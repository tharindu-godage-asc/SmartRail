# Passenger

## Discovery profile

The primary passenger persona is a 29-year-old accounts executive who commutes from Gampaha to Colombo Fort. The discovery describes Sinhala as her language, a mid-range Android phone, occasional connectivity loss, and mild colour-vision deficiency. Occasional and long-distance travellers and passengers without smartphones are also considered.

## Goals

- Decide whether to wait, change plans, or continue travelling using trustworthy train information.
- Find services between stations and understand status, ETA, and platform details.
- Tell whether information is current, verified, stale, or unavailable.
- Follow a journey for significant changes, without needing a passenger account.
- Where relevant, browse catering, order, and understand collection status.

## Needs and constraints

- Never present missing or stale train data as current. Show the last verified update and distinguish train silence from the viewer's offline state.
- Keep the passenger app and station displays consistent and make status clear without colour alone.
- Support Sinhala, Tamil, and English; account for text scaling and screen readers.
- Show an Android push notification for significant journey changes.
- For catering, make the eligible collection point and one-time token clear.

## Success

The passenger can make a confident travel decision and can recognize when live information is unavailable or out of date.

## Product boundary

The passenger app is anonymous/device-based for the MVP. Accounts are Should; ticketing, seat reservations, crowd-sourced reports, and real payment processing are out of scope. The caterer owns payments, refunds, menu, and fulfilment.

## Discovery grounding

Primary and secondary actors, profile, and rationale: [discovery §4](../context/smartrail-discovery.md#4-actors-and-mvp-status). Goals: [§13](../context/smartrail-discovery.md#13-pov-statements-and-hmw-questions). App and display scope: [§11–12](../context/smartrail-discovery.md#11-station-experience). Anonymous use and push: [§14](../context/smartrail-discovery.md#14-epics-and-thin-slices), decisions D-26 and D-31.
