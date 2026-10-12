# Catering Provider — Stakeholder Transcript

- Pain point: Food is wasted when trains are late or cancelled because preparation was based on the schedule rather than the live train position.
- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)
- Role perspective: [Catering Provider persona](../personas/catering-provider.md)

## Response

*   **Discovery addresses the pain point partially:** D-16 fixes the ordering cutoff to the timetable, but does not specify if preparation triggers shift to live train positions. The discovery confirms the caterer receives journey updates (delays/cancellations) but does not mandate using them to adjust prep.
*   **Ownership boundary:** D-13 confirms the caterer owns fulfilment and payment. SmartRail only provides events. The decision to waste or save food based on live data rests entirely with the caterer’s internal logic, which is outside SmartRail’s scope.
*   **Refund/Compensation gap:** D-14 and D-32 define automatic refunds for cancellations/terminations. However, there is no stated rule for compensation if food is prepared but not collected due to a delay (not cancellation) where the train eventually arrives late.
*   **Message versioning:** The caterer must ignore stale updates. If a train is delayed then cancelled, the caterer must handle the sequence correctly. Uncertainty: Does the discovery specify the latency tolerance for "live" position updates to be actionable for prep?
*   **Open question:** Does the railway–caterer service agreement (referenced in D-14) include clauses for waste compensation due to schedule slippage, or only for hard cancellations? This is not detailed in the provided excerpts.
