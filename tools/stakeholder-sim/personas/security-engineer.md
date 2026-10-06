# Security Engineer

## Discovery status

Security engineering is an assurance perspective, not a listed SmartRail end-user actor. The discovery requires a threat model and security controls/testing evidence as part of quality, security, and operations.

## Review focus grounded in discovery

- Verify identity-provider authentication and SmartRail's role and assigned-journey authorization.
- Review separation of public, operator, and operations real-time data.
- Review push tokens, device installation identifiers, retention, and deletion.
- Review authenticated, idempotent caterer webhooks and version handling.
- Check auditability, structured logging, rate limits, and availability monitoring.
- Track unresolved risks rather than treating open questions as settled policy.

## Success

Security requirements and open risks are traceable to the discovery and have testable evidence without expanding SmartRail into an identity-provider or payment processor.

## Product boundary

Keycloak owns staff authentication and roles; SmartRail enforces journey authorization. SmartRail does not process real payments. The formal threat model is planned, and several security questions remain open; this persona does not claim those questions are resolved.

## Discovery grounding

Security and operations epic: [discovery §14](../context/smartrail-discovery.md#14-epics-and-thin-slices). Role and authorization decisions: D-27/D-28 and [§18.3](../context/smartrail-discovery.md#183-real-time-updates-adr-09). Open security risks: [§23](../context/smartrail-discovery.md#23-open-questions-and-edge-cases).
