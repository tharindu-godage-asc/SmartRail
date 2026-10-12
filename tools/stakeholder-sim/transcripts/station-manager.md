# Station Manager — Stakeholder Transcript

- Pain point: A platform change must reach passengers quickly, but station staff cannot make it in the system because the controller owns platform changes in the MVP.
- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)
- Role perspective: [Station Manager persona](../personas/station-manager.md)

## Response

*   **Discovery addresses the pain point:** Yes. D-12 confirms platform changes are controller-owned in the MVP (Should), explicitly excluding station staff from making these changes.
*   **Uncertainty regarding speed:** The discovery does not define a maximum latency for a controller-initiated platform change to appear on station displays. Is there a defined SLA for this propagation?
*   **Escalation workflow gap:** While station staff must escalate discrepancies, the discovery lacks details on how staff notify the controller of urgent platform issues. Is there a defined communication channel or alert mechanism?
*   **Display status clarity:** D-19 and D-50 define display content, but it is unclear how passengers are informed if a display is offline or showing stale data during a rapid platform change.
*   **Open concern:** The "design-only" status of station staff (D-12, D-22) creates a dependency on controller speed. If the controller is delayed, passengers receive no local update. How is this risk mitigated in the MVP?
