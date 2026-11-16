# FASTTRACK-142A checkpoint149 — dashboard to workload with explicit calendar

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=82271afb13454ca4458a016b0c9320084e5f2bfe
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
SHARED_DB_MUTATION=NO

UC132 task indicators now offer Review workload for a single selected branch and a supported period. The local link retains department, exact dates, UTC calendar, and applicable overdue/Closed filter. It never chooses an arbitrary branch from an aggregate or truncates a period beyond Workload's existing 366-day bound. The canonical Tasks route preloads Workload filters and initiates its independently authorized read. Unsupported/malformed deep-link inputs do not auto-load a different filter set.

Workload API explicitly accepts UTC or Asia/Ho_Chi_Minh and returns its calendar on employee list/detail responses. Default remains hotel-local, including existing callers of WorkTimePeriod. UTC callers must receive UTC in the frontend response contract; missing/different calendar is rejected. No existing TASK_VIEW, actor, branch, session or SHIFT_VIEW check is removed. Dashboard permission alone does not grant Workload access. No schema or generated entity change.

Direct validation: solution build PASS zero warnings/errors; explicit backend regression799 PASS/14 SKIP. Solution test command succeeds without discovering the separate project. New calendar unit test proves UTC boundary clipping and unchanged hotel default; unknown calendars reject. Frontend9 focused tests PASS covering scope/date links, ambiguous branch/invalid dates/oversized period refusal, response calendar confirmation, existing workload safety. Typecheck/production build PASS. ModelProbe build retains3 existing EF1002 warnings; Vite retains use-client/large-chunk warnings. No dependency change or full frontend suite rerun.

Trial1 PASS on first execution; final runtime contains dashboard browser proof, UTC/local HTTP reads, invalid calendar400 and revoked TASK_VIEW403 while DASHBOARD_VIEW still reads200. Browser clicks actual effort link, lands in canonical Workload with the selected department/from/to and UTC, and displays2400 closed seconds, matching dashboard40 minutes. Identical date strings with omitted calendar return the existing local-period3000 seconds: the crossing interval is included in full under hotel boundaries. Backend calendar tests and previous C148 period evidence support the boundary difference. Scope/grant changes in the probe are synthetic fixtures only. Clone and backup removed by the existing identity-guarded cleanup; runtime cleanup=false delegates immutable-source removal to whole-clone disposal.

Changed product files: WorkTimePeriod.cs, TaskWorkloadDtos.cs, OperationalTaskWorkloadService.cs, DashboardPage.tsx, dashboardWorkloadLink.ts, TaskReadPage.tsx, TaskWorkloadPanel.tsx, workload.ts. Verification: TaskWorkloadContractTests.cs, dashboard-workload-link.test.mjs, operational-task-workload.test.mjs, sprintTaskDashboardBrowser.mjs, SprintTaskDashboardScenario.cs; matrix and receipt/evidence.

New complete UCs:0. UC132 remains PARTIAL for KPI-05 semantics and full acceptance, including actor/scope and count reconciliation: Workload groups by employees and historical accepted completions, whereas dashboard open totals include unassigned tasks and closed counts reflect current Closed state. Review workload links preserve navigation context; they do not assert identical counting populations. Whole-UC completion is not inferred from the effort match. Next highest-impact work: reconcile unassigned/current-closure drill-down populations and finish KPI-05 on the pending explicit actual-end/cycle decision; resume FE13/UC105 on their pending contracts. Sprint remains active; deadline unchanged.
