# UC030 / UC031 / UC034 readiness

All three: no active HTTP action/route or request/response DTO for these cleaning operations in current source. Do not infer routes from screen labels/CLEANING_EXECUTE, inspection Start or UC032 progress. No new writer, SQL mutation or shared approval in FS006.

| Dimension | UC030 Reassign | UC031 Start | UC034 Complete |
| --- | --- | --- | --- |
| Permission / owner | Supervisor; catalog CLEANING_ASSIGN suggests action family, exact binding pending | Assigned staff; existing CLEANING_EXECUTE family, own-source gate required | Assigned staff; own-source execution gate required |
| Branch | Resolve task→room plus both assignees, retained branch eligibility | Task→room/branch + actual current owner | Same source/owner + exact current cycle |
| State | SRS ASSIGNED/IN_PROGRESS/ON_HOLD→ASSIGNED | SRS ASSIGNED/ON_HOLD→IN_PROGRESS | SRS IN_PROGRESS→PENDING_INSPECTION |
| Concurrency | Source + old/new responsibility + interval revisions/lock set, not implemented | Source/assignment/Workday/open-interval locks+revision, not implemented | Source/items/evidence/interval/current cycle revisions, not implemented |
| CSRF | Fresh token / exact If-Match contract required for future action | Same | Same |
| Transaction | End old interval, preserve history, new assignment+audit+room+mandatory intents atomically | First start timestamp, open interval, source+room+audit atomically | Required items/note/evidence, actual end, interval close, room/history+audit+mandatory intent atomically |
| Notification | Old and new assignees per SRS | SRS UC031 main flow has no mandatory GBR010 event; do not invent one | Authoritative supervisor for inspection assignment; routing unresolved |
| Room | Recalculate from current cycle unless Blocked | Cleaning In Progress unless Blocked | Pending Inspection unless Blocked |
| SQL | cleaning_tasks/assignments, work_intervals/workdays, room history/audit, durable notification records | Same time/source/room families | Plus frozen task items/private evidence and future inspection-cycle handoff |
| Frontend | Existing initial assignment UI explicitly cannot reassign | Existing progress panel is not Start action | Percent100 is not Complete; no fake completion control |
| Missing | Reassign DTO/writer, time/history/routing/readable notification contracts | Start DTO/writer, Workday orchestration + current-cycle/block protocol | Complete DTO/writer, exact terminal/current-cycle/inspection recipient and lifecycle integration |
| Readiness | BLOCKED_CONTRACT | BLOCKED_CONTRACT | BLOCKED_CONTRACT |

A1 IWorkTimeTransactionParticipant exists and is scoped; header REVIEW PROPOSAL is not enough to infer every integration is approved. Actual SQL participant requires caller-owned same DbContext transaction, actor/owners sorted/time locks before location/source locks, authoritative permission/owner/state reread, server time, Working own Workday and no second open interval. SourceAsync cleaning requires legacy IN_PROGRESS and exactly one actual ACTIVE own assignment. Close must occur while source passes that validation; a caller changing source state first can invalidate close. Exact Start ordering/source validation and rollback need reviewed integration, not status assignment alone.

Initial Assign's shift predicate does not prove a Working Workday. Scheduled workday foundation exists but unscheduled/Pause All and cross-owner shared contracts remain restricted. FS006 does not modify DEV3 Workday, IWorkTimeTransactionParticipant, source resolver or DI. Rework execution/conclusion remains separate and blocked; assignment/progress cannot substitute for it.
