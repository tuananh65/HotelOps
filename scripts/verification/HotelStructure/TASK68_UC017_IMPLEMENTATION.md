# Task68 — UC017 Create Room Type, SRS branch contract

31/10/2026, Asia/Saigon. **PARTIAL — implementation and trial acceptance completed; candidate
migration is not integrated into deployment.** The final contract follows the developer's explicit
SRS-first clarification: branch, standard cleaning minutes and default checklist are all retained.
No production schema is changed and no verified UC points are awarded.

## A. Requirements and model decision

Reviewed [UC017 §2.6](../../../docs/requirements/srs_sections/02_use_case_specifications.md),
[GBR003/004/007/008/036](../../../docs/requirements/srs_sections/05_requirement_appendix.md),
draft Permission Matrix/[change notes](../../../docs/requirements/SRS_CHANGE_NOTES.md),
[database design](../../../docs/database/DATABASE_SCHEMA.md), [database rules](../../../docs/database/DATABASE_RULES.md),
actual DbContext/RoomType/Room/InspectionChecklist/Version/Item/Audit mappings, delivery plan and
[Task67](../RoomDirectory/TASK67_UC023_COMPLETION.md). No separate approved ERD artifact was found
under docs; the database design is a source-derived draft, not an SRS amendment.

SRS requires an authorized logged-in actor, branch/code/name/description/standard cleaning minutes/
default checklist, branch-unique code, Active creation, assignability to Rooms, atomic CREATE audit
and operational attributes only (no pricing). Current canonical schema instead has a **global**
RoomType with6 columns: bigint identity PK, varchar30 globally unique code, nvarchar150 name,
nullable description/standard_capacity and IsActive default1. There is no branch/cleaning/default
checklist mapping. InspectionChecklist already has nullable RoomTypeId; versions/items are real
existing entities. Per the user's clarification, this mismatch is addressed by a separate candidate
SQL migration, not by dropping SRS fields or treating capacity as cleaning minutes.

## B. Candidate schema and compatibility

- [Forward](../../database/release/candidates/Task68_UC017.forward.sql) adds nullable branch_id,
  standard_cleaning_minutes and default_checklist_id, branch/checklist FKs, trusted shape CHECK,
  filtered branch+code unique index and separate legacy-global code unique index. Existing PKs,
  scalar values, Room/checklist references and is_active are untouched. No legacy branch ownership
  or operational default is guessed/backfilled.
- Legacy rows keep all3 added fields NULL. Scoped rows require branch, positive cleaning minutes
  and actual checklist FK. Source creates scoped rows only. Codes in unclassified global legacy
  rows remain reserved by the application across new branches to avoid ambiguous legacy reuse;
  new scoped rows can reuse a code across different branches. No code/name cleanup is automatic.
- Forward checks trial identity/Task17 marker, prerequisite column lengths/tables/original unique
  constraint, normalized legacy collisions, existing version marker and required indexes/FKs/CHECK.
  DDL is transactional, namespace-serialized, idempotent for the supported state; unknown/partial/
  damaged states are rejected rather than silently repaired.
- [Rollback](../../database/release/candidates/Task68_UC017.rollback.sql) restores the exact previous
  unique-constraint name and removes candidate objects **only if no scoped/operational data exists**.
  Otherwise it refuses before dropping data/columns. It never deletes/reparents RoomTypes or Rooms.
- Both scripts are deliberately guarded to **disposable canonical Task17 trials only**. They are
  not referenced by production startup or the release manifest. A production rollout needs an
  independently reviewed package/backup/maintenance/cutover procedure; these files cannot be run
  against HotelOpsDB as-is.
- [RoomTypeSchema.cs](../../../src/HotelOps.Api/Data/RoomTypeSchema.cs) is a read-only module gate:
  Legacy, BranchV1 or Incompatible. Legacy reads remain available; creation503 until BranchV1.
  Unknown marker/disabled required index fails closed. Existing startup/auth schema gate is unchanged.

Generated EF files and canonical SQL are not edited. Candidate columns are accessed through
parameterized SQL on the **existing scoped HotelOpsDbContext connection/transaction**, with dedicated
DTOs. The generated index snapshot still describes the legacy global index; controlled Database
First mapping/index refresh must be reviewed with deployment, not an automatic production scaffold.
No EF migration, new project, synthetic checklist table or fake default record is introduced.

## C. V1 business and authorization decisions

| Topic | Implemented ASSUMED_V1 / requirement |
|---|---|
| Branch ownership | Required positive request branchId; every new row and its audit use that authorized branch. There is no new global/NULL-branch creation path |
| Authorization | Existing MASTER_DATA_MANAGE SQL branch evaluation, active branch and persisted unrestricted session, initial/final checks. Global ALL may create for an explicit branch; branch-bound authority only its own branch. No role-name inference. The original task's global-write rule is superseded by the user's SRS-first branch-model decision; branch grants do not gain global catalog writes |
| Code/name | Trim code/name; uppercase ASCII code1–30 letters/digits/_/-, first alphanumeric; Unicode name1–150/no controls. Case-insensitive normalized lookup, inactive codes reserved. Name is not unique in schema/SRS, so equal names with different codes are allowed |
| Operational fields | standardCleaningMinutes required positive Int32, distinct from optional positive standardCapacity. Description trim/blank→null,max4000,newline/tab allowed. These bounds are explicit V1 input decisions; no pricing/rate fields |
| Default checklist | Required real active **generic template** (InspectionChecklist.RoomTypeId NULL), with a currently effective PUBLISHED version and at least one active criterion. The existing nullable binding supports reusable templates; no checklist is created/copied/rebound by UC017. An inactive/missing/draft/future/type-bound reference is rejected. This conservative creation rule avoids borrowing another RoomType's bound checklist or introducing a circular new-ID dependency |
| State / identity | SQL identity ID, IsActive=true controlled by server. Existing RoomType/Room rows untouched. Unknown JSON fields including id/isActive/actorUserId are400 |
| Audit | Existing audit_logs, entity_type=room_types, CREATE, server actor, actual branch, UTC time, before wrapper{entity:null}, full after DTO and changed-field list. Same transaction as business INSERT |
| Retry | No automatic mutation replay.1205/1222/-2 or application-lock failure503; cancellation propagates. Fresh request after verified rollback may retry; repeated committed code409, no second business/audit record. Identity gaps after rollback are expected |

Generic template eligibility is a documented V1 reference rule, not a claim that UC039 checklist
creation/publication is implemented. An eligible actual template must exist; production data was
not inspected. Tests create real version/criterion fixture rows only in disposable SQL, and use
production service validation without a fake checklist/permission/session provider.

## D. HTTP and source

`POST /api/room-types` over HTTPS with existing cookie authentication and CSRF:

```json
{
  "branchId": 1,
  "code": "DLX",
  "name": "Deluxe",
  "standardCleaningMinutes": 35,
  "defaultChecklistId": 12,
  "description": "Operational room type",
  "standardCapacity": 2
}
```

IDs are examples, not hard-coded grants or manufactured defaults.201 returns dedicated
id/branchId/code/name/description/standardCleaningMinutes/defaultChecklistId/standardCapacity/isActive.
No fabricated GET Location; RoomType detail/update is not added.400 validation/checklist,401 invalid
or expired session,403 branch/permission,409 duplicate code,503 missing/incompatible candidate schema
or transient SQL availability. Controller data responses are no-store; no provider/credential text.

New production files:

- [CreateRoomTypeDtos.cs](../../../src/HotelOps.Api/DTOs/HotelStructure/CreateRoomTypeDtos.cs).
- [RoomTypeCreationService.cs](../../../src/HotelOps.Api/Services/HotelStructure/RoomTypeCreationService.cs).
- [RoomTypesController.cs](../../../src/HotelOps.Api/Controllers/RoomTypesController.cs).
- [RoomTypeSchema.cs](../../../src/HotelOps.Api/Data/RoomTypeSchema.cs).

Existing source modified: one scoped DI registration in [Program.cs](../../../src/HotelOps.Api/Program.cs);
[RoomDirectoryService](../../../src/HotelOps.Api/Services/Rooms/RoomDirectoryService.cs) and
[BranchStructureService](../../../src/HotelOps.Api/Services/HotelStructure/BranchStructureService.cs)
reuse a compatible Room query. On BranchV1, a Room with a foreign branch-owned RoomType is hidden
before count/page/detail projection; legacy-global type references stay visible. This is the
necessary impact on existing readers. The Room FK itself is retained; future Room assignment
writers must enforce type/Room branch compatibility rather than rely on that legacy ID-only FK.
No current production Room create/assignment writer was found or certified by the fixture INSERTs.

Verification reuses [Program.cs](Program.cs) with --task68, adds [Task68RoomTypeCases.cs](Task68RoomTypeCases.cs)
and [Task68RoomTypeFaults.cs](Task68RoomTypeFaults.cs). No new test framework/project. Candidate SQL
files above and the delivery plan are the other deliverables. DEV2, generated entities, shared
DbContext mappings, auth implementations, UC006 gates and UC016 runtime files remain unchanged.

## E. Transaction and lock order

Own clean SERIALIZABLE transaction; **actor User → SQL Session/grants → Branch U/HOLDLOCK →
RoomType namespace application lock → schema gate → Checklist/Published Version/criteria retained
reads → normalized branch/legacy duplicate check → parameterized INSERT → audit INSERT → final
session/grant check → COMMIT**. The namespace also participates in candidate DDL. User locks are
retained through audit; no feature→new User lookup or stale EF tracked entity is used. The ADO
INSERT explicitly uses the current EF transaction, command timeout and CancellationToken.

Actual checklist deactivation winning the lock is reread/rejected. Serializable ref reads prevent
published/active eligibility from changing before commit; time expiry is rechecked. Failed audit,
SQL fault or cancellation disposes/rolls back both inserts; audit tracker entry is detached. Test
deadlock inversion deliberately belongs to the harness, not to the production lock order. Neither
these schedules nor read filters certify arbitrary external writers/global deadlock freedom.

## F. Executed evidence

| Verification | Result |
|---|---|
| [Final UC017 receipt](task68-accepted.local.json) | **130 UC017+2 startup=132 PASS**,10 DMV-observed waits,7 post-INSERT faults/expiry schedules. Actual root API HTTPS/login/cookie/SQL/CSRF; persisted branch attributes/checklist FK/audit; auth/scope/strict inputs; physical uniqueness; same code across branches; legacy preservation; real HTTP cancel/stale checklist; metadata-negative probes |
| Migration/rollback | Legacy read200/create503; unknown contract read503/forward rejected; forward+idempotency preserve original scalar data and Room FKs; empty-candidate rollback restores original schema/constraint; reapply succeeds; populated rollback refuses safely; disabled uniqueness protection blocks creation/idempotent forward, restoration preserves data |
| Actual post-INSERT faults | Audit THROW51069, SQL1222, command timeout-2, real deadlock1205, cancellation, SQL-session expiry and assignment expiry. In-transaction SQL proves the raw business INSERT already exists before injected audit fault/wait. All cases remove both row and audit, clean transaction/tracker; fresh retry commits once, replay409 |
| [Legacy-schema UC023 SQL](task68-legacy-read.local.json) |117 PASS/11 DMV waits on **unpatched** canonical schema, including all-table snapshot preservation, original session/scope/count/page/cancel/provider-fault behavior |
| [Scoped-schema UC023 HTTPS](task68-scoped-rooms.local.json) |227 UC023+2 startup=229 PASS/4 DMV waits on migrated trial. Full list/detail/reason/source/history plus scope/cancel/timeout/final-expiry behavior retained |
| [Scoped-schema UC013/014/015](task68-scoped-structure.local.json) |589 PASS=215 UC014+159 UC013+215 UC015,53 waits/14 post-UPDATE UC015 fault schedules. Same production services on migrated trial; no production schema modification |
| Auth foundations |174 PASS:43 PermissionService+61 Authorization+49 ResourceAuthorization+21 Hardening; their documented doubles/loopback scope is retained. Live SQL authorization is certified by UC017/HTTP receipts, not inferred from foundation tests |
| Build/test/diff | Final solution build PASS,0 errors,NU1900 feed warning; targeted harness builds PASS. dotnet test exit0/no test project; console suites explicitly executed. Final diff/new-file/link/preservation checks recorded in obj/task68-validation.local.json |

Final creation trial: `HotelOps_Canonical_20261030_171442_6745fc1d_Fresh`. All runs use exact trial
instance/name/Task17 marker checks and startup schema compatibility; approved trial-only users
rowversion prerequisite is unchanged. No HotelOpsDB connection or deployment operation occurs.

First candidate attempt (obj/task68-acceptance.local.log) hit SQL102 near QUOTENAME in dynamic DDL,
before the migration executed. It was corrected to construct SQL in a variable and invoke
sp_executesql. Subsequent full migration/rollback/HTTP/SQL runs passed; the failed trial/log and
successful125-check intermediate receipt are retained. No error or earlier artifact was erased.

## G. Classification and remaining deployment decisions

**UC017 PARTIAL**, not COMPLETE_VERIFIED: candidate schema is not integrated/released. Before
production use: approve/canonicalize migration and maintenance/backup/rollback procedure, review
Database First mappings/index metadata, verify actual eligible templates and legacy ownership/code
cutover policy without guessing/backfilling, and rerun deployment compatibility acceptance. The
candidate deliberately refuses destructive rollback once scoped data exists. No live migration or
permission catalog changes are authorized/performed by this task.

Implementation preserves the complete requested SRS field set and exercises real successful
creation on trial SQL. It does not convert a missing business requirement into an unsupported
response or drop branch/cleaning/checklist just to award completion. No UC039 or Room writer
acceptance is claimed. **Gain0/2**, DEV1 remains **10/75,25/256=9.77%**. UC016 remains PARTIAL.
Next: reviewed UC017 deployment integration; then UC018 Update Room Type using this approved
branch/checklist/concurrency contract, without silently weakening legacy and Room-reference rules.
