# Staffing V3 approved implementation

Owner authorization: C:/Users/Hax/.codex/attachments/46f49ab3-34c9-49a6-9ccd-b56dac4c5a47/Pasted text.txt. Single-agent. Target HotelOpsCompactDB only; isolated restore of this target allowed for restore/migration/application verification. No Project1/Rebuild source, schema/grant definitions, extra employees, real weekly scheduling, email, shared passwords, automatic activation, Git mutation or deployment.

Decisions: normalize 15 IDs; insert39 INACTIVE/no password, employment ACTIVE as personnel records (login still denied); apply roster role assignments and78 memberships after checks; revoke3 inactive source assignments preserving history; Owner438 keeps global ALL. Preserve existing sessions/security stamps/password hashes. Retain425/433 department authorities unless workflow safety fully established, report exception. No provisioning credentials issued.

- [x] Preflight fresh identity/concurrency/role/grant/schema checks; backup+VERIFYONLY+isolated restore and administrator credential verification without exposing credentials.
- [x] Create scoped migration and runner with transaction, locks, batch audit, before/after, protected-data invariants and rollback/recovery path; no schema changes.
- [x] Exercise migration on restored database, failure rollback, duplicate-run guard and Admin recovery; validate integrity and scoped application behavior.
- [x] Back up primary immediately before approved mutation; apply migration once using fresh concurrency snapshot; verify primary Admin login/permissions and Owner scope.
- [x] Backend/frontend build and automated tests; test role/Workday candidates/scopes without creating production schedules; report inactive account boundary and coverage/staffing gaps.
- [x] Inline security/data review, reconcile evidence and deliver final report with exact counts and unresolved limitations.

Testing constraint: Existing scripted workflows seed extra users and assign real work. Run write tests only on isolated restored target with explicit test dates and no external delivery; production checks limited to approved login sessions and reads after migration. No existing user sessions revoked. Backups before any DB data changes, including test fixtures.

Completed 2026-11-22: primary applied09:44:19Z, audit248, batch B5D5DF03-3E1C-40F4-B60A-6AE335231630. 54 users/15ACTIVE/39INACTIVE,57 historical role assignments/3revoked,110memberships,40tables,67permissions/178grants unchanged. Primary Workday assignments remain0. Backend build0warnings/errors,26testsPASS+1workerSKIP; frontend buildPASS+4testsPASS; fixtureAPI31PASS; primaryAPI26PASS; UI6PASS. Recovery and forced-rollback rehearsal passed on restored fixture. Initial3fixture failures came from existing timers/room eligibility and passed after preparing test prerequisites through APIs. No application source edits needed. Retained425/433authority as approved exception. Report: plans/reports/test-261122-1630-staffing-v3-implementation.md. Future activation/staffing/schedule decisions are outside this completed authorization, no pending approval question.
