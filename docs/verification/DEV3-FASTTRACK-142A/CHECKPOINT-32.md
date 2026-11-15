# FASTTRACK-142A checkpoint32: approved readiness and report admission

Newly completed UCs: **0**. Totals remain **1 COMPLETE_VERIFIED (UC110), 143 PARTIAL**. This checkpoint moves approved business contracts into live consumers and report endpoints; it does not claim FE05 or UC135 completion.

## Implemented and verified

- EffectiveRoomReadiness calculates GBR-044 from live blocks and cleaning/inspection/rework data, not stored READY. Inactive wins, then any effective block. Unknown or unavailable lifecycle sources never authorize room use. Cancelled cycles are ignored; the latest non-cancelled task is selected by created_at/task_id. Ready additionally requires its latest inspection Passed. The caller retains SERIALIZABLE locks across reads.
- Room directory list/detail now expose effectiveReadiness and canUseRoom; list adds effective counts across the filtered scope. Room Status Board displays effective state separately from physical codes. Dashboard ready counts/ratio and Cleaning Monitor use the same calculator. Existing physical history and codes remain unchanged.
- Actual SQL revealed UX_room_blocks_active allows one unreleased block per room, conflicting with the approved multiple-block acceptance. A guarded isolated script trials uniqueness on room_id/incident_id, per GBR-075. It is executed INSIDE an outer transaction and rolled back with fixtures. Original index is restored; this is not a schema deployment.
- UC135 backend: options, POST preview, owner-bound immutable snapshot and POST CSV export are mounted. ReportAccess requires OPERATIONS_MANAGER/OWNER and REPORT_VIEW from the SAME eligible assignment, using persisted role codes and principal/session authorization, never fixed role IDs. It retains branch/department scope, rechecks current authorization, and does not alter global catalog grants or UC136/137 rules.
- Export reauthorizes the original snapshot branches, commits a REPORT_EXPORT/CREATE audit with operation=EXPORT and snapshot/filter metadata before returning bytes. Audit entries detach before the session service's mandatory clean-context gate. Existing CSV formula and decimal safety remain.

## Direct execution evidence

- [Final solution build](checkpoint32-build-delivery.txt): PASS, 0 warnings/errors. [Probe build](checkpoint32-report-build-delivery.txt): PASS. [Typecheck](checkpoint32-typecheck-final.txt) and [frontend production build](checkpoint32-frontend-build-final.txt): PASS. Vite use-client/chunk warnings remain. npm ci not repeated; no dependency/lock changes.
- [Final backend regression](checkpoint32-tests-delivery.txt): 502 PASS, 11 SKIP. [Room Status/AuthRouter/shared-host tests](checkpoint32-frontend-regression.txt): 174 PASS, 0 fail/skip. [dotnet test solution](checkpoint32-solution-test.txt) discovers no test project, not counted as test coverage.
- [Actual SQL readiness](checkpoint32-readiness-delivery.json): 8 checks PASS, rollback=true. Covers never-passed priority, completed+Passed, two effective blocks, expected-release not equivalent to release, removal of one block, inactive priority, removal of last block, failed cycle with unsubmitted rework, and unchanged physical status. Eight check records combine some related assertions. No HTTP, browser, or concurrent writer acceptance for readiness yet.
- [Report HTTPS/SQL](checkpoint32-report-delivery.json): 24 records, success=true, cleanup=true. Catalog DEPT_MANAGER really has REPORT_VIEW yet options/preview/export are denied; OWNER+REPORT_VIEW succeeds alongside that denied role. Missing CSRF is rejected. One scoped task is snapshotted, then its stored progress changes; CSV exactly matches the earlier server preview. Export audit persists; revoking OWNER blocks cached export and creates no successful export audit. No report browser acceptance or complete all-role/branch matrix yet.
- [Independent cleanup](checkpoint32-cleanup.json): users/branches/tasks/inspections/cleaningTasks/blocks/incidents/audits all zero. Original block index restored, trial index absent. Owned HTTPS API stopped. Shared DB untouched.

## Failures retained and fixed

Initial backend compile used an incorrect ticket namespace; corrected to the existing Security namespace. Initial readiness fixture referred to a nonexistent requested_by column; corrected against CleaningRework and DbContext. Initial report export used unsupported audit action EXPORT; the unchanged schema supports CREATE, now explicitly creating a REPORT_EXPORT event. Next export failed the SQL session clean-context guard due to tracked audit state; the owned saved audit entry is now detached before final authorization. The CSV assertion also initially checked Title, which is not a report column; final assertion compares the full preview-derived CSV after changing a real exported progress field. All earlier failure artifacts remain. Runtime EF unmapped-index and Take-without-OrderBy warnings remain.

## Remaining workflow gates

FE05 room mapping is APPROVED, not a pending business decision. Public Approve/Reject/Reinspection composition, effective transition history, MSG17/notification, block-release writer integration and concurrent-block HTTPS/browser tests remain. room_status_history still accepts physical codes only; room_readiness_events still accepts only PENDING_CLEANING. Do not write new readiness labels into either CHECK-constrained location. A narrow effective-history storage/composition change needs implementation and isolated verification; permission to alter disposable schema does not make it complete.

Reports still need canonical UI, UI visibility policy, pagination and complete SRS report indicators/formats. API success does not close UC135. All room-usability consumers must be reviewed as conclusion/release is integrated; the four changed consumers do not prove system-wide coverage. FE13 read authorization and UC006/010 writer coordination remain.

Next highest-impact implementation: FE05 public conclusion/reinspection with effective transition history and notification; then concurrent block changes and browser E2E. Finish canonical report preview/export UI using the now-tested admission API, without introducing extra components unrelated to these workflows.

Changed files: EffectiveRoomReadiness.cs; RoomDirectoryService.cs; RoomDirectoryContracts.cs; DashboardService.cs; CleaningMonitorService.cs; frontend rooms.ts and RoomStatusBoardScreen.tsx; ReportAccess.cs; ReportService.cs; ReportsController.cs; Program.cs registrations; ReportSnapshotQueries.cs comment; EffectiveRoomReadinessTests.cs; ReportAccessTests.cs; ModelProbe Program/SprintAuthProbe/SprintReadinessProbe/SprintReportApiScenario; RoomBlockPairIsolated.sql; decisions, matrix and checkpoint32 evidence.

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=dbbdfdd07b7e99bbd3a66ae2feb0176d76013214
NEWLY_COMPLETED_UCS=0
UC_COMPLETE_VERIFIED=1
UC_PARTIAL=143
SQL_HTTPS_STATUS=READINESS_SQL_PASS;REPORT_HTTPS_SQL_PASS;ROLLBACK_AND_CLEANUP_VERIFIED
NEXT_THREE_CODING_TASKS=FE05_CONCLUSION_EFFECTIVE_HISTORY;FE05_BLOCK_CONCURRENCY_E2E;REPORT_CANONICAL_UI
COMMIT_SHAS=delivery commit containing this receipt
PUSH_STATUS=reported after remote verification
