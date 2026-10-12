# QA Engineer — Stakeholder Transcript

- Pain point: Offline and out-of-order reporting scenarios are hard to reproduce and test reliably.
- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)
- Role perspective: [QA Engineer persona](../personas/qa-engineer.md)

## Response

*   **Controllable Clock Limitation**: ADR-06 mandates a controllable clock for backend authority, but discovery does not specify how to simulate network latency or packet loss for offline client scenarios. *Question: Is there a defined mechanism to inject network failures in the test environment?*
*   **Out-of-Order Reproducibility**: D-09 states occurrence time governs order, and D-35 uses client IDs for deduplication. However, no excerpt defines the expected UI behavior or state reconciliation when events arrive out of sequence after an offline period. *Open: How is "reliable reproduction" of race conditions evidenced?*
*   **Offline Reporting Scope**: The persona requires testing offline reporting and retries, but the provided excerpts contain no specific rules for offline queue management, retry limits, or conflict resolution strategies. *Question: Are offline retry behaviors defined in omitted sections, or are they currently undefined?*
*   **Silence vs. Offline Distinction**: D-39 distinguishes train silence from viewer silence. It is unclear if "offline" is treated as a specific silence type or a connectivity state that triggers the "connectivity" announcements mentioned in D-54. *Uncertainty: Does offline status trigger a distinct UI state separate from standard silence?*
*   **Evidence Gap**: While D-53/D-54 cover accessibility verification, there is no excerpt detailing the specific quality evidence required for offline recovery paths (e.g., data integrity checks post-reconnect). *Concern: The definition of "repeatable evidence" for failed/stale paths lacks concrete metrics in the provided text.*
