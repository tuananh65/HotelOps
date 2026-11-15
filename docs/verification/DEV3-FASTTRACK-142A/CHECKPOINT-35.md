# FASTTRACK-142A checkpoint35 - Reject workflow and UC044 closure

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=2c74ce6e4f9eb50854dd5cba06a179e24f7e8c6f
NEWLY_COMPLETED_UCS=1 (UC044)
UC_COMPLETE_VERIFIED=2 (UC044 and historical UC110)
UC_PARTIAL=142

## Implemented business behavior

Public Reject now requires current assigned inspector, permission, current cleaning cycle, strong ETag, confirmed nonempty reason and matching failed criteria/defects. Existing separation-of-duty and evidence guards remain. Inspection FAILED/COMPLETED/FAIL, task REWORK_REQUIRED and audit/notification intent commit together through the existing SERIALIZABLE batch. Missing official cleaner attribution fails closed before mutation. Effective readiness is REWORK_REQUIRED even before a rework row exists; active blocks still override it. Physical room status is preserved, with effective history recorded in the audit, without changing CHECK values or generated entities.

The approved UC045 audience is staged in the Reject audit: all currently eligible active branch supervisors with INSPECTION_VIEW plus the official original cleaner. Delivery revalidates immutable cleaner proof, source, supervisor eligibility and read access. User locks precede audit/source locks; notification and recipient receipts are atomic and deduplicated per event. Delivery failures retain the committed business event. The existing opt-in worker discovers both passed and failed events with one cursor entry per audit, avoiding fanout pagination loss. The runtime verification invokes the actual delivery implementation on independent SQL contexts; it is not a new hosted-worker acceptance claim. Notifications have no unapproved source link; direct source access retains existing authorization. Source-open navigation is still pending for UC045.

Canonical UI has reason, explicit confirmation, failed-criterion/defect prerequisites, Reject and MSG17 for blocked rooms. Mandatory FAIL disables approval, shows MSG03 and offers the functional Reject action. Requests retain fresh CSRF, strong ETag, cancellation and no mutation replay on uncertain responses.

## UC044 acceptance mapping

- PRE-01/02 and normal steps1-8: checkpoint33 direct approval, source/security negative cases, atomic audit/history rollback and normal/blocked browser evidence; checkpoint34 durable hosted retry and permission recheck. These are historical supporting results, not newly executed counts.
- Blocked alternative: checkpoints33/34 verify Passed remains BLOCKED, MSG17, multiple blocks and concurrent insert/release, preserving physical status.
- Mandatory-FAIL exception: checkpoint35-rejection-browser-final.json directly verifies canonical MSG03, disabled Approve, confirmed Reject, FAILED and REWORK_REQUIRED/BLOCKED via browser -> trusted HTTPS -> disposable SQL.
- Therefore UC044 is COMPLETE_VERIFIED for this scoped isolated acceptance. This does not declare UC045/046/047 complete or authorize production deployment.

## Direct validation

- checkpoint35-build.txt: solution build PASS, zero warnings/errors.
- checkpoint35-regression.txt: 507 PASS, 11 SKIP. Skipped SQL/live tests are not counted as executed coverage.
- checkpoint35-solution-test.txt: command succeeds, no solution test project; not coverage.
- checkpoint35-typecheck.txt and checkpoint35-frontend-build-final.txt: PASS. Existing react-router use-client and >500kB bundle warnings remain.
- checkpoint35-frontend-tests.txt: 22 inspection client tests PASS.
- checkpoint35-auth-router.txt: 185 PASS, 0 FAIL (inspection + AuthRouter + shared host).
- checkpoint35-frontend-all-tests.txt: broad run explicitly interrupted to focus on relevant coverage; partial output is NOT a suite PASS.
- checkpoint35-rejection-browser-final.json: both normal and blocked scenarios PASS. CSRF400, missing ETag428, stale/duplicate412, persisted Reject and readiness, durable audit, delivery fault rollback, original-cleaner/supervisor dedup, revoked supervisor exclusion, access recovery, two-context concurrent retry and cleanup verified.
- Earlier checkpoint35-rejection-browser.json and retry.json retain failures: SQL metadata query exceeded the unchanged30s timeout (HTTP503), plus browser admission timeout. Independent cleanup returned zero users/inspections/notifications/audits. Additional security run timed out waiting for a response at15s; harness wait subsequently allows45s to observe the unchanged API timeout. No certificate, security or SQL timeout bypass.

- checkpoint35-rejection-security-retry.json: PASS both scenarios, including durable discovery/cursor receipt, inactive supervisor role, inactive account, revoked branch access, delivery fault rollback, concurrent retry and dedup. The previous security timeout remains recorded.

- checkpoint35-approval-regression.json: current final code also PASS normal/blocked Approve, MSG08/MSG17, original-cleaner delivery, CSRF/ETag/duplicate guards and persisted state reload.
- checkpoint35-cleanup.json: independent final zero synthetic business/notification/session rows; attribution/fault objects absent. Owned API stopped.

- Final build with restore initially hit sandbox access denial for local NuGet.Config (checkpoint35-build-final.txt); the approved elevated retry is checkpoint35-build-final-verified.txt. No config contents or secrets were printed.
- Cleanup JSON unexpectedly projected databaseGuid as all zeros. Direct sqlcmd read/comparison (checkpoint35-cleanup-identity.txt) confirms the expected GUID and identityMatches=1, with all queried counts zero. Original anomalous JSON retained; no mutation followed the identity anomaly.

## Remaining lifecycle gaps and next implementation

UC045 remains PARTIAL: open Request Rework with defects preselected, canonical source navigation and full negative/Reject rollback acceptance. UC046 needs authoritative eligible-housekeeper options, original-cleaner ordering, due time/defect selection UI and assignee notification. UC047 reinspection needs approved narrowly scoped handling of schema lifecycle differences (existing CHECK lacks ACCEPTED/FAILED). FE11 remaining lifecycle, FE13 read authorization and UC006/010 remain open; FE15 and UC110 prior approved decisions are unchanged.

Next three coding tasks: (1) UC045 -> UC046 complete authorized Request Rework handoff; (2) UC046/047 acceptance, execution and reinspection with transaction/concurrency tests; (3) highest-impact FE11 lifecycle gap, then FE13 contract resolution and UC006/010. No new peripheral component work.

## Delivery boundaries

All writes used the exact guarded disposable database HotelOps_DEV3_142A_20261114_cc78c13f on meita, GUID1D5A9B53-7D19-435A-96AB-9FDFF447B6E0 and owned run marker. Synthetic fixture cleanup is rehearsed under rollback before committed trial and executed in finally. No shared HotelOpsDB mutation, persistent schema change, production seed/deployment, generated EF edits or permission-catalog changes.

Changed application files: HousekeepingWorkflowController.cs; InspectionWorkflowDtos.cs; new InspectionRejectionService.cs; EffectiveRoomReadiness.cs; InspectionPassedNotificationDelivery.cs; new InspectionFailedNotificationDelivery.cs and InspectionFailureRecipients.cs; inspectionExecution/api.ts and InspectionWorkspace.tsx. Verification: readiness/client tests, rejection browser driver, ModelProbe rejection delivery/dispatcher/fixture extension, approved-decision notes, UC matrix and checkpoint35 artifacts. Final commit/push identity follows Git safety verification.
