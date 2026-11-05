# Implementation plan after approval — no execution in FS007

Order requested: UC028→UC029→UC030→UC031→UC032 regression→UC034→FE05. A step's missing unrelated downstream contract must not prevent an independently safe earlier slice. All proposed filenames/routes below are planning targets, not implemented APIs. Use single HotelOps.Api, no new layers/projects, preserve generated entities and Database First.

## 0. Gate/package baseline

Recheck HEAD/source hashes, approved file/hunk receipts, new source package availability, exact target schema. Integrate only reviewed diffs; no blanket restore handover or merge other worktrees. Keep GET assignment-options and active monitor unchanged. Apply approved additive sidecars/Notification schema only under separate SQL authorization; absent/incompatible version must fail closed. Existing Assign POST needs server-side guarded boundary before new production-ready consumption; do not rely on disabled frontend button.

## 1. UC028 Create

- Proposed files: Controllers/CleaningTaskLifecycleController.cs; DTOs/Housekeeping/CleaningTaskCreationDtos.cs; Services/Housekeeping/CleaningTaskCreationService.cs (partial of existing RoomTypeCreationService, preserving local convention); feature RoomCleaningCycleReader/RoomReadinessReducer/RoomReadinessParticipant helpers. Exact approved new Data partial/hand-authored sidecar entities and release scripts only if R02 approved; never generated Room/CleaningTask modifications.
- Proposed APIs: GET branch cleaning-task-options?roomId and POST branch cleaning-tasks per UC028_CREATE_PROPOSAL; new route inventory/collision tests; existing Rooms read not redefined. Pure reducer/helper can avoid new Program registration if composed by existing feature boundary; any actual DI/constructor/shared Batch hunk still needs exact approval, not an assumed necessary platform rewrite.
- Frontend: CreateCleaningTaskScreen reuses panel/form with real options/validation; server result+ETag, navigate authorized header/monitor when read permission allows; refresh real list. No prototype IDs/status/actor or fallback. No unsafe retry, duplicate409 shows authorized existing task; ambiguous response triggers read recovery.
- Permission: P01 CLEANING_ASSIGN actual room branch + hierarchy; no new grant. SQL: task workflow/coarse mirror, pointer, derived event, audit; room legacy/occupancy/block unchanged; template definitions read only. Fresh CSRF, one outer Serializable tx; no notification required for Create alone.
- Gates: R02 + C028/P01 approved, exact source/DDL/EF compatibility, reviewed preview→first Start snapshot timing, bounded SQL writes. Tests: active/inactive/cross-branch refs/unknown legacy/duplicate open states, parallel Create/room lock, fault atomicity, unchanged legacy, UI refresh/read/no retry, UC032 baseline.

## 2. UC029 Assign

- Reuse active HousekeepingWorkflowController.AssignCleaning and HousekeepingPlanningController; modify only reviewed UC029 path in HousekeepingAssignmentsService and exact Batch integration hook. New feature AssignmentNotificationAdapter uses accepted A01; actual namespace/signatures from DEV2 package, no invented INotificationPublisher implementation.
- DEV2 owns revised B01/B02/C01 files/worker/schema; Program modifications only exact reviewed hunk from current hash. Same scoped DbContext/physical transaction; no parallel transport/commit. Add source-specific immutable AssignmentId+event facts/result/replay hook and explicit recipient read adapter.
- Proposed narrow GET task header under current CLEANING_VIEW with approved assignmentId read relationship; add Controllers/DTO/Service/authorization-resource changes only after P03/P04 review. Existing branch monitor remains unchanged. Don't use UC032 GET as ASSIGNED notification destination.
- Frontend CleaningAssignmentScreen: turn draft into actual POST only after mandatory producer guard and dependencies present; reuse current getJson/UC032 fresh-CSRF conventions, actual task HEX ETag from planning, stable GUID Idempotency-Key, no blind retry; DTO stage/delivery truth, error/state/draft preservation, reload choices, refresh list/detail. Transport extension within existing DEV1 client only if needed; no new competing client/auth store.
- SQL: new ACTIVE assignment, task ASSIGNED/PENDING+due/version, audit, completed operation/publication/intents same transaction. Worker later inbox under its durable transaction. Workload advisory; no Working-day or max-task policy invented. Dependencies/gates: A01 source, revised B01/B02/C01 applied+verified, P03/P04, exact reservation/source lock ordering, SQL write approval. Test complete replay/recipient/source-read/intent rollback/delivery retry/inbox with real context; original routes/UC032 regression.

## 3. UC030 Reassign

- Future separate request/response/action in lifecycle controller/DTOs + Services/Housekeeping/CleaningTaskReassignmentService.cs, no call to initial Assign to replace active owner. Route contract reviewed separately rather than minted now. Existing CleaningAssignmentScreen can expose action-specific mode only after approval; don't enable by status label alone.
- CLEANING_ASSIGN action binding; discover and lock old/new users then A1 time/source/Room according exact order; close old open interval before invalidating old-source ownership, preserve interval/assignment history, insert new assignment→ASSIGNED, derived event+audit+old/new intents in outer tx.
- Gates: approved time/source and history semantics, readiness, old/new source-read/notification events, new SQL scope. Tests: old time preserved, only new actor executes, same-assignee/no-op explicitly specified, stale/revoke/both users race, all-or-nothing intent/interval/source failures.

## 4. UC031 Start

- Future lifecycle Start action/DTO/service partial; CleaningTaskDetailScreen adds action beside preserved UC032, not through percent update. Preserve actual task/assignment source identity, CLEANING_EXECUTE own responsibility, same-branch Working Workday and one open interval. No Admin bypass.
- Reuse A1 same scoped instance; exact stage-IN_PROGRESS/first-start/freeze/helper/open-order reviewed as dependency doc. Freeze room-type cleaning items once, first actual start preserved on resume. SQL source, progress snapshot/revision, interval, readiness-event/audits one transaction; no mandatory Start notification invented.
- Gates: exact time/source flush/locks and P06 scheduled Workday route review if needed; full UC138/B1.2 can remain deferred. Tests: no Working day, other interval, foreign owner, expired session/grant after wait, first-start/freeze once, failure rollback, effective block/unknown-room-readiness behavior approved (do not invent blanket blocked-room start rule).

## 5. UC032 regression

- Existing WorkProgressController/WorkProgressService/cleaningProgress.ts/CleaningProgressPanel unchanged except only necessary approved helper extraction. Don't replace progress ETag with parent task/Workday tag. Cases: own unique ACTIVE assignment, frozen items/evidence, CSRF/version, percent100 stays IN_PROGRESS, branch isolation, audit rollback.
- Run relevant107baseline+new xUnit,39baseline+new Node/browser and real SQL progress reads. Actual live PUT regression is a write and needs new/reconfirmed specific scope; historical FS004 acceptance is not fresh authorization.

## 6. UC034 Complete

- New dedicated completion action/DTO/service partial after separate approval, task detail completion form reuses frozen items/evidence. Actual completer persisted as immutable transition fact in approved sidecar/event/schema; not inferred from latest assignee/interval close.
- Same own CLEANING_EXECUTE/resource checks, item/note/evidence policy; close interval while IN_PROGRESS ownership valid, then task PENDING_INSPECTION/end time, derived readiness event, audit and mandatory supervisor intent. Recipient routing exact approved branch supervisor relationship; no broadcast/Department.ManagerUserId guess.
- Gates: exact terminal/current-cycle/completer/inspection-pending contract, A1+room/publisher/source-read, new SQL scope. Tests: missing required items, wrong cycle, stale version, no interval/foreign interval, intent/audit/event fault rollback, readable supervisor inbox, no Ready.

## 7. FE05 inspection handoff

- Reuse current AssignInspectionAsync/planning/checklist/version/execution where approved; INSPECTION_PERFORM is existing assign permission. New queue/source-read may be needed, not solved by copying prototype. Existing Finish only PENDING_CONCLUSION.
- Approve/reject and rework/reinspection contracts separately pin exact current task+inspection/rework version, SoD and final completer. Source/task/readiness history/audit/notification atomic, no old PASS→Ready. No module broad refactor or restore missing conclusion writers from assumed history.
- Gates: approved current inspection identity and conclusions, recipient facts/read policy+delivery, coordinated room/block/reception transactions, new SQL scope. Tests stale/historical pass, SoD/foreign inspector, concurrent block/pass/check-in if later activated, rework linkage and final readiness. Full check-in/occupancy is not claimed by cleaning tests.
