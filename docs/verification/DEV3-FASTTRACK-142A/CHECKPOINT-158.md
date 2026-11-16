# FASTTRACK-142A checkpoint158 — UC132 actor and drill-down acceptance

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=86da3f11d3af5f0ee8cd42edc5e7d50b2d132215
NEW_COMPLETE_VERIFIED=1 (UC132)
TOTAL_COMPLETE_VERIFIED=27/144

Applied the approved UC132 decision: task dashboard metrics/distributions require Operations Manager or Owner with a matching DASHBOARD_VIEW grant path in each selected branch. Owner can read the existing task-only population endpoint with independently valid TASK_VIEW. Owner does not gain UC113 employee aggregates, time logs or workdays. Department Head retains UC113. No production grants are created or expanded.

Dashboard effort links now use EFFORT_IN_PERIOD task population instead of employee workload. UTC period, branch and department are retained; no employee interval or workday DTO is returned. Added visible MSG29 plus missing-source list, satisfying the incomplete-data alternative. [Acceptance ledger](UC132_ACCEPTANCE.md) distinguishes current actor/navigation evidence from historical unchanged KPI05 computation evidence.

Trial1 failed because the browser still expected the former employee workload route/table after selecting effort. Updated it to assert the approved task-only response, one matching task and absence of employee table; no guard or timeout was loosened. Trial2 PASS184 records. Final [trial3 PASS185 records](checkpoint158-runtime.json) additionally verifies visible MSG29/KPI05 source reason and independent Owner task detail authorization. Includes actual OM and Owner browser navigation across two branches/500-day UTC period, missing grants, foreign branch, revoked assignment, Department Head denial of UC132 with preserved UC113 access, Owner employee/time-log/workday403 and task detail200. Existing KPI04/06 synthetic SQL reconciliation, missing-data/N/A and checklist workflow regression also ran.

KPI05 cycle formula source is unchanged. C153 runtime PASS337 is historical acceptance for five accepted cycles across two Closed tasks, Return/resubmit, multiple Reopens, Pause/Resume, pending exclusion and zero denominator. It was inspected for this acceptance review, not rerun or reported as new execution.

Validation: solution build PASS0 warnings/errors; solution test exits0 without separate-project discovery; explicit backend regression824 PASS/14 SKIP; focused frontend11 PASS; TypeScript and production build PASS. ModelProbe has three existing EF1002 warnings; Vite use-client/large-chunk warnings remain. npm ci and full frontend suite not rerun.

All three clone cleanup receipts report DROPPED_AND_BACKUP_REMOVED. Independent master query returned no exact clone names, all backups absent and ports7142/7143 not listening. No shared/production SQL mutation, schema change or generated EF edit. Current actor fixture grants exist only in destroyed clones.

Changed product files: DashboardService.cs, new TaskDashboardAccess.cs, OperationalTaskWorkloadService.cs, DashboardPage.tsx, dashboardWorkloadLink.ts and workload.ts. Verification changes: TaskDashboardAccessTests.cs, dashboard-workload-link.test.mjs, SprintDashboardScopeScenario.cs, SprintTaskDashboardScenario.cs, sprintTaskDashboardBrowser.mjs; receipt, acceptance ledger, runtime and matrix.

Next priorities: FE13 completion/handover contracts and remaining UC006/010 responsibility/writer acceptance. Those UCs remain PARTIAL; completing UC132 does not complete the sprint or other dashboard modules.
