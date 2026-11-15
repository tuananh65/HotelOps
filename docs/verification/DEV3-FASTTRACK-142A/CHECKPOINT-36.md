# FASTTRACK-142A checkpoint36 - Reject to Request Rework

TASK_ID=DEV3-FASTTRACK-142A
INITIAL_REMOTE_SHA=3df0a384b1d44b8aba134b7fb17d74e09d85bdfa
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEWLY_COMPLETED_UCS=0
UC_COMPLETE_VERIFIED=2 (UC044, UC110; prior evidence)
UC_PARTIAL=142

## Implemented

Canonical FAILED inspection now automatically opens Request Cleaning Rework. Defects are preselected; authorized backend choices show the eligible official original cleaner first. No manual user IDs or guessed current assignee. The form collects assignee, selected defects, due time and existing mandatory reason. Fresh CSRF, inspection ETag, request cancellation, no automatic mutation retry, and explicit recovery after uncertain writes are preserved. Open rework is displayed with MSG03 and cannot be submitted again through the form.

New GET /api/branches/{branchId}/inspections/{id}/rework-options uses existing BatchAsync session/permission/branch locks and returns a DTO plus ETag. Eligibility was extracted as a shared query with identical predicates used for the writer; no role, branch, department, shift or account restriction was removed. Official cleaner attribution is required for ordering. The existing creation endpoint now also rejects stale cleaning cycles and defects without matching FAIL results from the assigned checklist version.

The form reports actual notification status NOT_INTEGRATED. It does not claim the full workflow or display a fabricated delivered receipt.

## Direct evidence

- checkpoint36-rework-browser.json: normal and blocked rooms, real Edge -> trusted HTTPS -> disposable SQL. Reject, auto-open form, preselected defect, authoritative original-cleaner option, Save201/PENDING_ACCEPTANCE, SQL assignee/defect links/audit, existing rework recovery and cleanup PASS.
- checkpoint36-rework-security-final.json: both variants PASS. Missing CSRF400, missing ETag428, stale412, empty/foreign defects400, ineligible assignee400, past due400; no rework created by rejected commands; duplicate API409. Original checkpoint36-rework-security.json is retained: test driver incorrectly sent Reject body to Rework endpoint; fixed driver sends the intended payload. This was a verifier defect, not a source failure or skipped guard.
- checkpoint36-build.txt: dotnet build HotelOps.slnx PASS, zero warnings/errors. checkpoint36-solution-test.txt: successful command without test projects in solution; not coverage.
- checkpoint36-regression.txt: 507 PASS, 11 SKIP. checkpoint36-inspection-tests.txt:24 PASS, including authoritative options scope/uniqueness, selected-ID validation, CSRF/ETag and uncertain-response no replay.
- checkpoint36-typecheck.txt and checkpoint36-frontend-build.txt PASS; existing use-client/bundle-size warnings remain. Runtime EF mapping/ordering warnings remain.
- checkpoint36-cleanup.txt: independent identity GUID comparison and zero users/reworks/links/departments/shifts/templates/assignments/audits/notifications/recipients. API stopped. No shared database writes or schema changes.

- checkpoint36-auth-host.txt: 138 PASS, 0 FAIL (shared auth host plus inspection client; includes the24 client tests, not additive).

## Remaining business completion

UC045/046 remain PARTIAL. Assignee notification is not integrated. ReworkLifecycleService and ReworkAuthorityReader require committed FE05_COMMITTED_V1 proof, issuer/module/receipt/audit integrity, separate committed transaction and source/evidence digests. This existing creation writer does not issue that proof; execution is still fail-closed. Do not substitute persisted FAIL alone, synthetic proofs, or disable the authority checks. Next work must connect the real writer to this contract and then prove Accept -> Start -> Complete -> Reinspection.

A specific business question is pending: GBR-046 names Active + HK_STAFF + correct branch/Housekeeping department, while existing eligibility additionally requires a currently active PLANNED shift. Current guard is retained pending the user's decision; unrelated implementation can continue. Source notification navigation and full Request Rework rollback/concurrency/permission acceptance also remain open. No new UC COMPLETE claim.

Next three coding tasks: committed rework authority issuance; assignee notification with read authorization/retry; executor and reinspection lifecycle integration. FE11/13 and UC006/010 remain in the broader sprint, not abandoned.

## Changed files

Application: HousekeepingAssignmentsService.cs, new InspectionReworkOptionsService.cs, InspectionWorkflowDtos.cs, HousekeepingWorkflowController.cs; inspectionExecution/api.ts, InspectionWorkspace.tsx and new RequestReworkForm.tsx.
Verification: fe05-inspection-execution.test.mjs, sprintRejectionBrowser.mjs; ModelProbe Program.cs/SprintApprovalBrowserProbe.cs and new SprintReworkFixture.cs; UC matrix and checkpoint36 artifacts. Commit/remote identity reported after Git safety checks.
