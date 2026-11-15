# FASTTRACK-142A checkpoint 33 — FE05 public approval

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=9908882f9bbde6c7464de1c362c3bb85a5c521ae
NEWLY_COMPLETED_UCS=0
UC_COMPLETE_VERIFIED=1 (UC110, historical checkpoint31 evidence)
UC_PARTIAL=143

## Business implementation and direct evidence

UC044 now has POST /api/branches/{branchId}/inspections/{id}/approve and canonical UI action. It retains protected session identity, INSPECTION_PERFORM branch permission, assigned inspector, separation of duties, mandatory criteria, current cleaning cycle, source evidence validation, CSRF and strong rowversion. Initial/final session and permission checks occur inside the existing SERIALIZABLE commit boundary. Uncertain commit is never retried automatically.

Approval changes inspection to Passed and cleaning task to Completed, calculates current effective readiness with retained room/block locks, and stores before/after effective and physical states in the same inspection audit. Only effective READY projects physical READY and writes room_status_history. Effective BLOCKED preserves the physical code and emits MSG17. No generated entities, migrations, CHECK constraints or persistent database schema were changed. Audit JSON is effective-state history for this event; it does not yet constitute a general block-release history timeline.

After successful business commit, delivery uses the durable audit key in a separate transaction and revalidates original-cleaner attribution/access. Delivery failure leaves a pending event rather than replaying approval. No automatic background retry worker was enabled. Missing attribution is explicitly returned, never substituted with the inspector or current assignee.

- [Browser → HTTPS → SQL](checkpoint33-approval-browser.json): both unblocked and blocked canonical UI approvals PASS; each has seven browser check records. Normal path returns PASSED/READY/MSG08; blocked path returns PASSED/BLOCKED/MSG17. Both deliver one notification to the original cleaner. CSRF missing=400, ETag missing=428, stale=412, duplicate=412; denied attempts preserve revision. Reload shows persisted state/readiness. SQL independently verifies task, inspection, physical status/history, one approval audit and original-recipient delivery. Setup/cleanup compatibility rehearsed with rollback before synthetic commits; final cleanup verified.
- [SQL approval component](checkpoint33-approve-sql.json): 26 scenarios PASS, including blocked approval, mandatory failure, session/permission denial, wrong branch, cleaner conflict, stale version, late audit/history fault rollback, recipient restrictions, durable notification fault/retry and concurrent delivery. These are actual executions this checkpoint; component notification concurrency is not concurrent room-block coverage.
- [Independent cleanup](checkpoint33-cleanup.json): owned database empty of synthetic users, branches, inspections, cleaning tasks, room history, blocks, incidents, audits, notifications and sessions. Attribution/fault trial objects removed. Shared HotelOpsDB untouched. Owned HTTPS host stopped after tests.

## Validation

- [Final solution build](checkpoint33-build-final.txt): PASS, zero warnings/errors. [Probe build](checkpoint33-browser-probe-build.txt): PASS.
- [Typecheck](checkpoint33-typecheck.txt) and [frontend production build](checkpoint33-frontend-build.txt): PASS. Existing Vite use-client/chunk-size warnings remain. Dependency lock unchanged; npm ci not repeated.
- [Backend regression](checkpoint33-backend-tests.txt): 502 PASS, 11 SKIP. [Domain](checkpoint33-inspection-domain.txt): 45 PASS; [loader](checkpoint33-inspection-loader.txt): 64 PASS. These two are in-memory tests, not SQL proof.
- [Inspection/AuthRouter/shared-host regression](checkpoint33-frontend-tests.txt): 179 PASS. [Final inspection client checks](checkpoint33-inspection-client-tests.txt): 19 PASS, including three new approval response/transport checks; these overlap the preceding suite and must not be summed as unique coverage.
- [Required solution test](checkpoint33-solution-test.txt): command exits successfully but the solution itself has no test project; not counted as executed tests.
- Runtime retains existing EF unmapped-index and Take-without-OrderBy warnings. Node bundled-test import.meta warning remains. No failed checks in this checkpoint's completed runs.

## Remaining lifecycle/runtime gates

Newly COMPLETE_VERIFIED UCs: **0**. UC044 has two complete demonstrated approval paths, but stays PARTIAL pending concurrent room-block changes through HTTPS, approval race acceptance and automatic durable notification retry coverage. Full FE05 also requires Reject → Request Rework → Reinspection, plus block-removal history and consumer coverage. Multiple effective blocks and removal were tested in checkpoint32 SQL only; not newly claimed as HTTPS proof. The disposable multiple-block index remains a rollback-only trial. Missing original-cleaner attribution in a target database remains a delivery dependency.

FE13 read authorization remains unresolved; no invented permission or weakened guard. UC006/010 coordination and remaining FE11 lifecycle are unchanged. FE15 actor restriction and UC110 pause contract remain as implemented previously; not counted as new work here.

Next highest-impact work: (1) concurrent block/approval verification and durable notification retry; (2) FE05 Reject/Rework/Reinspection composition using existing schema states; (3) FE11 lifecycle and UC006/010 coordination. No broad checkpoint audit was repeated.

## Changed source files

- Backend: Controllers/HousekeepingWorkflowController.cs; DTOs/Housekeeping/InspectionWorkflowDtos.cs; Services/Housekeeping/InspectionApprovalService.cs (new), InspectionApproveDomainWriter.cs, InspectionExecutionService.cs, InspectionConclusionPreparation.cs.
- Frontend: features/inspectionExecution/InspectionWorkspace.tsx, api.ts, model.ts; tests/fe05-inspection-execution.test.mjs; scripts/sprintApprovalBrowser.mjs (new).
- Verification: ModelProbe/Program.cs, SprintApproveProbe.cs, SprintNotificationCommitProbe.cs, SprintApprovalBrowserProbe.cs (new); UC_STATUS_MATRIX.md and checkpoint33 evidence.

Commit/push SHA and final remote verification are reported in the delivery message; no force push or main/develop merge.
