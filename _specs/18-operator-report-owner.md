# Feature Spec: Each operator sees only his own reports on a shared phone

branch: claude/feature/operator-report-owner
spec: _specs/18-operator-report-owner.md
owner: tharindu
date: 2026-10-20
status: approved (owner asked for it on 20 Oct 2026; built the same day)

---

## 1) Problem
Found by hand on the emulator: John Doe signed in to the operator app and the Reports tab listed Nimal's reports. The My reports list is read from the phone's own database (spec 12 Q3), and that database did not record who made a report. Sign-out clears the cached journey but keeps the reports on purpose, so that a report waiting to be sent is not lost (AC-E03-05 6). On a phone shared between operators this shows one operator's work to the next and could also send it under the next operator's token. The server is not affected: the Previous journeys reads already return only the caller's own journeys.

## 2) Scope
Operator app only (`mobile/operator_app`). No backend change, no new endpoint, no new role.

## 3) Requirements and criteria used
- FR-03 and FR-31 (reports belong to the operator who made them and are kept on the phone); spec 12 rule 7 (My reports is phone data); AC-E03-05 6 (a waiting report survives an app update); NFR-10 and NFR-12 (privacy of operational data). If one of these is worded differently in `docs/`, the spec text here is the reading used.

## 4) Rules
1. Every report saved on the phone records the **Keycloak subject** (`sub` of the access token) of the operator who made it.
2. Every read of the phone queue is limited to the signed-in operator: the pending list the sender uses, the pending count shown in the sign-out box and on the sign-in screen, My reports, the reports of one journey, and the latest report time.
3. The duplicate check for "the same stop twice" applies per operator: John's report for a stop is never answered with Nimal's.
4. Updating a report (attempts, state) never changes its owner.
5. With no one signed in nothing is visible. While the session has expired the owner is still known, so the waiting count still shows.
6. When another operator signs in the queue is rebuilt for him; nothing from the earlier operator is shown or sent.
7. **Reports saved before this change have no owner.** They are kept in the database (nothing is deleted) but belong to no one, so no operator sees or sends them. The old waiting ones are therefore not sent after the update. This is a deliberate choice: they cannot be attributed safely.
8. Delivered reports older than 7 days are still removed for everyone (spec 12 rule 7); the clean-up does not depend on the owner.

## 5) Data (phone database, drift)
- Schema version 2 to 3: `reports.owner` text, default empty. The earlier version 1 to 2 rebuild now includes the column, because the rebuild copies the table as it is defined today.
- No server data changes.

## 6) Architecture
- `AuthState` gains `userId` (from `userIdFromToken`); `markSessionExpired` keeps it.
- `DriftReportQueue.forOwner(db, owner)` scopes every query; the plain constructor stays unscoped for the data tests.
- `reportQueueProvider` watches the signed-in user id and builds the scoped queue, so a different sign-in rebuilds everything that reads it.

## 7) Acceptance Criteria
- [x] An operator sees only the reports he made in My reports, the journey list, the sign-out count and the sender's pending list.
- [x] Two operators reporting the same stop each keep their own report.
- [x] An update keeps the owner; with no one signed in nothing is visible.
- [x] Reports saved by the earlier app version stay on the phone and are shown to no operator.
- [x] The version 2 to 3 upgrade runs, and the version 1 to 2 upgrade still runs.
- [x] The existing operator app tests still pass.
- [ ] Tried on the emulator with two operators (Nimal, then John): John's Reports tab is empty after signing in. *(Hand check.)*

## 8) Test Plan
Unit and data tests in `test/reports/report_owner_test.dart` (every read, duplicate per operator, update keeps owner, signed out, legacy rows, token subject) and the existing migration tests. Hand check as in section 7.

## 9) Not done
- Reports of the earlier app version are not migrated to an owner (rule 7).
- The Keycloak operator directory (spec 17 A3) is unrelated and still not built.
