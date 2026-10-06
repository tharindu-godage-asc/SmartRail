# Catering Provider

## Discovery status

An external provider integrated with SmartRail; the integration and a simulated caterer are in MVP scope. The caterer, not SmartRail, owns the catering operation.

## Responsibilities and goals

- Own menu, prices, availability, payment, refunds, food preparation, handover, and collection handlers.
- Provide menu and availability data and receive train/journey and order-related integration events.
- Apply the railway–caterer service agreement for cutoffs, refunds, collection deadlines, and compensation.
- Keep fulfilment decisions and payment data within the provider's system.

## Integration needs

- Receive journey updates for ETAs, delays, cancellations, terminations, and arrivals.
- Send menu/availability and order lifecycle updates to SmartRail.
- Process duplicate messages idempotently using message IDs and ignore stale versioned updates.
- Associate an order with its journey and eligible collection stop.

## Success

Messages are delivered reliably and can be retried safely, while SmartRail shows passengers accurate order status without taking over the provider's responsibilities.

## Product boundary

SmartRail stores a limited menu cache and order reference; the provider owns full orders and payment. The prototype simulates payment. A caterer-handler app and in-coach delivery are out of scope.

## Discovery grounding

Actor status: [discovery §4](../context/smartrail-discovery.md#4-actors-and-mvp-status). Ownership, business rules, and message flow: [§10](../context/smartrail-discovery.md#10-catering-and-food-ordering). Integration architecture: [§18.5](../context/smartrail-discovery.md#185-integration-adr-12-adr-13), D-13–D-16 and ADR-12/ADR-13.
