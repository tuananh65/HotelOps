# Task 02B — Context alignment and legacy department cleanup

21/10/2026 — **BLOCKED for persistent cleanup; diagnostic dry run and integrity PASS**.

## Approved context

Only HK/MT/WH/FO may remain active. FB/LAU/SEC must be retired by soft deactivation, retaining historical references. This model is approved, not pending reconsideration. The unresolved part is disposition of live dependent records: the current task prohibits inventing reassignment/closure rules or bulk disabling accounts.

RBAC remains 12 roles / 10 active / 62 permissions / 140 grants, scopes ALL/BRANCH/OWN. Previously approved OM/DH/Storekeeper identities remain fixed.

## Sources reviewed and gap

AGENTS.md; Release README and scripts 01–04; previous trial README, orchestration, SQL copies, fingerprints and verification; Department entity and DbContext; authentication/current-user implementation; SRS and Change Notes; current repository SQL files and database history.

Release 02 explicitly leaves noncanonical departments for separate review. Script 01 creates schema, 03 seeds RBAC, 04 reports active noncanonical departments. No executable retirement migration for FB/LAU/SEC was found in current repository SQL files. Thus Task 02 upgraded catalog/personnel but did not retire departments. This does not claim to audit every external historical script.

UC-026 in the historical SRS (around lines 3959–3981) checks active users/warehouses/open work and stops when dependencies exist. The current task supersedes the department model, but does not specify how to resolve live records or explicitly exempt these dependencies.

## Actual dependencies

Server: DESKTOP-JI8U50T\SQLEXPRESS. Trial: HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282.

Legacy department IDs: Branch 1 LAU=3, SEC=5, FB=6; Branch 2 SEC=10.

| Group | Actual records |
| --- | --- |
| Active users | 11→LAU Branch 1; 17→SEC Branch 1; 18→FB Branch 1; 20→SEC Branch 2. Employment/account ACTIVE, not deleted. |
| Role/branch assignments | User 11 has active assignment to inactive STAFF; DH assignments for 17/18/20 already inactive. Branch access remains unrevoked: 11 STANDARD, 17/18/20 MANAGER in their own branch. Branch access alone is not effective RBAC permission. |
| Active warehouses | 4 WH-FB Branch 1; 9 WH-LAU Branch 1; 8 WH-SEC Branch 2. |
| Active request types | 4 LAUNDRY, 5 SECURITY, 6 FB_ORDER, 9 EVENT_SETUP. |
| Active work categories | 2 LAUNDRY, 10 SECURITY_PATROL, 11 FB_SERVICE. |
| Open request | Internal request 13, FB, DRAFT. |
| Open/planned shifts | LAU Branch 1: shift 4 IN_PROGRESS; shift 5 PLANNED. Both dated 16/10/2026. Old dates do not close stored statuses. Shift 4 has CONFIRMED assignments for users 4 and 11. |

Historical references to preserve: requests 4/8 COMPLETED receiving LAU; request 5 CLOSED with SEC requester; request 14 CANCELLED receiving FB. Operational task 14 COMPLETED (100%) in LAU; user 11 task assignment COMPLETED.

No direct legacy rows were found in maintenance_works, material_requests, preventive_maintenance_schedules, assets, or either asset_handovers department FK. All 14 incoming department FKs were inspected and enabled/trusted. No department trigger or CHECK was found. FK validity alone does not establish business validity for active references.

## Executed diagnostic

`../Run-DepartmentCleanup.ps1 -Stage Inspect` generated dependency-before.txt.

`-Stage Apply` is currently a guarded **rollback-only diagnostic**, not completed cleanup:

1. Confirmed exact server/database, four legacy records and eight canonical active records.
2. Reported live dependency groups.
3. Updated exactly four is_active flags and observed only HK/MT/WH/FO active inside the transaction.
4. Rolled back; all 72 table counts and SHA256 fingerprints unchanged.
5. Tested the commit gate. SQL 52005 rejected unresolved dependencies before UPDATE; transaction rolled back.
6. Rechecked exact RBAC/personnel, schema and integrity.

No persistent data change occurred. Existing Task 02 backup remains outside Git, but is not represented as a current upgraded-trial restore point. No new backup was needed for this rollback-only diagnostic. Before a later approved COMMIT, create and verify a current trial COPY_ONLY/CHECKSUM backup.

`department-cleanup.sql` is a reviewable trial-only candidate, defaults to rollback, and does not target HotelOpsDB. Its commit gate is conservative for known dependencies, not a complete production preflight/concurrency policy. Do not bypass it. Later execution needs full dependency reinspection, a current recovery point and audit-policy review.

## Verification

| Check | Result |
| --- | --- |
| Diagnostic update and rollback | PASS: four flags; all 72 table fingerprints restored |
| Commit gate | PASS: SQL 52005 rejected commit |
| Department final acceptance | BLOCKED: four legacy records still Active |
| Exact roles/permissions metadata and grants/scopes | PASS, bidirectional comparison against Release |
| RBAC | 12 roles, 10 active / 2 inactive, 62 permissions, 140 grants, no DEPARTMENT grant |
| Approved OM/DH/Storekeeper | PASS, unchanged |
| CHECKDB / CHECKCONSTRAINTS | PASS, no findings |
| FK/CHECK/unique indexes | Enabled/trusted as applicable; no disabled unique index |
| Users, sessions, business data | Entire table fingerprints unchanged |
| Main database | All 72 table fingerprints unchanged against immediate and Task 02 baselines |
| Main catalog | Still 11 roles / 47 permissions / 182 grants; not upgraded |
| Protected files/configuration | SHA256 unchanged |

The requested absence of active user/department/assignment inconsistencies cannot be certified by deactivation alone. Backend prevention of new selections is also unverified; no business endpoints were changed. All four legacy records remain active after rollback; no acceptance claim is made.

## Required decisions and future Release integration

Define disposition for four active users/branch access, three warehouses and inventory workflows, active request types/categories, draft request 13, shifts 4/5 and confirmed assignments. Exact lifecycle/audit rules must be chosen by the developer. Historical completed/closed/cancelled records need no reassignment merely because departments become inactive.

After resolution on trial: current backup, preflight/dry run, guarded COMMIT and verification. Add a reviewed separate department-retirement migration after personnel/catalog normalization and before final Release verification. Keep script 04 read-only; review future seed to exclude FB/LAU/SEC operational defaults. No standard Release edit or main upgrade was performed.

## Artifacts and Git

New: ../Run-DepartmentCleanup.ps1 and Task02B SQL, dependency/dry-run reports, source/trial before/after fingerprints, protected-file hashes, verification report and result.json. No credentials or connection strings stored.

Task 01 code and standard Release unchanged. Task 02 read-only verifier reused; its evidence JSON regenerated with the same verified state. No C# build/test because no C# edits. No commit/push/merge/PR, database drop, source upgrade or connection-string change.
