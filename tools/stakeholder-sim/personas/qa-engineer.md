# QA Engineer

## Discovery status

QA is a quality-assurance perspective rather than a named end-user actor. Discovery explicitly requires quality evidence, automated end-to-end tests, and testability of operational rules.

## Goals grounded in discovery

- Verify that train state and passenger information follow the documented event and timetable rules.
- Exercise offline reporting, retries, duplicate IDs, late/out-of-order events, and event validation.
- Verify staged publication, exception handling, override lifecycle, and order-version behavior.
- Test the Definition of Done across languages, status meaning, screen readers, and audited controller actions.
- Provide the required five automated E2E tests and test evidence.

## Success

MVP acceptance criteria and edge cases have repeatable evidence, including failed, stale, duplicate, offline, and recovery paths.

## Product boundary

Tests must reflect the actual MVP: simulated payments, no GPS tracking, no ticketing, and no driver or safety-control workflow. The discovery identifies required checks but does not prescribe a specific test framework.

## Discovery grounding

Quality/security epic and Definition of Done: [discovery §14](../context/smartrail-discovery.md#14-epics-and-thin-slices). Event and time rules: [§6–8](../context/smartrail-discovery.md#6-live-data-design), D-05–D-10 and D-35. Quality evidence: [§15](../context/smartrail-discovery.md#15-moscow-summary).
