# DEV2 MASTER EXECUTION 001 — dependency readers and paired writer coordination

Stacked on P0 commit 24189b4165e2cc6b08e09a7c90f5730d0793cb78, itself based on cached integration commit
5411c1b86006d767fbad9aaf4c78cc23c9ece3e4. GitHub network unavailable; remote HEAD and PR publication unverified.
Do not integrate P0 placement changes alone: paired User-first corrections below complete the reviewed source scope.

DEV2 source changes only; no generated entities/mappings/schema/shared RBAC/DEV1/DEV3 writer changes.
Program.cs contains THREE additive scoped registrations (P0 handoff's earlier four-count was corrected here).
Notification foundation, worker/producer flags and FS013 remain unchanged; Fe12 facts adapter is the sole new file in Notifications.

## UC016 paired writer coordination

Condition/Retirement/Update Asset paths acquire eligible actor User before Asset.
Create Handover locks sorted actor+receiver Users before Asset; finalization locks relevant custody Users+actor
for acceptance and actor for rejection before retained Asset/Handover locks. Discovery reads remain hints only.
Incident status and triage acquire eligible actor before Incident. These changes prevent the actor-User/feature
inversion identified against new placement writers; they do not certify all existing feature-lock interactions.
Maintenance ReturnWork remains byte-identical: Users ascending -> Incident -> MaintenanceWork -> reread -> write/commit.
SQL1205, complete writer census, lock contention in both orderings and rollback still require approved SQL evidence.

## UC006

Dev2DependencyReader.ReadUserAsync(user,branch,ct) is explicitly branch-only, not a global clear decision.
ReadUserGlobalAsync returns GlobalUserDependencySnapshot: ordered branch snapshots plus unscoped or cross-branch
inconsistent assigned Maintenance facts. It requires actual global MASTER_DATA_MANAGE and existing user-management scope.
ReadUserGlobalInCallerTransactionAsync / RevalidateUserGlobalInCallerTransactionAsync bind actor/target to same transaction;
owner must retain existing global-admin/Users universe guard BEFORE individual Users, then authority/source locks.
The reader does not implement that owner guard or approve release. CanDeactivate remains false.

Known approved user blockers: PENDING/IN_PROGRESS/ON_HOLD current Maintenance assignee; active Warehouse manager;
IN_USE/UNDER_REPAIR custodian; PENDING handover receiver; DRAFT/NEEDS_INFORMATION requester; greatest-revision
PENDING approval whose parent is PENDING_APPROVAL. Historical creator/reporter/fulfiller/replenishment requester
does not become current assignee. Unknown vocabularies, completion markers, PM scope and replenishment responsibility
remain explicit unresolved items; existing Dev2ResponsibilityStateQueries is reused, not changed.

## UC012 / UC022 / UC026

ReadBranchAsync / ReadRoomAsync / ReadDepartmentAsync expose authorized internal facts, not HTTP routes or release.
ReadStructureInCallerTransactionAsync(kind,id,branch,ct) requires SERIALIZABLE, eligible actor User first, then retained
Branch lock. RevalidateInCallerTransactionAsync requires the matching transaction/resource lease and exact snapshot hash.
No helper owns commit or performs deactivation/transfer/cleanup.

Source fingerprints include real assignment/parent fields, existing RowVersion where mapped, current statuses/timestamps,
Warehouse manager/department, per-warehouse-and-item nonzero ledger quantities, Material Request links/revision,
fulfillment receipt references and PM run/work bindings. Same ID/status with reassignment now conflicts on reread.
Room coverage includes actual linked Incident/Asset and PM-run relationships and Material Request receiving-room/Incident links.
Department coverage includes Warehouse.DepartmentId, MaterialRequest.DepartmentId, Asset.CurrentDepartmentId,
both handover department links, Maintenance.DepartmentId, PM.AssignedDepartmentId and Warehouse replenishments.
Mismatched Asset/fulfillment/handover/Maintenance/PM source branches remain explicit unresolved facts, not hidden clear results.
Target identity/context facts are snapshot anchors, not evidence that a target's active status is itself an approved blocker.

No unapproved terminal/transfer/receipt policy is invented. Stock nets by item, never unlike items; negative nonzero balances remain facts.
Selected base has no NotificationIntents/C04/FS013 substrate; branch reader emits that missing contract instead of importing frozen Notification source.
DEV1 must provide current-cycle Cleaning/Inspection/RoomBlock inputs for Room Deactivation; DEV3 owns final mutation/audit/rollback.
All branch/dept/room reference-creating writers still require accepted coordination census and SQL races before activation.

## FE12

Fe12MaintenanceDependencyAdapter.ReadFactsAsync reads persisted work identity, authoritative Incident/Department branch,
current assignee, active account/employment/not-deleted + active branch membership, existing MAINTENANCE_VIEW gate,
and stable fingerprint. Caller-transaction read/reread requires SERIALIZABLE and matching source lease; owner must
prelock sorted Users/time, then Incident -> MaintenanceWork, preserving ReturnWork, and authorize the actual source.
This branch-permission adapter does not invent an OWN-source scope mapping.

Selected base lacks mapped MaintenanceWorkCycle and accepted cycle authority. ReadAsync(work,branch,expectedCycle,ct)
rejects MAINTENANCE_CYCLE_CONTRACT_REQUIRED; it never substitutes work ID or a timestamp for a current cycle.
ReadFactsAsync is executable accepted-field source work; Work-Time mutation binding, time-kind/event/recipient semantics,
Notification staging/worker/retry and Shift/Handover behavior remain unaccepted/OFF. Existing DEV3 WorkTimeOperation
interface is a review proposal and remains unchanged.

## Verification and integration status

Current build and focused receipts are in the coordinator's DEV2-MASTER-EXECUTION-001 report directory.
Provider-stub tests execute actual writer paths but prove only first retained User-query ordering, not SQL locks or commits.
SQL and HTTPS remain NOT_RUN; registered runtime adapter, fresh exclusive lease, owned fixtures, permitted target/schema,
least-privileged credentials, no competing writer and preservation/cleanup plan must be provided before execution.
SQL1205 always FAIL, cancellation is not commit success. Existing probe entry refuses execution without approved runtime port.

READY_FOR_FE_IMPLEMENTATION=true for reviewing/wiring these internal source contracts in an isolated consumer candidate.
READY_FOR_API_INTEGRATION=false and READY_FOR_BUSINESS_ACTIVATION=false for all final deactivation/time-write operations.
Policy ambiguity or an empty reader result never removes owner release gates. No deployment/merge/activation is authorized.
