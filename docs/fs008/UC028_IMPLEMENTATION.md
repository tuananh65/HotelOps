# UC028 implementation — VERIFIED_LOCAL

Active new routes in CleaningTaskLifecycleController:
- GET /api/branches/{branchId}/cleaning-task-options?roomId=... → CleaningCreateOptions: authoritative room/branch, active CLEANING_TYPE/PRIORITY references and template item count.
- POST /api/branches/{branchId}/cleaning-tasks → CreateCleaningTaskRequest(RoomId,CleaningTypeId,PriorityId,DueAt,Instruction) →201 CreatedCleaningTask + task rowversion ETag.
- GET /api/branches/{branchId}/cleaning-tasks/{id}?assignmentId=... → minimal AssignedCleaningHeader for independent source-read preparation; not a substitute for supervisor monitor.

Source: DTOs/Housekeeping/CleaningTaskCreationDtos.cs, Services/Housekeeping/CleaningTaskCreationService.cs (existing RoomTypeCreationService partial), Controllers/CleaningTaskLifecycleController.cs. No generated EF entity or DbContext/Program registration edit. Raw unmapped parameterized SQL handles additive lifecycle metadata.

Authorization/transaction reuse existing BatchAsync: actual Cookie/SQL session, active account/employment eligibility locks, CLEANING_ASSIGN branch SQL grants, retained active branch lock, catalog app lock, Room UPDLOCK/HOLDLOCK and task/pointer range reads, final gates and one outer Serializable commit. DTO rejects unknown server-owned fields; positive IDs, future UTC due, instruction trimmed≤4000/no control chars. Room/floor/area/type active and branch-compatible; type/priority active correct reference groups. Require1–100 template cleaning definitions, preview only. Frozen snapshot creation remains first Start (not implemented here), preserving UC032.

Create inserts legacy PENDING + workflow PENDING_ASSIGNMENT, GUID-backed unique task number≤40, explicit current-cycle pointer and immutable-by-application pending-cleaning event with effective-block snapshot + same-transaction audit. No Room.OperationalStatus/occupancy/RoomBlock or legacy room_status_history changes. No mandatory Notification producer for Create alone per SRS. Missing/incompatible lifecycle/progress/housekeeping schema fails closed503, missing/foreign hierarchy400/403/404/409 according actual gates.

Duplicate prevention: conservative nonterminal-or-null workflow query under retained room/range locks; pending inspection/rework remains open. Unknown legacy history without pointer fails closed, no automatic adoption. Unique filtered SQL room index for coarse status not COMPLETED/CANCELLED protects competing active inserts, with existing trusted Task73 workflow/coarse CHECK mapping. Composite FK task+room protects cycle/event lineage. This does not turn all legacy completion into proved current Ready.

Frontend CreateCleaningTaskScreen reuses header/panel with real room options, active ref selects, UTC deadline/instruction, fresh CSRF and cookie transport. Successful201 navigates to live monitor detail and appears in server-backed list. After error/ambiguous result, submit remains disabled until explicit config reload and Monitor check; no automatic unsafe retry or mock fallback. Monitor has Create navigation; branch/room IDs are untrusted filters checked on server.

Evidence: real browser→trusted HTTPS→API→isolated SQL verifies task/pointer/event/audit consistency and unchanged room legacy; duplicate409; concurrent pair201+409 with no deadlock retry; audit failure after inserted task/event rolls back all. See LIVE_RESULTS.json. No production release or all downstream lifecycle acceptance claimed.
