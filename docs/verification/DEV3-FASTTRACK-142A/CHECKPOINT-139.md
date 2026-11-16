# FASTTRACK-142A checkpoint139 — inspection report actor filters

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=2397738f12b59407da0558ed3fe9e88472b9e6b6
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Implemented RPT02 housekeeper filtering using existing immutable cleaner attribution and its original assignment/completion provenance. Historical inspection attribution does not change when the cleaner account is subsequently deactivated. Inspector and housekeeper filters intersect the authorized inspection query. Missing capability or any missing attribution in the selected inspection population fails closed instead of silently dropping unknown rows. No generated entity, database schema, permission or security guard changed.

Canonical report UI now accepts housekeeper/inspector filters for inspections, clears the old snapshot on edits and removes these filters when switching report types. The client rejects a snapshot that does not confirm the requested actor filters. CSV/XLSX continue to use the frozen snapshot and existing export authorization/audit.

Direct evidence:
- Backend build PASS, zero warnings/errors. Initial sandbox attempt could not read local NuGet.Config; authorized rerun succeeded. Solution test exits successfully but does not discover the separate regression project.
- Explicit backend regression: 783 PASS, 14 SKIP (not runtime passes).
- Focused frontend report tests: 24 PASS; TypeScript and production build PASS. Existing router directive/chunk-size warnings remain.
- SQL component: 57 checks PASS, rollback=true. Tests cover missing capability, missing historical attribution, official cleaner inclusion, inspector exclusion, deactivated cleaner history, inspector intersection and frozen CSV filter metadata. This is query/CSV evidence, not public HTTP authorization or new filter E2E acceptance.
- Existing reports regression through real Edge -> HTTPS -> owned disposable SQL: success=true, cleanup=true (checkpoint139-reports-runtime.json). Covers denied DEPT_MANAGER despite REPORT_VIEW, allowed Owner/Operations Manager, CSRF, frozen CSV/XLSX, persisted export audits, role revocation, room effective readiness and request overdue snapshots. Does NOT exercise the new housekeeper/inspector controls.
- Previously running full frontend suite completed: 3444 PASS, 1 SKIP, 0 FAIL. This run predates C139 actor filters; the 24 focused tests cover the new frontend changes.

New whole UCs completed: 0. Total remains 24/144 COMPLETE_VERIFIED. UC135 remains PARTIAL: new actor-filter E2E, full RPT indicators and PDF acceptance still required. No shared/production SQL mutation; synthetic SQL component transaction rolled back and HTTPS fixture cleanup confirmed.

Changed product files: ReportsPage.tsx, reportApi.ts, CleanerAttributionSource.cs, InspectionHistoryCleaner.cs, ReportSnapshotQueries.cs. Verification files: reports-api.test.mjs, reports-browser.test.mjs, SprintReportProbe.cs; this receipt, status matrix and SQL/runtime evidence.

Next highest-impact tasks: finish UC122 reroute after the pending lifecycle decision; verify the inspection actor filters through real browser/HTTPS with an isolated attribution fixture; complete remaining UC135 indicators/export acceptance without weakening authorization. UC122 approved CreatedAt-to-DueAt timing is already implemented and tested in C131/C135; reroute remains separate and unresolved.
