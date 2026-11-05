# Task85 — six producer source inventory

Evidence scope: current working tree vs historical snapshot
`cf8fb020362f8f64192e7f2f14a10547cebe5b91` (**S** below). Snapshot files are absent from
current runtime unless explicitly marked current. Exact numbered extracts and Git blob IDs:
HISTORICAL_SOURCE_LINES.txt; current hashes/lines: CURRENT_SOURCE_LINES.txt.
No DB row inspection, restore, source edit or API acceptance executed in Task85.

## Actual writers and signatures

All S assignment methods belong to **HotelOps.Api.Services.HotelStructure.RoomTypeCreationService**,
not a class named HousekeepingAssignmentsService. S file
`src/HotelOps.Api/Services/Housekeeping/HousekeepingAssignmentsService.cs` (**A**):

| UC / status | Actual method / signature / line | Actual controller / route / source-read |
|---|---|---|
| UC029 Assign Cleaning Task — PARTIAL historical; writer MISSING current | A:32 Task<RoomTypeBatchResult> AssignCleaningAsync(SessionReference ticket,long sid,long branch,long task,string tag,AssignCleaningRequest input,CancellationToken ct) | S Controllers/HousekeepingWorkflowController.cs:29–31 AssignCleaning, POST `/api/branches/{branchId}/cleaning-tasks/{id}/assign`; current read GET `/api/branches/{branchId}/cleaning-monitor/cleaning/{id}` |
| UC037 Monitor Cleaning Tasks — PARTIAL current | current Services/Housekeeping/CleaningMonitorService.cs:22 Task<CleaningMonitorResult> ListAsync(SessionReference ticket,long sessionId,long branchId,CleaningMonitorQuery input,CancellationToken ct); :65/:69 DetailAsync(ticket,sessionId,branchId,string kind,long id,[CleaningMonitorTimelineQuery input,]ct); **no Cancel writer** | current Controllers/CleaningMonitorController.cs:16–23 List/Detail GET `/api/branches/{branchId}/cleaning-monitor`, `/{kind}/{id}`; kind cleaning/rework |
| UC041 Assign Room Inspection — PARTIAL historical; writer MISSING current | A:56 Task<RoomTypeBatchResult> AssignInspectionAsync(SessionReference ticket,long sid,long branch,long task,AssignInspectionRequest input,CancellationToken ct) | S same controller:32–34 AssignInspection POST `/api/branches/{branchId}/cleaning-tasks/{id}/inspections`; S :19–20 Read GET `/api/branches/{branchId}/inspections/{id}/execution`, **not integrated currently** |
| UC044 Approve Room Inspection — MISSING / NOT_IMPLEMENTED | no Approve writer found in current or inspected S source | no Approve route found; S execution GET is not approval/readable live conclusion endpoint |
| UC045 Reject Room Inspection — MISSING / NOT_IMPLEMENTED | no Reject writer found in current or inspected S source | no Reject route found; recording defect is not rejection |
| UC046 Request Cleaning Rework — PARTIAL historical; writer MISSING current | A:90 Task<RoomTypeBatchResult> RequestCleaningReworkAsync(SessionReference ticket,long sid,long branch,long inspection,string tag,RequestReworkRequest input,CancellationToken ct) | S same controller:35–37 Rework POST `/api/branches/{branchId}/inspections/{id}/reworks`; current read GET `/api/branches/{branchId}/cleaning-monitor/rework/{id}` |

Read endpoint existence does not guarantee recipient access: current Monitor uses CLEANING_VIEW
branch/global SQL grants. OWN is not promoted; an assigned HK_STAFF may lack branch read rights.
Do not ship a notification action to that GET without real recipient-permission acceptance.
No new readable inspection endpoint or permission is invented in this handoff.

## Transaction / audit / authorization boundaries

S `Services/HotelStructure/RoomTypeBatchService.cs` (**B**): BatchAsync:22 receives operation,
additionalUsers,eligibility,permissionCode; :30–32 rejects dirty/current/ambient transaction
and begins Serializable. :35 sorted actor+target UserEligibilityLock; :36 Gate SQL session and
permission; :37 eligibility; :40 branch UPDLOCK/HOLDLOCK; :45 transaction app lock
`HotelOps:RoomTypes:v1`; schema gate; :48 executes writer; :49–52 final gate/eligibility;
:53 **single CommitAsync**. Non-success/throw rollback via transaction disposal, no explicit
RollbackAsync. :60 ChangeTracker.Clear does not commit. Owning context is RoomTypeCreationService's
injected HotelOpsDbContext, reused by all partials; no independent participant DbContext.
B:193–201 BatchAuditAsync inserts ActorUserId/BranchId/EntityType/Id/before/after/ChangedFields,
TimeProvider UTC timestamp, then SaveChanges:199. Audit failure prevents outer commit.
Current Workday's SQL clock must not be falsely attributed to these historical writer timestamps.

| Producer | Actual persisted identity/state/audit point | Permission, responsibility and missing integration |
|---|---|---|
| UC029 A:36–53 | branch resolved task→active Room; expected rowversion; CleaningAssignment.AssigneeUserId/AssignedBy/AssignedAt/ACTIVE; SaveChanges:48; raw workflow update ASSIGNED/legacy PENDING:49; audit:51 | CLEANING_ASSIGN:53; EligibleHousekeeper A:13–19 requires active user/employment/HK department+branch/current branch access/HK_STAFF role/current assigned planned shift. Missing publisher/replay/retry and room lifecycle contract |
| UC037 current service:119–123 | Serializable read, Gate before/after, Commit read only; **zero SaveChanges/business audit/cancellation writer** | CLEANING_VIEW:135; branch lineage resolved in RowsAsync. Task82 timeline pagination implemented. No cancel→room recalculation→audit→assignee notification transaction to hook |
| UC041 A:60–87 | task→Room branch, submitted cleaning/rework, no existing open inspection; stores InspectorUserId and checklist VersionId; SaveChanges:82; workflow ASSIGNED/due:83; audit:85 | INSPECTION_PERFORM:87. EligibleInspector A:20–27 active account/employment/access/current branch HK_SUPERVISOR. A:71–74 excludes historical assignment/progress/rework performers. Missing assigned-inspector notification and lifecycle integration |
| UC044 | no writer, commit/audit integration point MISSING | Source SRS actor is inspector/supervisor, but actual approve permission binding, current-cycle completion identity, SoD, all criteria, effective blocks, Passed+Completed+room recalculation+audit+intent must be implemented/reviewed; don't infer from execution finish |
| UC045 | no writer, commit/audit integration point MISSING | Must atomically conclude Failed+ReworkRequired+room/effective block/audit/notification. Final completer and supervisor recipient resolution unresolved; no invented reject policy |
| UC046 A:95–114 | inspection→task→Room lineage; expected rowversion; FAILED inspection/REWORK_REQUIRED cleaning; rejects existing open rework; validated selected defect links; stores CleaningRework.AssigneeUserId; SaveChanges:108, workflow PENDING_ACCEPTANCE/legacy PENDING:109, links:110, audit:112 | INSPECTION_PERFORM:114; on-shift eligible housekeeper. Missing durable delivery/retry/replay and effective-block coordination |

Durable intent integration for restored A writers belongs **after authoritative writes and audit,
before B:53 outer commit**, using same context/transaction. This is a technical integration point,
not approval to restore source or change shared files. Runtime restoration/schema/EF/DI gaps remain.

S `Services/Housekeeping/InspectionExecutionService.cs:28` signature:
`Task<RoomTypeBatchResult> ExecuteInspectionAsync(SessionReference ticket,long sid,long branch,
long id,string? expected,string operation,SaveInspectionRequest? input,
RecordInspectionDefectRequest? defect,string? reason,string privateRoot,CancellationToken ct)`
(`SaveInspectionRequest?` parameter's exact identifier is `save`, not `input`). Only read/start/save/finish/defect accepted.
:90 finish sets PENDING_CONCLUSION/legacy IN_PROGRESS, not PASSED/FAILED; :112 audit,
:114 INSPECTION_PERFORM. Assigned InspectorUserId must equal actor. This is **not UC044/045**.

SQL state mapping evidence: S `scripts/database/release/housekeeping/01_Housekeeping.sql`:
CHECK constraints map inspection PASSED→legacy COMPLETED/PASS, FAILED→COMPLETED/FAIL;
rework PENDING_ACCEPTANCE→PENDING, IN_PROGRESS/PENDING_REINSPECTION→IN_PROGRESS.
DDL permitting a state is not proof a writer exists. HousekeepingSchema marker, workflow
columns/rowversions/links and common partials are not silently restored in current runtime.

## Persisted recipient / final completer evidence

- **Assigned inspector proven as a modeled persisted field**: current Entities/RoomInspection.cs:18
  InspectorUserId, S A:81–85 writer and assignment audit. Must read exact inspection key/current cycle,
  not latest cleaning assignment; actual stored row values NOT queried in Task85.
- CleaningTask has ActualEndAt (:26), CompletionNote (:30) and CreatedBy (:32), **no CompletedBy**.
  CleaningRework has AssigneeUserId (:14), CompletedAt (:22), **no final-completer field**.
  CleaningProgressLog.LoggedBy (:20) identifies progress author, not authoritative final completion.
  S WorkProgressService.WorkProgressAsync records IN_PROGRESS and progress audit, not final completion.
  Search of S housekeeping/room writers found no final completion writer mapping these timestamps
  to a durable final actor. **Actual final cleaning/rework completer identity MISSING/UNPROVEN**.
- WorkInterval.ClosedBy proves interval closer only; pause/hold also closes intervals. It is not
  final business completion. Latest assignment/current assignee/last progress actor cannot substitute.
  Need a reviewed authoritative completion event/actor/cycle mapping and atomic persistence before
  UC044/045 recipient delivery is accepted; no schema diff authorized by this inventory.
- EligibleInspector A:20–27 proves an eligibility predicate (active HK_SUPERVISOR branch role,
  account/employment/access/validity), **not a unique designated supervisor recipient**.
  No Department.ManagerUserId fallback, broad broadcast or latest-assignment inference.
  Exact supervisor-selection business decision must come from available approved joint decisions.
- Source SRS UC044 sends to housekeeper who cleaned; UC045 sends supervisor+housekeeper who cleaned;
  UC029/041/046 use actual persisted assignee/inspector; UC037 cancellation uses assignee if any.
  See SRS_SOURCE_LINES.txt. Runtime recipient IDs and delivery outcomes NOT_RUN in Task85.
