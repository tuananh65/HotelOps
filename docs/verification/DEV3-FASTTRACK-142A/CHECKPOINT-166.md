# FASTTRACK-142A checkpoint166 - RPT06 actual workday time

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=d79a3233775b8f557ddeff39eafddc5ce6b74cd6
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

Implemented workday_time_by_employee from persisted actual StartedAt/EndedAt under SRS UC139/GBR140 and RPT06. Employee/branch aggregates keep closedWorkdaySeconds, provisionalWorkdaySeconds and openWorkdays separate from task effort and planned shift duration. Includes actual unscheduled workdays; no raw employee logs or workday details are exposed. No UC113 access expansion. Existing UC135 OM/Owner plus REPORT_VIEW admission, branch/department scope, Serializable snapshot, limits, CSRF and export audit remain unchanged.

ReportWorkdayTime validates coherent department/branch, known WORKING/ENDED state and timestamp pairing, nonfuture dates, scheduled-assignment identity when present and absence of overlapping workdays per employee in the selected population. It bounds validation to50000 rows and clips actual duration to the UTC period. Invalid/unavailable workdays omit the section with an explicit reason; independent valid task effort still appears.

Direct evidence:
- Product solution build PASS0 warnings/errors; ModelProbe PASS with3 pre-existing EF1002 warnings in unchanged SprintCredentialScenario.
- Solution test exit0; backend regression829 PASS/15 runtime SKIP. Frontend report tests24 PASS; browser script syntax valid.
- Existing guarded report SQL probe70 checks PASS, rollback=true, including scope/snapshot/combined-row limits.
- Owned-clone browser/HTTPS/SQL trial PASS203 records. Workday closed1200 seconds/provisional>=7200/open count1 remains distinct from task effort2400 seconds. Prior period receives1200 crossing-workday seconds; future period has no projected open duration. Overlap withholds workday aggregation while valid task effort remains. Future recorded timestamps fail closed. Existing task-effort, Dashboard and Owner access regression also runs.
- Browser verifies workday section, column and numeric split, then downloads the same snapshot as CSV/XLSX with persisted export audits. XLSX is container-checked; desktop rendering is not claimed. Runtime fixtures exercise unscheduled workdays; scheduled-assignment validation is implemented but this checkpoint does not certify a separate scheduled-workday producer workflow.
- Clone1cec6a63 dropped and backup removed; independent master query0 matching databases, backup absent,0 HTTPS listeners. No shared/production mutation, schema or generated entity changes. No frontend product edit or fresh full frontend build/typecheck claim.
- git diff --check PASS with LF/CRLF notices.

Evidence: checkpoint166-reports.json, checkpoint166-runtime.json, checkpoint166-cleanup.json. Generic runtime scope text belongs to the containing harness; taskDashboardCheck/reportEmployeeEffortBrowser entries identify report assertions. cleanup=false precedes the separate verified clone drop.

UC135 remains PARTIAL for specialized filters, remaining report families and PDF; no whole-UC promotion. UC006/010 release gates unchanged. Next: resolve employee filtering population, complete other approved report indicators/export formats and remaining FE13 lifecycle contracts. Sprint active.

Changed source/test files: ReportWorkdayTime.cs(new), ReportSnapshotQueries.cs, SprintTaskDashboardScenario.cs, frontend/scripts/sprintReportsBrowser.mjs. Documentation: receipt,3 evidence files and UC matrix.