# Source review / provenance

Baseline FS005680f69026644ee3b6201f695f0768de871e2bb0a verified by local HEAD + git ls-remote, clean DEV1 worktree. No worktree switch, merge/pull, schema installer or archive integration.

Reviewed:
- docs/requirements/HotelOps_SRS_DRAFT.md FE04 UC028–038, state matrix near16287; SRS_CHANGE_NOTES.md pending role/room approvals.
- docs/fullstack-integration/UC_LEDGER_SUMMARY.md; official historical rows read via git show28f0a5e...:scripts/verification/HANDOVER_CODEX_A_20261105/FULL_UC_LEDGER.csv. UC028 BLOCKED_EXTERNAL, UC029 PARTIAL, UC030/031/034 NOT_IMPLEMENTED, UC032 COMPLETE_VERIFIED_V1. No new points or ledger edits.
- Codex A FINAL_HANDOVER_REPORT.md and notification/room-workday shared contracts at immutable28f0a5e4f582bc56ba101b48915d33baadeb7b95; current Codex B HANDOVER.md declares no new production implementation/UC107 authorization. No B source imported.
- docs/fs002 README/mapping/routes, fs003 UC032 contract/README, fs004 README/validation/HTTPS/security/trial helper and fs005 README/audit/write-readiness/live evidence. Historical inactive-route statements are not treated as current: monitor's two GETs became approved active in FS005; assignment/planning routes are currently active.
- Controllers HousekeepingPlanning/HousekeepingWorkflow/WorkProgress/CleaningMonitor; DTOs HousekeepingPlanning/InspectionWorkflow/Progress/CleaningMonitor; Services HousekeepingAssignments/HousekeepingPlanning/InspectionExecution/WorkProgress and shared RoomTypeBatchService (read only).
- HotelOpsDbContext cleaning task/assignment/progress, Room, ShiftAssignment and Notification/Recipient mappings; generated entities and optional housekeeping schema scripts inspected only. No migrations/entities/DbContext changes.
- Existing Cookie/SQL Session/PermissionService, CSRF middleware, source permission resolver; IWorkTimeTransactionParticipant + actual SQL participant; frontend create/assignment/monitor/progress and getJson API client.
- Task73 conditional approval and Task74/FS004 progress acceptance, current RBAC catalog CLEANING_ASSIGN/CLEANING_EXECUTE/CLEANING_VIEW. Catalog text is not approval for an unimplemented action or new resource binding.

Conflicts kept explicit: (1) SRS rich workflow vs legacy coarse PENDING/IN_PROGRESS/COMPLETED/CANCELLED and nullable opt-in workflow; (2) generated entities do not encode all optional metadata; (3) planning candidates/raw shifts are broader than eligible-now writer checks; (4) mandatory notification/readable own ASSIGNED source absent despite existing assign core; (5) SRS desired room recalculation/current cycle/effective blocks lacks approved shared writer protocol; (6) old handover runtime descriptions differ from current imported active code. No silent mapping or role grant adjustment.

Shared scope: no Program.cs/DI/config/auth/RBAC, Workday/Hotel Structure production service, Notification or SQL schema edits. Existing assignment endpoint is not disabled or repaired by this task; it remains a documented inherited partial write path and is not newly consumed by React. UI block is not a server authorization measure.

SOURCE_MANIFEST.json lists exact changed application/test source hashes. Git diff/commit provides full reviewable change; build/test/live evidence resides only under docs/fs006. No protected archive contents, credentials, private SQL fixture, logs, trace/video/screenshots or binaries published.
