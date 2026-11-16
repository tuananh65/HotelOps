# FASTTRACK-142A checkpoint164 - RPT06 task status/overdue summary

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=35adc34652df4be580a3c491b3929a9542913fdf
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

Implemented task_status_summary for SRS RPT06 over the exact authorized branch/department and created-in-period detail population. SQL groups canonical workflowState and nullable snapshot-time overdue with LongCount. Unknown remains null, not known non-overdue. The section uses existing SERIALIZABLE snapshots, combined50000-row cap, generic preview and frozen CSV/XLSX. Actor/grant/scope/session/CSRF/audit unchanged. Population and remaining effort/shift limitations are explicit.

Direct evidence:
- Product build PASS0 warnings/errors; ModelProbe PASS with3 pre-existing EF1002 warnings in unchanged SprintCredentialScenario.
- Solution test exit0; separate report-filtered backend tests39 PASS/1 runtime-name-match SKIP.
- Frontend report tests24 PASS. Initial invocation from repository root failed module resolution; rerun from required frontend directory passed.
- Final guarded SQL70 checks PASS, rollback=true: unknown and canonical In Progress/Paused overdue buckets, detail reconciliation, branch/department/UTC filters, inconsistent lifecycle, frozen CSV, empty scope, combined detail+summary cap. First probe failed an obsolete detail-only total assertion after59 checks and rolled back; fixture assertions corrected without relaxing product cap.
- Real Edge/HTTPS/disposable SQL PASS: summary heading/count column, detail reconciliation, CSV summary columns, XLSX download, persisted export audit for each format and browser logout. Existing inspection actor filtering/revoked-role denial also passed. XLSX checked as ZIP container, not desktop Office rendering.
- Clone359f1289 cleanup receipt DROPPED_AND_BACKUP_REMOVED. Independent master query:0 databases; backup absent;0 HTTPS listeners. Component fixture rolled back. No shared/production writes.
- git diff --check PASS with LF/CRLF notices. No frontend product edit; existing production bundle renders generic report sections. No fresh full frontend build/typecheck claim.

Evidence: checkpoint164-reports.json, checkpoint164-runtime.json, checkpoint164-cleanup.json. Runtime cleanup=false means immutable fixtures remained until separate whole-clone cleanup.

UC135 stays PARTIAL for other indicators, task effort/shift, specialized filters and PDF. UC006/010 fuses unchanged. This verifies the status-summary preview/export path, not the whole reporting UC. Next: approved effort attribution, remaining FE13 lifecycle contracts, UC006/010 complete writer/session acceptance.

Source/test files: ReportSnapshotQueries.cs; SprintReportProbe.cs; SprintReportApiScenario.cs; SprintReportActorsScenario.cs; frontend/scripts/sprintReportsBrowser.mjs. Documentation: receipt, three evidence files and UC matrix. Sprint active.