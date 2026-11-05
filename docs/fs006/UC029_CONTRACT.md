# UC029 contract — existing core / BLOCKED_CONTRACT for writes

| Dimension | Current source contract |
| --- | --- |
| Endpoint / active | HousekeepingPlanningController GET assignment-options; HousekeepingWorkflowController POST assign, both active before FS006 |
| Routes | GET /api/branches/{branchId:long}/cleaning-tasks/{taskId:long}/assignment-options; POST /api/branches/{branchId:long}/cleaning-tasks/{id:long}/assign |
| DTO | PlanningQuery(Search,From,To,Page,PageSize) → HousekeepingPlanningDto; AssignCleaningRequest(AssigneeId,DueAt,Reason,AdoptUnassignedLegacy=false) → HousekeepingWriteDto(Id,State,Notification=NOT_INTEGRATED) + ETag |
| Permission / ownership | CLEANING_ASSIGN branch gate and actual SQL session/actor; eligible target is not caller-supplied authority |
| Scope | Task.Room in requested branch; active room; actor branch/access and target branch/HK department/role/shift validated in source |
| State | PENDING_ASSIGNMENT→ASSIGNED, legacy status mirror PENDING. Explicit AdoptUnassignedLegacy only permits null workflow + legacy PENDING; no implicit backfill. FS006 draft always false |
| Concurrency | Existing strong quoted uppercase HEX8-byte task row_version; WorkflowTag; stale412, malformed400, missing header428. Atomic ACTIVE-assignment check; no HTTP replay/idempotency contract |
| CSRF | Existing middleware validates X-CSRF-TOKEN on unsafe API before action; malformed/missing token may produce400 before authorization |
| Transaction | BatchAsync: clean scoped SQL context, Serializable, sorted user eligibility locks incl actor/target, gate+eligibility, active branch retained lock, catalog app lock, schema guard, task/assignment mutations+audit, final gate+eligibility, one outer commit |
| Notification | SRS GBR010/UC124 mandatory to persisted new assignee. No approved production publisher/intent/replay integration found; response truthfully says NOT_INTEGRATED |
| Room effects | Initial Assign source writes task workflow/legacy status/due and assignment; no room recalculation or Workday start. Never reuse as UC030 |
| SQL | users/user_roles/roles/user_branch_access/departments/shifts/templates/assignments, rooms/floors/areas, cleaning_tasks/cleaning_assignments, audit_logs; optional housekeeping schema |
| Frontend | /cleaning/assignment?branchId=...&taskId=... reads planning, displays candidates/workload/raw shifts and checks local DTO draft; Save disabled |
| Missing implementation | Mandatory notification intent/delivery/retry + explicit-recipient readable source; no approved idempotency integration. Writer already exists; FS006 does not certify or extend it |
| Approval readiness | GET safe independent slice; POST integration BLOCKED_CONTRACT. No new production write path or no-op publisher |
| Live verification | Planning GET only; new mutation acceptance LIVE_WRITE_BLOCKED_APPROVAL |

Source: Services/Housekeeping/HousekeepingAssignmentsService.cs, HousekeepingPlanningService.cs; Services/HotelStructure/RoomTypeBatchService.cs; DTOs/Housekeeping/{HousekeepingPlanningDtos,InspectionWorkflowDtos}. No backend production edits in FS006.

GET planning status contract:200 DTO,400 invalid UTC window/pagination/search or HTTPS,401 SQL session,403 permission/scope,404 task,409 inactive hierarchy,503 schema/unavailable. Default window is server UTC today→tomorrow, explicit from/to≤2 days. Candidates may have no recorded shifts; writer's eligible-now predicate is stricter. Raw EF DateTime values may omit zone; UI preserves text and never seeds a deadline from them. CapturedAt/effective query dates carry server UTC.

POST existing controller:200 DTO+new ETag;400 invalid references/validation/CSRF;401/403;404;409 Inactive/Duplicate;412 Stale;428 missing If-Match;503 missing schema/dependency. Assignment DTO currently does not disallow unknown JSON fields; client draft maps only the four known fields. Do not claim hardened unknown-member rejection or delivery success. No request retry: missing response/ambiguous commit needs future durable operation resolution, not blind POST.

Reason: trim, required,≤500 UTF16 characters, no control characters; DueAt future UTC; positive IDs. No reassignment reason10+ rule imported from prototype. Draft rejects invalid syntactic fields and non-PENDING_ASSIGNMENT/unverified snapshots; it does not grant permission or prove live shift eligibility. No ETag-consuming POST adapter is shipped. Fresh CSRF, exact GET ETag, final SQL eligibility, post-success refresh/navigation and notification behavior remain write-integration requirements.
