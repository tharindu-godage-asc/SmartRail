# Accessibility Specialist — Stakeholder Transcript

- Pain point: State changes are not announced to screen readers, or every routine update is announced and the screen becomes unusable.
- Source of truth: [SmartRail discovery](../context/smartrail-discovery.md)
- Role perspective: [Accessibility Specialist persona](../personas/accessibility-specialist.md)

## Response

**Discovery addresses this pain point:** Yes, partially. D-54 explicitly defines the logic: screen readers announce meaning changes (status, verification, disruption, connectivity) but never routine value changes. This directly targets the "unusable screen" issue by filtering noise.

**Open questions and concerns:**
*   **Definition of "Meaning":** D-54 lists four specific triggers. Does a minor delay (e.g., 2 minutes) count as a "status" change requiring announcement, or is it a routine value update? The threshold is undefined.
*   **Routine Update Suppression:** While D-54 mandates silence for routine changes, it does not specify *how* these updates are visually presented to non-screen-reader users simultaneously. Does the visual update still occur? If so, is there a risk of visual clutter that mimics the "unusable" state for low-vision users?
*   **Verification Gap:** D-53 states accessibility is verified separately in React and Flutter. There is no mention of automated testing for announcement frequency or "chatter" in the Definition of Done. How will we validate that the "never routine" rule is technically enforced in code, not just design?
*   **Platform Displays:** D-54 focuses on screen readers (apps). D-19 and D-50 cover public displays. Are public displays expected to have audio announcements? If not, the "unusable" risk for visually impaired passengers on platforms remains unaddressed in the excerpts.
*   **Discrepancy:** The persona mentions "text scaling," but D-50 only specifies Level 1 as the "largest element." There is no explicit requirement for dynamic text scaling or zoom support in the provided excerpts.
