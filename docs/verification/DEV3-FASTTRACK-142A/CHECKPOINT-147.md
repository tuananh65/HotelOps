# FASTTRACK-142A checkpoint147 — task lifecycle dashboard integration

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=7c6d72c079bf1bfb1f80c84f490c3c58dadee56a
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
SHARED_DB_MUTATION=NO

UC132 now consumes the existing TaskWorkflowRead lifecycle under the dashboard's existing scoped SERIALIZABLE snapshot. It provides canonical state/priority distribution, open tasks, overdue tasks and tasks closed in the selected UTC period. Paused and Pending Verification remain open; Closed/Cancelled do not count as overdue. Null/future deadlines do not count overdue. Closed-period count uses CompletedAt, not CreatedAt. Existing physical-state distribution remains explicitly labeled separately. Missing capability or any missing/inconsistent scoped lifecycle withholds totals as null, never fabricated zero; UNKNOWN distribution and missing-source explanation preserve visibility. Department/room/area branch incoherence is treated as unknown. No new lifecycle authority, schema or grants are introduced by product code.

Dashboard UI now distinguishes the selected period (stock movements and closures) from snapshot-time open/overdue figures. No public task mutation is introduced. No authorization release gate is relaxed. Remaining UC132 work includes completion duration/cycle attribution, closed interval effort, drill-down and whole actor/scope acceptance; this is not whole-UC completion.

Direct validation: required solution build PASS, zero warnings/errors; explicit backend regression 798 PASS / 14 SKIP, zero failures. Solution test command succeeds but does not discover the separate regression project. ModelProbe build PASS with three pre-existing EF1002 credential-fixture warnings. Frontend TypeScript and production build PASS; existing React Router use-client and large-chunk warnings remain. No dependency installation/change or full frontend suite rerun in this batch.

Final trial4: 105 records PASS, including 10 new named dashboard checks plus a browser result with three assertions. Authenticated Operations Manager scoped branch/department filters show open=4, overdue=2, closedInPeriod=1; Paused, Pending Verification and Closed display separately. SQL fixtures include earlier-created tasks closed in period, earlier-period closure, Cancelled, null/future due dates and legacy missing lifecycle. Legacy rows produce null totals; foreign branch returns403. Dashboard reads leave source tasks and business audit untouched. The lifecycle rows are synthetic aggregation fixtures, not evidence that this batch reran every producing lifecycle. Existing public linked task, cleaning creation, deadline and handler regressions also pass. No new average/effort assertion is claimed.

Failed trials retained: trial1 and trial2 reached the dashboard assertions but later collided with the pre-existing helper that changes all actor roles to HK_STAFF; the new temporary Operations Manager assignment caused a unique-key collision. Final fixture deletes only its own added assignment before returning (or restores pre-existing activity). Trial3 passed dashboard/browser assertions but failed the final no-active-session assertion because the new browser had not logged out; explicit public Logout fixed it. Trial4 passes the unchanged final security assertions. All four clones/backups removed by identity-guarded cleanup. No failed trial relabeled PASS. Runtime cleanup=false delegates immutable-fixture removal to whole-clone disposal; cleanup receipts prove removal.

Changed product files: TaskDashboardQueries.cs, DashboardService.cs, DashboardPage.tsx. Verification: SprintTaskDashboardScenario.cs, SprintLinkedTaskCreationScenario.cs, sprintTaskDashboardBrowser.mjs; matrix and this receipt/evidence. Final runtime and all cleanup receipts accompany this checkpoint.

Next highest-impact work: finish UC132 duration/effort and authorized drill-down from existing lifecycle sources where semantics are explicit; resume UC105 requester provenance when the pending legacy-source contract arrives; resume UC102 recurrence and FE13 reroute/material completion on their existing decisions. UC102/105 were inspected first in this batch and remain constrained by those contracts. Sprint goal remains active; deadline unchanged.
