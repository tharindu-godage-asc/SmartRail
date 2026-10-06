# Railway Controller

## Discovery profile

The controller is the senior operational role responsible for identifying, verifying, and communicating significant issues. The discovery profile places the controller at the Main Line desk, using a desktop with reliable wired connectivity, multiple monitors, and dense data; Sinhala and English are listed.

## Goals

- See the network state and identify issues requiring intervention.
- Verify operational reports and publish passenger-safe information quickly.
- Enter events on an operator's behalf when phone fallback is used.
- Apply and clear Suspend, Cancel, or Terminate overrides.
- Maintain an auditable, trusted operational view during disruptions.

## Needs and constraints

- Prioritize actionable exceptions by severity and age.
- Publish delay timing before its reason; expose the reason only after verification.
- Keep operational actions in the event audit trail.
- Publish disruption messages using localized templates and parameters, not unverified free text.
- Make override state and its active/cleared lifecycle explicit.

## Success

The controller can identify and resolve actionable issues while keeping passenger-facing information consistent with the authoritative journey state.

## Product boundary

Customer-information responsibilities are merged into the controller role for the MVP (D-11). Platform changes are controller-owned but Should. Section closure and historical reporting are deferred.

## Discovery grounding

Role profile: [discovery §4](../context/smartrail-discovery.md#4-actors-and-mvp-status). Workflow and priority: [§19.4](../context/smartrail-discovery.md#194-jayasinghe--operations-view) and [§21](../context/smartrail-discovery.md#21-user-stories-index). Publishing, overrides, and audit decisions: D-10, D-11, D-12, D-36, D-42, ADR-05.
