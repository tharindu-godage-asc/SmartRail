# Driver

## Discovery status

The train driver is explicitly **not a SmartRail actor**. The driver is distinct from the on-train Train Operator, who reports journey events.

## Role boundary

Train movement and safety remain within railway operating procedures and safety systems. SmartRail informs; it does not control train movement, signalling, speed restrictions, or emergency response.

## Implications for SmartRail

- Do not attribute operator event-reporting responsibilities to the driver.
- Do not introduce a driver account, workflow, or driving instruction as an assumed MVP requirement.
- Keep safety-critical information and emergency coordination outside SmartRail.

## Product boundary

“Driver” is included in the requested persona set to document a role boundary, not to imply that the driver is a SmartRail user. Any future driver-facing capability would require explicit scope and safety review.

## Discovery grounding

The operator/driver distinction and driver exclusion are explicit in [discovery §2, A-03 and A-05](../context/smartrail-discovery.md#2-context-and-assumptions) and [§15 Won't](../context/smartrail-discovery.md#15-moscow-summary).
