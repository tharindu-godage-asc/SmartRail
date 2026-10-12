# Passenger — Stakeholder Transcript

- Pain point: Cannot tell whether the train information shown is current; RDMNS showed updates 30+ minutes old and passengers still act on it.
- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)
- Role perspective: [Passenger persona](../personas/passenger.md)

## Response

*   **Discovery addresses pain point:** Yes, partially. D-20 mandates a shield badge for verification, and D-39 explicitly distinguishes "train silence" (ETA replaced) from "viewer silence" (last known ETA kept and marked).
*   **Uncertainty on "Current":** The excerpts do not define the maximum age threshold for data to be considered "current." It is unclear if the shield badge changes state based on timestamp age or only source verification.
*   **Stale Data Handling:** While D-39 marks viewer silence, it is open whether the system actively flags data that is technically "live" but significantly delayed (e.g., the 30+ minute RDMNS scenario) as untrustworthy.
*   **Accessibility Gap:** D-54 states screen readers announce verification changes, but it is unclear if the "stale" status is announced with sufficient urgency for a passenger relying on audio.
*   **Open Question:** Does the "shield badge" (D-20) provide a timestamp, or only a binary verified/unverified state? Without a timestamp, a passenger cannot independently judge the 30-minute delay.
