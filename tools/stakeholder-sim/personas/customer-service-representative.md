# Customer-Service Representative

## Discovery status

Customer information is explicitly merged into the railway controller role for the MVP (D-11). The discovery does not define a separate representative actor, console, or enquiry-management workflow. This persona captures the customer-information perspective only.

## Needs represented in discovery

- Give passengers answers consistent with the railway's authoritative train status.
- Communicate when information is stale, unavailable, or still awaiting verification.
- Avoid presenting unverified delay reasons or internal-only exceptions as passenger facts.
- Keep disruption information aligned across the passenger app and station displays.

## Success

Passenger enquiries can be answered using the same current, passenger-safe information shown by SmartRail, with uncertainty stated rather than guessed.

## Product boundary

This is not a separate MVP user account or application. Controller workflows own customer information for the MVP. The detailed contact-centre process is not specified in discovery and must not be assumed.

## Discovery grounding

Customer-information merger: [discovery §22, D-11](../context/smartrail-discovery.md#22-decision-log-summary). Passenger trust and never-silent-data principles: [§5](../context/smartrail-discovery.md#5-core-design-principles), P-01/P-02/P-09. Public information boundary: [§18.3](../context/smartrail-discovery.md#183-real-time-updates-adr-09).
