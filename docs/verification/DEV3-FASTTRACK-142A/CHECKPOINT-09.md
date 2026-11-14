# FASTTRACK-142A — checkpoint 09: scoped dashboard filters

Baseline `ff36542370962b208ce963176847a006a0f2e317`, branch `integration/dev1-dev2-dev3-unblock-20261114`. Previous checkpoint is verified progress; sprint remains ACTIVE.

Implemented SRS UC131–134 filter behavior: replace arbitrary branch/department ID fields with dropdowns sourced from existing `/api/dashboards/options`. Department choices follow the selected branch; changing branch clears department. No new permissions or backend schema. API continues to enforce authorization independently of UI options.

`DashboardFilters.tsx` separates filter loading/draft from dashboard snapshot. Every response is bound to the current authority; unmount/replacement aborts it. Options remain disabled until a valid response arrives. Failure has explicit retry and no automatic request replay. A current 401 expires the matching session. Validator rejects invalid/duplicate IDs and departments outside returned branches. Dates use UTC calendar inputs and bound from/to. Scope names disappear on authority replacement. The snapshot still displays the filters that generated its data, independently of pending draft choices.

Changed source: `frontend/src/insights/{DashboardFilters.tsx,DashboardPage.tsx,dashboardApi.ts}` and `frontend/tests/shared-auth-host.test.mjs`. Added browser coverage for branch/department choices, clearing department on branch change, foreign option rejection, explicit recovery, authority replacement and options401. Existing dashboard tests now account for the independent options GET. Browser tests use mocked HTTP; checkpoint08 real HTTPS/SQL options evidence is historical, not a new E2E claim.

Validation: TypeScript PASS (executed directly); production build PASS (`checkpoint09-build-final.log.txt`), retains existing use-client and >500kB warnings. No dependency/lockfile/backend edits, so npm ci and backend tests not rerun. Initial browser run interrupted while locating dropdown label; isolated run identified ambiguous label text including option names. Added explicit accessible names Branch/Department. Kept initial log and bounded browser locator timeouts to make future failures actionable. Final focused suite: 140 PASS, 0 fail/cancel/skip (`checkpoint09-router-final.log.txt`).

UC131–134 remain PARTIAL: confirmed KPI source integration, inventory/PM positive SQL fixtures, drill-down and real browser-to-SQL E2E remain. Full matrix remains 144 PARTIAL, 0 COMPLETE_VERIFIED, 0 SOURCE_READY, 0 BLOCKED. No SQL mutation or live API process started in this checkpoint.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=09_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e
SOURCE_IMPLEMENTED=SCOPED_DASHBOARD_DROPDOWNS_AND_AUTHORITY_BOUND_OPTIONS
TESTS_EXECUTED=140_AUTH_ROUTER_PASS
BUILD_STATUS=TYPECHECK_AND_FRONTEND_PASS;BACKEND_UNCHANGED
FE05_STATUS=PARTIAL_UNCHANGED
UC006_UC010_STATUS=PARTIAL_UNCHANGED
FE11_STATUS=PARTIAL
FE13_STATUS=PARTIAL
FE14_STATUS=DASHBOARD_SCOPED_FILTERS_IMPLEMENTED;KPI_AND_NOTIFICATION_GAPS_REMAIN
FE15_STATUS=AUDIT_READ_PARTIAL;REPORTS_PENDING
ISOLATED_SQL_STATUS=NO_MUTATION_THIS_CHECKPOINT
HTTPS_API_STATUS=NOT_RERUN;CHECKPOINT08_EVIDENCE_HISTORICAL
E2E_VERIFIED_WORKFLOWS=NONE_NEW;BROWSER_TESTS_MOCK_HTTP
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_PUBLIC_LIFECYCLE_COMPOSITION;FE11_13_WORKFLOWS;MISSING_KPI_SOURCE_CONTRACTS;REAL_BROWSER_SQL_E2E
NEXT_IMPLEMENTATION=Positive inventory/PM dashboard SQL fixtures; browser-to-SQL acceptance; confirmed FE11/FE13 workflows

CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS are provided after delivery to avoid self-reference.
