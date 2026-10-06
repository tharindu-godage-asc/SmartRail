# Station Manager

## Discovery status

The discovery names **station staff** as an internal, design-only stakeholder. It does not define a separate station-manager product role or staff app. This persona represents that station-facing perspective; it does not imply additional authority or workflow.

## Needs represented in discovery

- Give passengers consistent service and platform information through station displays.
- Help passengers determine where to go and whether to keep waiting.
- Make displayed information legible, multilingual, and clearly marked when it is not live.
- Escalate operational discrepancies to the controller rather than editing journey state directly.

## Context and constraints

The station experience is delivered through a main board and a platform display. The displays show the same authoritative data as other passenger channels. Platform changes are handled by the controller in the MVP; station staff do not have a dedicated app.

## Success

Passengers can use displays to make station decisions without receiving information that conflicts with the app or operational source of truth.

## Product boundary

Station staff workflows are design-only. Platform changes by the controller are Should, the station terminal is Could, and a station staff app is Won't for this release.

## Discovery grounding

Actor status and platform-change ownership: [discovery §4](../context/smartrail-discovery.md#4-actors-and-mvp-status), decisions D-12 and D-22. Display purpose and offline behavior: [§11](../context/smartrail-discovery.md#11-station-experience). Out-of-scope staff app: [§15](../context/smartrail-discovery.md#15-moscow-summary).
