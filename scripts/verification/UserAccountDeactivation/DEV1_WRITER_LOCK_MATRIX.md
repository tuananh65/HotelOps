# Task47 — DEV1 writer/lock matrix

Post-approval helper ApprovedDev1ResponsibilityQueries implements direct responsibility
reads only. Designated columns are cleaning_assignments.assignee_user_id,
cleaning_reworks.assignee_user_id, room_inspections.inspector_user_id,
inspection_defects.assigned_to, departments.manager_user_id. Existing writer absence
and users-first requirements in the table remain unchanged. Helper uses sequential
read-only async queries in caller transaction; no retained locks or atomic snapshot
guarantee is claimed. Multi-query results cannot authorize mutation without integrated
writers/resource locks and authoritative reread.

Index review: cleaning target/status index exists; rework assignee, inspection inspector,
defect assigned_to and department manager have no matching target-leading index in the
inspected EF declarations. Reads may scan; no index was added. Future serializable
checks may retain broad locks/escalate, so query-plan/lock footprint testing remains
required. No schema/index assumption replaces retained participant User locks.

| Responsibility | Existing responsible source | Lock compatibility / action |
|---|---|---|
| Cleaning assignment/rework | No business writer found in Controllers/Services; EfCleaningTaskAccessReader and CleaningTaskResourceResolver are readers | Future assign/reassign/start/reopen/complete writer must lock affected Users ascending, then task/assignment; re-read discovered assignees and restart if participant set changes. Reader/helper is not coordination. |
| Inspection/defect | Entity/mapping only; no assignment/conclusion/resolve writer found | Future inspector/defect writer: Users before inspection/defect; exact feature order requires implementation review. |
| Operational task | Entity/mapping only | Future assignment/reopen/review writers must retain old/new participant locks before task/assignment resources. |
| Internal request | Entity/mapping only | Future sender/receiver locks before request/handover resources; acknowledgment must atomically switch responsibility. No event-actor shortcut. |
| Shift/Workday | Entity/mapping only for shifts/attendance; no Workday entity | Scheduling/check-in/out/handover lock participant User before shift/assignment/attendance. Unscheduled Workday representation unresolved. |
| Department/line manager | No manager assignment/reactivation writer found | Future department manager/user-manager changes require affected Users before department/subordinate resources, with post-lock reread. |

Source search covered src/HotelOps.Api/Controllers, Services and Security; it does
not certify external jobs, imports or direct SQL absent. No business writer was added
simply to justify a deactivation predicate.

## Existing user/security writers

- GlobalAdminSerialization.AcquireAsync -> ReadSnapshotAsync: application lock first,
  then Users TABLOCKX,HOLDLOCK, then catalog ROWLOCK,HOLDLOCK. TABLOCKX is an intentional
  existing deviation from fine-grained Users ascending. Keep it before feature locks;
  do not first lock individual users then upgrade this universe lock.
- UserRoleAssignmentService.AssignAsync: global snapshot first -> sorted actor/target
  Users -> authority/branch/department locks; compatible with global-first design.
- UserAccountActivationService.ActivateAsync and UserBranchAssignmentService.AddAsync:
  sorted actor/target Users first -> authority locks -> reread. Neither acquires the
  global guard later, so no new reverse global acquisition is introduced here.
- SessionRevocationService.RevokeAllAsync: requires caller transaction and clean
  context, re-enters target User UPDLOCK,HOLDLOCK -> sessions. Future Deactivate must
  already retain User locks and detach audit entities before guard snapshot checks.
- Reset workflow uses existing global-first LastAdmin integration. No reset semantics changed.

New helper takes no feature locks then User locks; it cannot create a reverse lock path.
Future Deactivate design: global guard -> Users universe/sorted participants -> authority
and feature resources in agreed canonical order -> authoritative session/permissions,
rowversion and responsibilities reread -> account/version + revoke sessions + audit ->
clean tracker -> LastAdmin poststate -> commit. Any query failure/cancellation requires
caller rollback. No check-then-update safety is claimed from the helper.

Exact existing DEV2 reverse-risk paths remain MaterialRequestService.Locked/Participant,
MaintenanceWorkService.Commands.ExecuteCorrectiveAsync/AssignWorkAsync and
InventoryService.Masters.SaveWarehouse. Proposed repair is their DEV2 handoff,
not DEV1 edits: discover participants, retain Users ascending, lock resource, reread and
restart on participant drift. No DEV2 source or existing DEV1 infrastructure was changed.
