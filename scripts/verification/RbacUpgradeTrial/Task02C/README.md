# Task02C — Legacy dependency resolution (21/10/2026)

**Verdict: BLOCKED, with an independently safe subset committed.** HotelOpsDB is unchanged. Four legacy departments remain Active because the unchanged SQL 52005 gate still rejects retirement.

## Context and preflight

Server: `DESKTOP-JI8U50T\SQLEXPRESS`.
Trial: `HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282`.

Reviewed AGENTS.md, Git branch/status, Task02/02B scripts and evidence, Release README/scripts, UC-006/026/075/123/138/139, GBR-085/091/141/142/154, state transition tables, related entities/mappings and FE-01 SessionRevocationService/SqlSessionPolicy/AuthenticationAudit. Actual SELECT preflight was rerun; previous counts were not assumed. Only authentication services currently implement backend workflows; no request/shift service was found to supply missing transitions.

The task approves deactivating obsolete sample accounts/catalogs and retiring departments, subject to valid workflows and preserved history. It does not approve arbitrary stock destinations, fabricated attendance or new transitions. Department model remains HK/MT/WH/FO; RBAC model/approved personnel unchanged.

## Recovery and transaction safety

A new current **trial** COPY_ONLY/CHECKSUM backup was created and passed RESTORE VERIFYONLY. Location is recorded in recovery-point.json; it is outside Git. No source backup/write, restore, drop or SINGLE_USER operation was performed in Task02C.

Trial-only SQL has exact server/database guards, expected ID/code/department guards, SERIALIZABLE transaction, workflow/balance preconditions, trigger detection and audit logging. Default mode rolls back. The orchestrator first verified exactly eight candidates and rollback fingerprints, then committed the same safe subset. No safeguard from Task02B was modified.

## Actual dependency decisions

### Users

Users 11/17/18/20 remain ACTIVE; no account or session update was performed.

- User 11 has a CONFIRMED shift-4 assignment, PRESENT attendance and no check-out. It is unsafe to deactivate before resolving that active workday. Its one unrevoked SQL session is preserved because the account has not been deactivated.
- Users 17/18/20 have no open direct task assignments discovered by the incoming-user-FK inspection, and their DH role assignments were already inactive. However, they remain manager_user_id of still-active SEC/FB/SEC departments (IDs 5/6/10). Their final account retirement is deferred rather than introducing inactive managers for still-active departments or silently clearing/reassigning those management links. This is a conservative consistency decision, not a claim that UC-006 defines an additional explicit manager-FK guard.
- User 20 was also manager of warehouse 8; that warehouse is now inactive. No manager/role/branch mapping was invented.

FE-01 requires account-status change and revocation of every unrevoked session in the same transaction with user-before-session locking, plus audit. No credentials should change or be logged. Since no account was disabled here, no session was revoked. Future account resolution must apply that policy, not leave sessions unrevoked when disabling accounts.

### Warehouses

Preflight covered all incoming warehouse FKs and stock/movement/document/request/fulfillment/stocktake tables. No separate reservation or balance table was found in the current schema; pending material requests/fulfillments were inspected as well.

- **WH-SEC (8) and WH-LAU (9): deactivated.** Zero movement rows/balances, no stock documents/material requests/fulfillments/stocktakes referencing either candidate. Responsible users and historical FK values retained.
- **WH-FB (4): unchanged and BLOCKED.** Item 14 has 384.000 units; item 15 has 192.000 units. Material requests 8/9 are APPROVED and fulfillments 8/9 PREPARING. Documents 3/8 are POSTED; stocktakes 3/7 are APPROVED/COMPLETED. No stock write, reversal or invented transfer destination was used.

GBR-091 requires zero balances and no open documents before deactivation. The task also prohibits leaving open supply workflows stranded. A reviewed stock destination and disposition of the pending workflows are needed.

### Request 13

Remains DRAFT, type FB_ORDER (6), requester 23, receiving FB. One CREATED event; no handler assignment found. The database permits CANCELLED as a value, but that is not authorization for DRAFT→CANCELLED. UC-123/GBR-154 and the transition table only describe PENDING_ACCEPTANCE→CANCELLED; current schema uses SUBMITTED instead of PENDING_ACCEPTANCE. No confirmed draft-cancel workflow or corresponding actor/event requirements were found. No status/event/time was manufactured; request 13 and type 6 remain unchanged pending an approved rule.

### Shifts

- Shift 4 remains IN_PROGRESS. Users 11 and 4 have actual check-ins on 16/10/2026 (05:55 and 06:00) but no actual check-outs. Scheduled end 14:00 is not evidence of actual end. No end time was fabricated.
- Shift 5 remains PLANNED, scheduled 16/10/2026, with no assignments/attendance. UC-138 documents cancellation of a future assignment, not cancellation of a past scheduled WorkShift row; no implementation or approved WorkShift transition was found. No arbitrary CANCELLED value was introduced merely because the column is unconstrained.

Both require a valid historical workday correction/closure or shift-cancellation rule. User 4 remains DH Branch 1, not disabled to resolve the shift.

### Catalog

GBR-085 supports soft-deactivation of used reference codes while preserving history.

- Deactivated request types **4 LAUNDRY, 5 SECURITY, 9 EVENT_SETUP**. Their current references are completed/cancelled or absent. Type **6 FB_ORDER** remains active due request 13 DRAFT.
- Deactivated work categories **2 LAUNDRY, 10 SECURITY_PATROL, 11 FB_SERVICE**. No open task references; task 14 is COMPLETED with completed assignment. It is an **operational task**, not a maintenance_work row.

## Applied changes and evidence

Exactly eight existing `is_active` flags changed: warehouses 8/9, request types 4/5/9, work categories 2/10/11. Exactly eight audit_logs appended in the same transaction. UPDATE audit has old/new flags, reason and Task02C marker; actor_user_id is NULL for database maintenance, not an impersonated staff member. No other fields or rows in those catalogs changed. Existing audit history remains hash-identical. Rollback audit inserts can leave normal identity gaps; no reseeding was performed.

No users, sessions, departments, assignments, request/shift states, stock movements, fulfillment or historical references changed. All 68 other tables retain exact counts and SHA256 fingerprints. Backup, credentials and connection strings are not stored in Git.

## Verification results

| Check | Result |
| --- | --- |
| Backup COPY_ONLY CHECKSUM / VERIFYONLY | PASS |
| Safe subset dry run / rollback | PASS; all 72 table fingerprints restored |
| Safe subset COMMIT / audit | PASS; eight flags / eight audits |
| Exact flags and audits postflight | PASS |
| Department dry run | Four flags can be toggled structurally, then rolled back; not acceptance |
| Unchanged department commit guard | SQL 52005 still rejects, no department COMMIT |
| Final department acceptance | BLOCKED; FB/LAU/SEC still active |
| RBAC exact comparison | PASS; roles/permissions metadata and grants/scopes match Release |
| RBAC quantities/personnel | 12 roles / 10 active / 62 permissions / 140 grants; no DEPARTMENT scope; OM/DH/Storekeeper unchanged |
| CHECKDB / CHECKCONSTRAINTS | PASS |
| FK/CHECK/unique indexes | Enabled/trusted as applicable, no disabled unique index |
| Users/authentication/SQL sessions | Entire table fingerprints unchanged |
| All business history and stock | Fingerprints unchanged; audit append only |
| HotelOpsDB | All 72 table fingerprints unchanged against immediate and Task02 baselines; still 11 roles / 47 permissions / 182 grants |
| Protected files | Task01, config, shared Release and Task02B SQL52005 script hashes unchanged |

Remaining SQL52005 groups: active users 4; active warehouse 1; open internal request 1; active/planned shifts 2; active request type 1; active work categories 0; open operational tasks 0.

Full-schema column comparison still reports 712 expected/actual columns and zero differences; this does not claim every index/default definition was independently compared. Release script 04 was run read-only and still warns about the four active legacy departments.

## Remaining decisions and official Release readiness

Trial has **not** reached final department model; do not report PASS or upgrade HotelOpsDB.

Needed: valid DRAFT request cancellation rule (and event/reason/actor); WorkShift cancellation/actual historical check-out correction policy; destination/disposition for WH-FB stock and pending fulfillment; consistent account/active-manager-link retirement and FE-01 session revocation. No missing times or warehouse mappings can be inferred from scheduled data or IDs.

After resolving those decisions, take a current trial recovery point, apply guarded workflow transitions with audit, rerun unchanged UC-026/52005 preflight, dry run and commit four department flags only when no blockers remain. Integrate reviewed dependency resolution and retirement as explicit steps before final Release verification. Keep environment-specific IDs out of shared Release; future seed must not recreate obsolete active defaults. Standard Release remains unedited.

## Files and reruns

All new task artifacts are under Task02C: Run-Task02C.ps1, preflight.sql, resolve-safe-dependencies.sql, this report, preflight/user-FK/postflight reports, backup metadata, dry/commit reports, source/trial fingerprints, department/Release verification, protected hashes and result.json. Existing Task02 read-only verifier regenerated its own equivalent verification evidence; source/Release/Task01 were not edited.

Execute refuses replay once result.json exists. Do not restore/drop or replay the completed trial. No C# build/test was run because C# was unchanged. PowerShell parse and git diff checks are used for scripts. No commit/push/merge/PR.
