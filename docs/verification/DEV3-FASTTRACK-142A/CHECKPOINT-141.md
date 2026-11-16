# FASTTRACK-142A checkpoint141 — inspection report related rework

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=118846f7c273bf63306644bf203a8e467f592d50
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

RPT-02 now includes a cleaning_reworks section in canonical preview and frozen CSV/XLSX. Rows are selected through the already authorized/date/inspector/official-housekeeper-filtered inspection query, with matching cleaning task and room required. The section preserves assignee, recorded status, requested time and nullable completion time; no reason/note content or new permission is exposed. Related reworks follow the selected inspection population, not an independent rework-date filter. The report explicitly distinguishes storedStatus from authoritative workflow state. All sections share the 50,000-row refusal limit and snapshot transaction. No schema/generated entity changes.

Requirements basis: SRS V.3.5 RPT-02 includes rework; actual CleaningRework mappings and existing Reject transaction provide storage semantics. Raw related records do not establish first-pass KPI or cycle/round aggregates. Those remain explicitly unavailable; this is not a whole-UC completion claim.

Direct evidence:
- Backend build PASS, zero warnings/errors. ModelProbe build PASS with three existing credential-fixture EF1002 warnings.
- Solution test succeeds without discovering the separate regression project. Explicit report-filtered regression: 39 PASS, 1 SKIP (the name filter also matches a skipped cleaning Report lifecycle test).
- SQL query/CSV probe: 61 checks PASS, rollback=true. New checks prove linkage to selected inspection/original cleaner, inclusion of related rework requested outside the inspection period, preservation of null completion and raw status, exclusion when the parent inspection is filtered out, and immutable old versus new CSV snapshot.
- Initial SQL attempt failed because fixture set FAIL without physical COMPLETED; fixed to match the existing Reject writer and actual CHECK constraint. Constraint was never weakened. Failed attempt rolled back.
- Real Edge -> trusted HTTPS -> verified disposable SQL: success=true, eight named reportActorsCheck entries plus browser PASS. Browser verifies two total rows, one rework row with correct assignee/null completion/PENDING, visible cleaning_reworks heading and PENDING cell, CSV rework contents, XLSX download, filter edits and logout. Server confirms old cached CSV excludes subsequently created rework, persisted browser export audits, role revocation denial and incomplete attribution refusal.
- Both successful HTTPS runs used independent clones; both clones and backups removed. Final runtime cleanup=false delegates to its separate cleanup receipt (DROPPED_AND_BACKUP_REMOVED). No shared/production mutation. The generic runtime scope label belongs to its surrounding authority harness; named report evidence identifies this read/export subflow.

Changed files: ReportSnapshotQueries.cs; SprintReportProbe.cs; SprintReportActorsScenario.cs; sprintReportsBrowser.mjs; receipt, status matrix, SQL and runtime/cleanup evidence.

New COMPLETE_VERIFIED UCs: 0, total remains 24/144. UC135 remains PARTIAL for full report aggregates, remaining filter/indicator acceptance and PDF. This closes related-rework preview/export functionality; it does not prove the Reject/Rework/Reinspection producer workflow. Next highest-impact work: first-pass/cycle and rework-round aggregation based on authoritative cycle receipts; remaining approved UC135 indicators/export; UC122 reroute after its pending lifecycle decision. Sprint remains active.
