# Task80 — DEV2 writer handoff and UC138 execution readiness

## A. DEV2 handoff — actual source, not draft implementation claims

Current branch: feature/BE1-database-integration, HEAD4a187320643d0c82f232c6b9dd5a667b8401c280.
**Important integration distinction:** UC029/041/046 writer source is inspectable in
snapshot `cf8fb02`, but its files/controllers/common RoomTypeCreationService partials are
**absent from the current working tree**. Do not wire a publisher into assumed live methods
or bulk-restore baseline files. Ledger PARTIAL is historical implementation evidence, not
proof these routes are currently callable. No restore/shared integration performed here.

| UC / ledger state | Exact source/method and availability | Authoritative facts, state writes and missing integration |
|---|---|---|
| UC029 Assign Cleaning Task / PARTIAL | Snapshot `src/HotelOps.Api/Services/Housekeeping/HousekeepingAssignmentsService.cs`, `RoomTypeCreationService.AssignCleaningAsync(...)`; absent locally. Snapshot controller `HousekeepingWorkflowController.AssignCleaning` | Branch comes from task->Room plus scoped active room query. Strong rowversion tag. PENDING_ASSIGNMENT -> ASSIGNED workflow; legacy status remains PENDING. Legacy PENDING adoption requires explicit AdoptUnassignedLegacy. No competing ACTIVE/unended assignment; inserts CleaningAssignment ACTIVE with assignee, AssignedBy actor, server AssignedAt/reason and task due_at. EligibleHousekeeper requires active account/employment/HK membership/HK_STAFF role, current branch access and on-shift ASSIGNED/PLANNED eligibility. Missing committed-assignee inbox delivery/retry/replay and room-lifecycle coordination. |
| UC037 Monitor Cleaning Tasks / PARTIAL | **Current** `src/HotelOps.Api/Services/Housekeeping/CleaningMonitorService.cs`, `ListAsync`, `DetailAsync`, private `ReadAsync`; controller `CleaningMonitorController.List/Detail` | Read-only projection; no writer or Cancel method. Branch joined through Room; cleaning/rework lineage verified; assignment/progress/audit timelines, assignee IDs and recorded vs optional workflow state are evidence, not inferred actor responsibility. SQL session plus CLEANING_VIEW branch/global grant, OWN not promoted. Serializable read transaction, Gate before/after reads, no business audit writes. Mandatory Cancel, notifications and room lifecycle remain absent. |
| UC041 Assign Room Inspection / PARTIAL | Snapshot same assignments file, `AssignInspectionAsync(...)`; absent locally. Controller `HousekeepingWorkflowController.AssignInspection` | Branch/task/active room/floor/area resolved; cleaning PENDING_INSPECTION or rework PENDING_REINSPECTION prerequisite; at most one open inspection. Persisted RoomInspection.InspectorUserId is recipient identity. EligibleInspector = active employment/account, branch access and active branch HK_SUPERVISOR assignment. SoD excludes cleaning assignment/progress/rework performer, including historical non-cancelled assignment. Applicable Published checklist/version/items fixed at assignment. Legacy inspection PENDING, workflow ASSIGNED, due_at; no notification/outbox integration. Source permission is **INSPECTION_PERFORM**, not a guessed INSPECTION_ASSIGN code. |
| UC044 Approve Room Inspection / NOT_IMPLEMENTED | **No writer found** in current tree or inspected snapshot controller/service. Snapshot `InspectionExecutionService.cs`, `ExecuteInspectionAsync(..., operation,...)` is NOT an approval method | Accepted operations only read/start/save/finish/defect; source.InspectorUserId must equal actor, INSPECTION_PERFORM gate. Finish writes PENDING_CONCLUSION with legacy IN_PROGRESS/inspected_at, not PASSED/Ready. Current assignment/supervisor/SoD validation, full pass criteria, blockers/effective-room-state checks, conclusion audit and notification are not implemented. Never map finish to approve or claim Ready transition. |
| UC045 Reject Room Inspection / NOT_IMPLEMENTED | **No writer found** in current tree or inspected snapshot. Same execution service does not accept reject | Recorded FAIL criterion/OPEN defect is evidence, not inspection rejection. No atomic FAILED conclusion + cleaning REWORK_REQUIRED + defect/rework/room lifecycle + required notifications. Do not treat defect recording as UC045. |
| UC046 Request Cleaning Rework / PARTIAL | Snapshot assignments file, `RequestCleaningReworkAsync(...)`; absent locally. Controller `HousekeepingWorkflowController.Rework` | Inspection/task/Room lineage and branch checked; rowversion, workflow FAILED + cleaning REWORK_REQUIRED; no open non-completed/non-cancelled rework. Selected defect IDs distinct,1–200, all belong to inspection; inserts rework with committed AssigneeUserId/reason, legacy PENDING/workflow PENDING_ACCEPTANCE, due_at and relational cleaning_rework_defects. Same eligible on-shift housekeeper check. INSPECTION_PERFORM branch gate; recipient from persisted assignment, not client replay recomputation. Missing notification/delivery/retry/replay and full lifecycle/effective-block integration. |

### Shared snapshot writer boundary (UC029/041/046)

`cf8fb02:src/HotelOps.Api/Services/HotelStructure/RoomTypeBatchService.cs`:
`BatchAsync` (line22), `BatchAuditAsync` (line193). Actual containing type is
`HotelOps.Api.Services.HotelStructure.RoomTypeCreationService`, despite Housekeeping folder.
Do not create a second service assuming the filename is its C# type.

BatchAsync requires clean SQL context/no ambient transaction; begins **Serializable**,
locks actor and additional assignee/inspector users through UserEligibilityLock in sorted
order, validates actual session and SQL permission, eligibility, active branch, transaction-owned
CatalogLock and required RoomType/Housekeeping schema gates. Revalidates session/grants and
eligibility before outer commit. Non-success returns roll back; timeout/deadlock fails unavailable.
CatalogLock is exactly `HotelOps:RoomTypes:v1`, defined in snapshot
`Services/HotelStructure/RoomTypeCreationService.cs:24`; do not substitute A1's time lock
or another lock resource when integrating these absent writers.

BatchAuditAsync inserts CREATE/UPDATE with ActorUserId, BranchId, EntityType/EntityId,
server OccurredAt, before/after JSON and ChangedFields, same DbContext/transaction. Multiple
SaveChanges and ChangeTracker.Clear do not mean separate commits. Publisher integration must
participate in that exact outer transaction; no callback/network delivery/independent commit.
Current writer does not call a publisher. Existing schema markers, branch RoomType contract,
workflow columns, rowversions and link tables must be separately restored/reviewed before
these snapshot methods can run in this branch.

Relevant snapshot gate: `src/HotelOps.Api/Data/HousekeepingSchema.cs`, InstalledAsync checks
HotelOps.Housekeeping.V1, task/inspection/rework row_version, workflow_status/due columns,
trusted CK_cleaning_workflow_v1/CK_inspection_workflow_v1/CK_rework_workflow_v1 and defect/evidence
link FKs. Do not replace workflow_status with legacy status merely because EF entities lack it.
Actual snapshot SQL is `scripts/database/release/housekeeping/01_Housekeeping.sql`, with
02_Verify.sql/03_Rollback.sql alongside. Inspection PASSED maps to legacy COMPLETED/PASS;
FAILED to COMPLETED/FAIL. Rework PENDING_ACCEPTANCE maps to PENDING; IN_PROGRESS and
PENDING_REINSPECTION map to legacy IN_PROGRESS; these CHECK mappings are not proof that
Approve/Reject writers exist. SRS primary actor for UC044/045 is Housekeeping Supervisor;
its current-cycle performer/SoD and final conclusion boundary still require implementation.
Source/blob/availability inventory: [SNAPSHOT_WRITER_EVIDENCE.json](SNAPSHOT_WRITER_EVIDENCE.json).

### Workday A1 interface available in current tree

`src/HotelOps.Api/Services/WorkTime/IWorkTimeTransactionParticipant.cs`:

```csharp
Task LockUsersAsync(IReadOnlyCollection<long> actorAndOwnerIds, CancellationToken cancellationToken);
Task<WorkTimeIntervalResult> OpenIntervalAsync(WorkTimeOperation operation, CancellationToken cancellationToken);
Task<WorkTimeIntervalResult> CloseIntervalAsync(WorkTimeOperation operation, long intervalId,
    byte[] expectedRowVersion, CancellationToken cancellationToken);
```

WorkTimeOperation = `(ActorUserId,OwnerUserId,BranchId,WorkItemKind,WorkItemId)`, trusted backend
only. SqlWorkTimeTransactionParticipant uses same scoped DbContext and caller-owned transaction;
no method commits. All user locks first, then A1 time locks, source locks and fresh source
authorization. SQL enforces one WORKING day/user and one open interval/user globally. Clock
is SQL SYSUTCDATETIME; no ShiftAttendance substitution. Source identity/current ownership/
branch and expected interval rowversion are authoritative, not request claims.

CLEANING/REWORK/TASK sources supported by A1 consistency checks. INCIDENT and
MAINTENANCE_OCCURRENCE fail closed pending DEV2 identity/participant. B1.1 concrete alias
and interface resolve to the same scoped participant; preserve its fixed transaction lock lease.

### UC139 B1.2 / maintenance-PM handoff

See [Task79 exact proposed B1.2 contract](../Task79/B12_APPROVAL_SCOPE.md). DEV2 checkpoint:
feature/DEV2-coordination-contracts, same HEAD4a18732, draft blob
dc5ceb10121f752351e56e4b7150277c96271e1c, DRAFT_READY_FOR_JOINT_REVIEW only. User confirmed
publisher/outbox/replay/worker not implemented. This task does not search for a nonexistent
publisher implementation or claim the inaccessible draft has been read.

Unscheduled Start needs approved unique branch-DH relationship, no eligible own shift,
reason, atomic business/audit/durable intent, durable operation-result replay and readable
inbox delivery. DEV2 must provide joint-approved publisher/replay contract, source and
acceptance. No independent queue, fake publisher or unapproved operation-store schema.

Maintenance/PM Pause All adapter must resolve actual current occurrence/work-item ID,
owner, branch, status/revision and permission; historical PerformedBy is not current ownership.
Use same actor/owner sorted User locks, A1 participant/context and outer transaction. Hold
source locks; A1 CloseInterval requires source still IN_PROGRESS, so coordinate close plus
approved pause/hold, source history/audit/responsibility, then End Workday atomically. Source,
interval, audit or intent failure rolls everything back. No inner commit or direct DEV1
maintenance status write. Unsupported/ambiguous kind fails closed. Rework pause still needs
an explicit SRS-consistent transition; partial-kind coverage is not full Pause All.

This is a local handoff artifact; no message was sent to DEV2 automatically.

## B. UC138 readiness decision

SRS §12.2 UC138 and functional screen Shift Management require definitions+weekly roster,
create/edit code/name/start/end, date-based employee assignments, overlap prevention,
row_version stale-write protection (GBR005), same-transaction audit, future-assignment
cancellation retaining history, and **in-app notification to affected employees (GBR010)**.
Do not convert this into read-only roster or omit notification to claim completion.

**Full acceptance is not feasible under existing approved contracts.**

| Concern | Actual source / remaining gate |
|---|---|
| Existing model | ShiftTemplate: branch/code/name/TimeOnly start/end/BreakMinutes/CrossesMidnight/active. WorkShift: branch/department/template/date and concrete ScheduledStartAt/EndAt, PLANNED legacy default. ShiftAssignment: user/shift, ASSIGNED default, assigned/confirmed/cancellation fields. ShiftAttendance separate; not Workday/WorkInterval. |
| Concurrency schema | No RowVersion in any of3 shift entities/mappings. Unique template `(BranchId,ShiftCode)` and assignment `(ShiftId,UserId)` do not prevent overlapping different shifts/dates/branches. Need specifically approved rowversion mapping/DDL or an explicitly approved alternative; do not silently replace GBR005 by an audit hash. |
| Authorization | Existing catalog grants SHIFT_MANAGE/BRANCH and SHIFT_VIEW/BRANCH to DEPT_MANAGER. Proposed UC138 binding needs feature approval; B1.1 approved only own SHIFT_VIEW/ATTENDANCE_RECORD. DH is branch-wide; Department is roster context, not a one-department role restriction. Current resolver supports own shift_assignment only, not SHIFT_MANAGE admin scope. |
| Notification/replay | Same DEV2 implementation gate as Task79; assignment changes/cancellation must notify actual affected users, readable inbox and durable retries. Not optional. |
| Template edit semantics | Define whether edit affects future materialized WorkShifts/assignments or only future creation; historical snapshots must not be rewritten and propagated edits must rerun every affected employee's global overlap checks. Equal start/end and overnight interpretation must be explicit; do not guess24-hour semantics. |
| Cancel history | Future eligibility/timezone/server-time, no started Workday/interval references, cancellation reason and exact status mapping must be reviewed. Do not hard delete or invalidate A1's existing ShiftAssignment FK. |

## C. Safe component implemented; limits of verification

New DEV1 source: `src/HotelOps.Api/Services/WorkTime/ShiftAssignmentConflictReader.cs`,
`HasOverlapAsync(long employeeId, DateTimeOffset startUtc, DateTimeOffset endUtc,
long? excludeAssignmentId, CancellationToken ct)`.

It reads actual non-CANCELLED employee assignments globally across branches with AsNoTracking,
uses concrete persisted UTC ScheduledStartAt/EndAt and existing WorkIntervalMath.Overlaps,
permits adjacency, detects overnight/one-tick overlap, supports server-validated edit exclusion,
rejects invalid/zero candidate ranges and fails closed on malformed stored shift ranges.
Unknown legacy assignment statuses are not treated as available. No template wall-time
conversion, guessed24-hour rule, permission bypass, endpoint/DI activation, mutation or audit.

This helper is **not** concurrency protection or an authorization boundary. Future writer must
validate actor and referenced employee/branch/department, retain sorted User and SQL source/
range locks, and rerun overlap in the atomic audit/outbox transaction. A positive absence
of overlaps from a standalone read never authorizes an insert.

**81 real SQL component checks PASS** on approved disposable
`HotelOps_Canonical_20261104_063549_81d4bc64_Fresh`: same/cross-branch conflict, adjacency,
overnight, cancelled/excluded assignment, tick precision,64 independent occupied-slot oracle
cases, invalid inputs, unknown legacy state, corrupt stored shift, no tracking and full fixture
rollback. No schema changes. [Local receipt](shift-conflict-sql.local.json).

Production `dotnet build HotelOps.slnx` PASS0 errors,2 NU1900 warnings; harness build PASS0
errors,4 NU1900 entries. NuGet vulnerability feed unavailable. `dotnet test HotelOps.slnx
--no-restore` exits0 with no framework cases: NOT AVAILABLE. New reader is an internal component,
not a new public workflow: real new UC138 HTTPS/RBAC/branch-authorization acceptance,
competing shift writes, transactional business audit and audit/outbox fault rollback are
**NOT RUN / BLOCKED**, not inferred from81 read checks. SQL cross-branch overlap is data
conflict coverage, not proof of API branch isolation. Existing B1.1 own-shifts is not UC138
weekly roster/admin CRUD and was not relabeled as UC138 acceptance.

Relevant existing WorkIntervalMath regression result and final preservation/hash/diff checks
are recorded in [TASK80_VALIDATION.json](TASK80_VALIDATION.json). No broad baseline restoration,
new business controller, shared schema/mapping/DI/RBAC or DEV2 edit was made.

## D. One narrow approval/dependency request and next action

Review [UC138_APPROVAL_SCOPE.md](UC138_APPROVAL_SCOPE.md): the minimum shared schema/mapping
gate is exactly3 rowversions plus5 C# mapping/model targets on a new disposable trial,
not a blanket full-UC implementation approval. Exact additive diff and future acceptance
are specified there; none applied. Notification event/replay/inbox delivery remains the
confirmed external DEV2 dependency. SHIFT_MANAGE administrative binding and template edit/
cancel materialization semantics must be explicitly reviewed before a public writer.

Keep UC138 **NOT_IMPLEMENTED** for full workflow, UC139/111 **BLOCKED_EXTERNAL**; UC029/037/
041/046 PARTIAL and UC044/045 NOT_IMPLEMENTED. No PARTIAL counted complete.
**26/75 UC;60/256 weighted points=23.4375%; delta0 UC/0 points.**
Ledger [UC_LEDGER_TASK80.json](UC_LEDGER_TASK80.json) preserves all75 classifications/weights/
earned points; adds actual handoff/readiness evidence only. Elapsed time is measured from
saved preflight in validation receipt; initial requirements review predates that clock.
