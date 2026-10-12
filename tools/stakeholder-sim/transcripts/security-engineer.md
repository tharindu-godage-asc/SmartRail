# Security Engineer — Stakeholder Transcript

- Pain point: False operational data such as fake delays or platforms could send passengers to the wrong place.
- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)
- Role perspective: [Security Engineer persona](../personas/security-engineer.md)

## Response

*   **Data Integrity Gap:** Discovery lacks controls to verify the authenticity of operator inputs (e.g., fake delays). D-36 records actions in `TrainEvents` but does not specify cryptographic signing or source validation to prevent malicious or erroneous data from being treated as truth.
*   **Authorization Scope:** D-28 confirms SmartRail enforces "assigned-journey authorisation," but it is unclear if this prevents an operator from modifying data for journeys outside their assignment to inject false operational data.
*   **Threat Model Status:** The formal threat model is explicitly "planned" and not yet complete. Consequently, specific risks regarding data tampering or insider threats are currently open questions rather than addressed risks.
*   **Audit Limitations:** While D-36 provides a single audit trail, the discovery does not state whether this trail is tamper-evident or if it includes sufficient context to distinguish between accidental errors and intentional falsification of delay/platform data.
*   **Open Questions:** §23 lists open security risks, but the specific mechanism for ensuring the integrity of real-time data feeds against false information is not yet defined or tested.
