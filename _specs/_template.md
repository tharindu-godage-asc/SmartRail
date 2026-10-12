# Feature Spec: <Feature title>

branch: claude/feature/<feature-slug>
spec: _specs/<feature-slug>.md
owner: tharindu
date: <YYYY-MM-DD>
status: draft | ready | in progress | done

---

## 0) Summary
**One-liner:** <What are we building?>
**Why now:** <Which Must requirement or demo flow needs it?>
**Who is this for:** <Passenger / Operator / Controller / Caterer / System Administrator>
**Success looks like:** <measurable, for example "report appears on the passenger screen within 30 s">

## 1) Traceability
- Requirements: FR-__, NFR-__
- Acceptance criteria: AC-__
- Edge cases: EC-__
- Decisions and assumptions: D-__, A-__
- Design: wireframe / hi-fi board names

## 2) Goals and Non-Goals
### Goals
- ...
### Non-Goals (include anything deferred by D-71 or D-72 if it is near this feature)
- ...

## 3) User Workflow
### Primary workflow
1. ...
### Secondary workflows (optional)
- ...

## 4) API Surface
For each endpoint:
- Method and route:
- Who may call it (role, and assignment checks):
- Request (fields, types, validation):
- Response (schema, status codes):
- Errors (ProblemDetails type, when it occurs):
- Example call and response:

## 5) Data Contract
- Tables and columns this feature reads or writes (name, type, meaning)
- New migration needed? (yes/no, summary)
- Uniqueness, indexes and constraints
- Seed data required (reference data vs demo data)

## 6) Business Rules
- Numbered rules, each with the requirement ID it comes from
- Behaviour when data is missing, late or out of order

## 7) Architecture (follow CLAUDE.md and ADR-15)
- Domain: entities, value objects, rules
- Application: command or query, handler, validator, interfaces
- Infrastructure: persistence, external clients
- Api: endpoint mapping
- Telemetry: metric names, trace spans, log fields
- Clients (web, Flutter) that depend on this

## 8) Edge Cases
- (copy the relevant EC-__ items and say how each is handled)

## 9) Acceptance Criteria
- [ ] ... (testable, copied or derived from AC-__)

## 10) Test Plan
### 10.1 Seed and fixture data
Which seeded journeys, stations and users the tests use, and the expected results.
### 10.2 Unit tests
Rules and validators, with cases.
### 10.3 Integration tests
Happy path, unauthorised, invalid input, missing data. Run against Postgres in a container.

## 11) References
- ADRs and documents followed:
- Existing code reused:

## 12) Open Questions
- Q:
- Decision owner / date:
