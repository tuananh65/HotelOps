# FASTTRACK-142A checkpoint151 — full-scope workload drill-down

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=a556b27289df1b116ef39cc9a3e838759e1a3477
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
SHARED_DB_MUTATION=NO

Continued the existing UC132 workflow. Dashboard links now preserve all selected branches and the full UTC period. The canonical task page renders a Workload section for each branch, with independent authorization, snapshot and pagination. It does not combine separate reads into a claimed single snapshot. Invalid/duplicate/ambiguous branch IDs do not auto-load a different scope. The task workflow distribution now has its correct label.

WorkTimePeriod.ForWorkload handles the Dashboard's ordered UTC period without silently truncating it to366 days. Existing Create validation and hotel-calendar bounds remain unchanged for other work-time consumers; page-size and integer-overflow guards, TASK_VIEW actor/scope checks and SHIFT_VIEW checks remain. Only Workload uses the new factory. Dashboard department filtering echoes the department's actual branch intersection after permission evaluation, so a department is not sent to unrelated branch endpoints. No generated entity/schema/dependency or product permission-catalog change.

Direct validation: solution build PASS, zero warnings/errors; backend regression **812 PASS/14 SKIP**; frontend focused link/Workload/task-read tests **17 PASS**; typecheck and production build PASS. Solution test exit0 discovers no separate regression project; the812 count is from the explicit project run. No npm ci/full frontend/Auth suite rerun; C150 Auth/Router158 PASS is historical evidence, not a new C151 execution. Vite use-client/large-chunk warnings and the ModelProbe's3 existing EF1002 warnings remain.

[Final runtime](checkpoint151-runtime.json): **133 records PASS**, browser → trusted HTTPS → disposable SQL. A synthetic Operations Manager has two authorized branches with2+1 Closed tasks. A500-day UTC period includes closures older than366 days; the browser clicks the real dashboard link and displays both populations with unchanged dates. Revoking the second branch role makes its stale-URL read403 and removes its table while the authorized first branch stays readable. Department filters return only the actual authorized branch. Employee workload over500 days includes3000 closed seconds, whereas the shorter prior UTC period includes2400; local-calendar500-day queries still reject400. This is aggregate/navigation acceptance, not new task-producer lifecycle acceptance.

[Trial history](checkpoint151-trials.json): trial1 completed the new scope/browser assertions but a later existing Cleaning fixture failed because it expected exactly two branches. Moved the extra branch fixture to the end of the existing scenario, keeping assertions intact. Trial2 PASS131; added the positive long-period effort assertion and unchanged local-calendar guard, trial3 PASS133. [Final cleanup](checkpoint151-cleanup.json) independently checks all3 databases absent, all3 backups absent and HTTPS listeners7142/7143 absent. Shared/production DB untouched.

Files: WorkTimePeriod.cs, OperationalTaskWorkloadService.cs, DashboardService.cs; dashboardWorkloadLink.ts, DashboardPage.tsx, TaskReadPage.tsx, TaskWorkloadPanel.tsx; TaskWorkloadContractTests.cs, dashboard-workload-link.test.mjs; SprintDashboardScopeScenario.cs, SprintTaskDashboardScenario.cs, SprintLinkedTaskCreationScenario.cs, sprintDashboardScopeBrowser.mjs; UC matrix, decision note and evidence.

**Newly completed UCs:0.** UC132 remains PARTIAL on actor compatibility and full acceptance. Exact unresolved conflict: SRS UC132 lists Operations Manager/Owner; UC113 and WorkloadAccess list Department Head/Operations Manager. Owner with DASHBOARD_VIEW+TASK_VIEW is still denied Workload unless another eligible role grants access. Generic dashboard permission checks also do not currently impose a UC132-specific actor restriction. An explicit decision was requested; no grant expansion, role ID assumption or security bypass was made while waiting.

Next highest-impact implementation: apply the UC132/UC113 actor decision consistently with same-role grants and branch scope, then test Owner/Operations Manager/Department Head/revocation. Independent next work: multiple Closed task denominator and Return/resubmit runtime coverage for KPI-05, then continue FE13 lifecycle acceptance. Sprint remains active; no broad audit restart and no whole-sprint completion claim.
