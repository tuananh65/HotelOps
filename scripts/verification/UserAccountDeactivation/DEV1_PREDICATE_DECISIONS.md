# Task47 — DEV1 predicate decisions

## Current technical approval and implementation (supersedes initial proposals below)

Developer explicitly approved rules 1/2/3 in the Task47 reply; this is technical
approval, not a replacement for PO/SRS sign-off. Implemented in
ApprovedDev1ResponsibilityQueries.ReadAsync, using the same caller DbContext/transaction.

| Predicate | APPROVED | IMPLEMENTED | UNRESOLVED |
|---|---|---|---|
| Cleaning assignment | ACTIVE + EndedAt NULL + nonterminal parent blocks; inconsistent/unknown data fails closed | PENDING/IN_PROGRESS parent blocks, including inactive room and cross-branch duties; left join retains missing parent as unresolved; contradictory end markers/unknown states produce explicit codes | ACTIVE with SQL parent COMPLETED remains CLEANING_TERMINAL_MAPPING; missing/unknown parent CLEANING_UNRESOLVED; CANCELLED parent + ACTIVE assignment inconsistent |
| Rework | Designated target, PENDING/IN_PROGRESS blocks independently of parent | assignee_user_id = target; no completed_at -> CLEANING_REWORK; completion marker on open state -> REWORK_DATA_INCONSISTENCY; completed history excluded with valid marker | Invalid/unknown statuses and conflicting/incomplete completion evidence fail closed; no inferred owner of unassigned rows |
| Inspection | Designated target, PENDING/IN_PROGRESS blocks | inspector_user_id = target -> INSPECTION_ASSIGNEE; open with result is inconsistent; COMPLETED with valid result is historical | Unknown status/invalid result unresolved; no extra responsibility inferred from authorship or completed inspection |
| Defect | OPEN/IN_PROGRESS with designated current target blocks | assigned_to = target is explicit owner while OPEN/IN_PROGRESS; resolved_at on open state is inconsistent; RESOLVED with marker is history | No owner inferred from parent inspector/checklist/creator; unknown status and missing resolution evidence unresolved |
| Department manager | Active managed department blocks; inactive department is not manager-specific blocker | is_active AND manager_user_id = target -> DEPARTMENT_MANAGER | Other duties checked separately; users.manager_user_id contract still unresolved |
| Operational Tasks | No new approval in reply | No predicate introduced | Lifecycle/verification/reopen mapping and current responsibility remain unresolved |
| Shift / Workday / Internal Request | Explicitly NOT approved in reply | Always retained in Unresolved list | Exact questions below still require PO contract; no invented mapping or empty gate |

The result has Blockers and Unresolved lists; it provides no CanDeactivate flag.
Unresolved always includes outstanding module contracts and writer coordination.
Queries read all target designated references and classify exact status strings ordinally;
they do not use room activity, primary branch, or creator identity to discard obligations.
No assignments, manager fields, schema or lifecycle records are modified by the helper.

SQL PENDING/IN_PROGRESS are provably unfinished for cleaning; SQL COMPLETED lacks the
approved mapping to SRS Pending Inspection/final completion. That ambiguity is retained,
not resolved by treating every COMPLETED parent as safe. Ended completed/reassigned/
cancelled assignment history does not establish current responsibility by itself.

Historical proposal table below records why the questions were asked. Its approved
rework/inspection/defect/department proposals are superseded by the status table above.

2026-10-29, branch feature/BE1-database-integration, HEAD 6278a8807bb72ff72c16f8173748ff48f5260794.
Use case inventory: docs/requirements/srs_sections/01_overall_requirements.md §4.2;
UC details: 02_use_case_specifications.md. No separately named Use Case Matrix found
under docs. The §4.4 Permission Matrix remains draft per SRS_CHANGE_NOTES.md.

## Resolved executable subset

`Dev1DeactivationPredicates.KnownExecutingCleaning` recognizes only ownership already
established by CleaningTaskResourceResolver: target assignee, assignment ACTIVE,
EndedAt NULL, AssignedAt <= UTC evaluation time, task IN_PROGRESS, active room with
positive BranchId, exactly one effective current assignment on that task.
No target primary-branch/actor-scope filter: duties in another branch remain evidence.
This is positive blocker evidence, NOT an exhaustive cleaning/DEV1 gate. A false result
cannot authorize deactivation, including inactive rooms or contradictory legacy rows.
No pending proposal below was promoted to approved business behavior.

## Exact decisions still requiring PO/developer approval

| Group / UC | Existing representation | Decision requested, not implemented |
|---|---|---|
| Cleaning UC029/030/031/034 | assignment ACTIVE/COMPLETED/REASSIGNED/CANCELLED; task PENDING/IN_PROGRESS/COMPLETED/CANCELLED | Approve ACTIVE + EndedAt NULL on nonterminal parent as blocker beyond proven execution? Specify future assignment and status/end-marker conflicts. Does SQL COMPLETED mean SRS Pending Inspection or final Completed? |
| Rework UC035/046/047 | assignee_user_id; PENDING/IN_PROGRESS/COMPLETED/CANCELLED; completed_at | Map SRS Pending Acceptance/Accepted/Pending Inspection/Reinspection to stored states. Approve PENDING/IN_PROGRESS independent of parent, with contradictory completed_at remaining unresolved? |
| Inspection UC041/042/044/045 | inspector_user_id; PENDING/IN_PROGRESS/COMPLETED/CANCELLED; separate result | Which stored state represents Pending Conclusion? Does COMPLETED release inspector before failed-inspection follow-up? Approve PENDING/IN_PROGRESS as blocker and explicit terminal release rule. |
| Defects | assigned_to; OPEN/IN_PROGRESS/RESOLVED/CANCELLED; resolved_at | Approve OPEN/IN_PROGRESS blocker independent of parent; define resolution acceptance and timestamp/status conflict policy. |
| Operational Tasks UC102/104/105/108/112 | ACTIVE assignment + ended_at; task TODO/IN_PROGRESS/BLOCKED/COMPLETED/CANCELLED | Map Pending Verification/Closed/reopen to stored values; approve TODO/IN_PROGRESS/BLOCKED assignment gate and state/end precedence. Does COMPLETED release executor while verification remains? |
| Internal Requests UC114-123, especially UC119 | requester_id; assignment status/ended_at; parent DRAFT/SUBMITTED/IN_PROGRESS/COMPLETED/CLOSED/REJECTED/CANCELLED | Which requester states require personal action? Which state releases current assignee? UC119 requires designated receiver acknowledgment before sender release; where is this authoritative acknowledgment stored? Events have no structured receiver/ack fields. |
| Shifts UC139/140 | shift assignment defaults ASSIGNED; shift defaults PLANNED; no status CHECK whitelist | Supply allowed states, timezone, inclusive/exclusive end boundary, overnight/cancelled/missed/checked-in rules. Past scheduled end alone cannot prove all duties ended. |
| Workday UC139 | shift_attendance requires shift_assignment_id; no Workday entity/table found | UC139 explicitly permits unscheduled Working workday. Approve an existing representation or a separate schema task; do not equate attendance with Workday by assumption. |
| Department/line manager UC024/025 | departments.manager_user_id/is_active; users.manager_user_id | Approve active managed department blocker; decide inactive department and active subordinate obligations. Scalar manager is not the revised branch-wide Department Head authorization role. |

Assignments/authors on terminal history are distinguishable structurally, but lifecycle
ambiguity prevents a universal historical-release predicate. Missing writers do not
exclude live legacy responsibilities. No schema or SRS conversion was invented.

## Isolation/index implications

The new helper uses caller DbContext and current transaction, AsNoTracking and async
AnyAsync with CancellationToken. It does not start/commit/rollback transactions or take
retained eligibility locks. READ COMMITTED alone is not race safety; RCSI may read versions.
Future mutation requires users-first coordinated writers plus authoritative resource reads.

Schema has cleaning_assignments_index_9 (assignee_user_id, assignment_status), index_8
(cleaning_task_id, assigned_at), and filtered UNIQUE UX_cleaning_assignments_active on
cleaning_task_id WHERE assignment_status='ACTIVE'. The EF mapping lacks that filtered
unique index declaration; SQL source does contain it. Resolver comment claiming no
unique constraint is stale relative to release SQL; no shared mapping/resolver changed.
Uniqueness does not validate EndedAt or ownership semantics. Correlated effective-count
check remains conservative for unverified legacy/index drift. No plan/lock footprint is
claimed from index definitions alone; live query-plan measurement remains outstanding.
