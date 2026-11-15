# FASTTRACK-142A checkpoint 59: initial inspection assignment through execution

New COMPLETE_VERIFIED UCs: **0**. Total remains **8 COMPLETE_VERIFIED / 136 PARTIAL**. This closes the initial-assignment normal flow and its real handoff into inspection execution. UC041 still needs reassignment acceptance; UC042/043 still need their remaining acceptance criteria. No whole-UC completion is inferred from a successful happy path.

## Implemented workflow

The canonical Room Status Board now exposes the inspection action after a scoped, authorized pending-cycle read. It opens Pending Inspections, then the exact cleaning cycle, published default checklist and eligible inspectors. The supervisor selects an inspector and future due time, reviews and confirms. The POST retains its route but now requires task If-Match, cycle ETag, exact checklist version and Idempotency-Key. The obsolete unversioned service mutation was removed after checking its sole controller caller. The old planning read endpoint remains; the new options endpoint is `/initial-inspection-options` to avoid a route collision.

The service checks the current actor and selected inspector, active branch/room hierarchy, current Pending Inspection cycle, one open inspection, published/effective checklist and existing cleaner separation rules. A registered writer saves Assigned inspection, audit and protected operation/notification receipt in one transaction. Repeating the same operation acknowledges the same historical receipt after current authorization; changing its request or using another key cannot duplicate the assignment. No new business table, lifecycle value, generated entity or EF migration was added. The trial uses the existing issuer registry definition and operation ledger; its writer, registry and restricted role are installed only on the exact disposable database and removed afterward. Missing runtime capability fails closed.

Durable INSPECTION_ASSIGNED delivery rechecks current eligible supervisor/INSPECTION_VIEW and current inspector identity, deduplicates concurrent attempts and rolls back notification creation if its recipient insert fails. Opening an inbox item verifies the committed writer receipt, producer/time and current source permission, ignoring stored URLs. It navigates to the existing inspection execution route. Existing assignment options retain their assigned checklist version after retirement. Execution mutations now reject an obsolete cleaning cycle and recheck the current supervisor role; historical reads remain separate from mutation admission.

Login return routes and direct page queries are validated. Duplicate/extra scope parameters fail before business reads. Session loss hides assignment data and stops pending writes; CSRF, explicit confirmation and no automatic write retry remain enforced.

## Direct evidence

- `checkpoint59-room-board-e2e.json`: PASS for **Room Board → Pending Inspections → Assign → reload existing assignment → delivered inbox → Start → Save PASS result → Finish → reload Pending Conclusion**. SQL confirms one inspection, one assignment audit and three execution audits. This occurrence ends at UC042's Pending Conclusion handoff; it does not assert a newly executed Approve/Reject in this receipt.
- The same fixture passes 15 service/SQL checks: stale task/cycle, foreign branch, invalid checklist selection, lost inspector role/branch, inactive inspector, cleaner conflict, Retired checklist for a new assignment, audit-fault rollback, concurrent same-key replay, duplicate/changed request rejection, revoked actor replay, retention of the exact assigned version after retirement, obsolete-cycle execution denial and current-role execution denial (related assertions are grouped in the receipt).
- HTTPS rejects anonymous reads, missing CSRF/If-Match, stale task/cycle, foreign inspector and a nonfuture deadline without changing the options snapshot. The UI performs the successful mutation, not an injected test-only action.
- Notification probe explicitly invokes delivery: forged audit, disabled writer, revoked branch/role, undelivered receipt, changed inspector and malicious stored URL checks; SQL fault rollback; concurrent/repeated delivery produces one recipient. Hosted worker scheduling is not claimed.
- Backend solution build and ModelProbe: PASS, final **0 warnings / 0 errors**. Actual regression: **552 PASS / 11 SKIP / 0 FAIL**. Solution-level `dotnet test` exits 0 but includes no test project.
- Frontend typecheck and production build PASS. Auth/Router/assignment/execution/source run: **190 PASS**. Subsequent shared-host/room/client run: **152 PASS**. Focused new route admission/session tests: **3 PASS**. These runs overlap and must not be added together. npm ci and the entire frontend suite were not rerun; dependency files are unchanged.
- `checkpoint59-cleanup-final.txt`: independent exact database identity and zero synthetic rows, no trial writer/registry/principals/fault constraints, original enabled projection triggers and trusted rework CHECK. Owned HTTPS host stopped. No shared database or production mutation/deployment.

Failures were investigated, not counted as PASS: fixture shape/immutable branch/required timestamp violations in rollback rehearsals; incorrect test expectation for ineligible assignee (the existing contract is InvalidReference/400); duplicate options route; missing login destination; notification/test selector mistakes; attempted fixture retirement reversal while an assignment existed; one overlapping probe build/run caused an apphost file lock and an old fixture payload. Final runs serialize build before runtime and preserve every SQL guard. One nullable compiler warning was fixed. Remaining build warnings are Vite's React Router use-client directives and 688.98 kB chunk; existing esbuild import.meta/IIFE warnings appear in tests.

## Remaining lifecycle work

1. UC041 change-inspector flow for an existing open inspection, with explicit lifecycle guards, audit and notification retirement/reassignment behavior; distinct-person assignment browser acceptance and full criterion ledger.
2. UC042/043 missing-item summary, complete evidence/revision and concluded-state acceptance, then compose the newly assigned occurrence with verified UC044/045. C55/C57/C58 conclusion/rework results remain historical evidence, not rerun claims here.
3. UC048 later-cycle history filtering/paging acceptance; then FE11/FE13 and UC006/010 business closure.

Initial local/remote HEAD: `a76653c7492d670e88bf0e7f1f52979018fe1a61`. Canonical branch: `integration/dev1-dev2-dev3-unblock-20261114`. The original sprint deadline remains unchanged and the goal remains active. Delivery uses one normal logical commit/push after remote-head and changed-path checks; no main/develop merge.

## Changed files

- `docs/requirements/DEV3_APPROVED_142A_DECISIONS.md`
- `docs/verification/DEV3-FASTTRACK-142A/UC_STATUS_MATRIX.md`
- `frontend/src/notifications/NotificationCenter.tsx`
- `frontend/src/notifications/inboxApi.ts`
- `frontend/src/screens/rooms/RoomWorkflowActions.tsx`
- `frontend/src/shared-host/SharedAuthHost.tsx`
- `frontend/src/shared-host/returnUrl.ts`
- `frontend/tests/rework-notification-source.test.mjs`
- `frontend/tests/shared-auth-host.test.mjs`
- `scripts/verification/CanonicalDatabase/ModelProbe/Program.cs`
- `src/HotelOps.Api/Controllers/HousekeepingWorkflowController.cs`
- `src/HotelOps.Api/Services/Housekeeping/HousekeepingAssignmentsService.cs`
- `src/HotelOps.Api/Services/Housekeeping/InspectionExecutionService.cs`
- `src/HotelOps.Api/Services/Notifications/InspectionPassedNotificationDelivery.cs`
- `src/HotelOps.Api/Services/Notifications/NotificationCenterService.cs`
- `docs/verification/DEV3-FASTTRACK-142A/CHECKPOINT-59.md`
- `docs/verification/DEV3-FASTTRACK-142A/checkpoint59-cleanup-final.txt`
- `docs/verification/DEV3-FASTTRACK-142A/checkpoint59-initial-assignment-attempt6.json`
- `docs/verification/DEV3-FASTTRACK-142A/checkpoint59-initial-assignment-verified.json`
- `docs/verification/DEV3-FASTTRACK-142A/checkpoint59-room-board-e2e.json`
- `frontend/scripts/sprintInitialInspectionBrowser.mjs`
- `frontend/src/api/dev1/initialInspectionAssignment.ts`
- `frontend/src/features/inspectionExecution/InitialInspectionAssignmentPage.tsx`
- `frontend/src/features/inspectionExecution/PendingInspectionsPage.tsx`
- `frontend/tests/initial-inspection-assignment-client.test.mjs`
- `scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionFixture.sql`
- `scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionHttpsProbe.cs`
- `scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionNoticeProbe.cs`
- `scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionProbe.cs`
- `scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionServiceProbe.cs`
- `scripts/verification/CanonicalDatabase/ModelProbe/SprintInitialInspectionWriter.sql`
- `src/HotelOps.Api/DTOs/Housekeeping/InitialInspectionAssignmentDtos.cs`
- `src/HotelOps.Api/Services/Housekeeping/InitialInspectionAssignmentReadService.cs`
- `src/HotelOps.Api/Services/Housekeeping/InitialInspectionAssignmentService.cs`
- `src/HotelOps.Api/Services/Notifications/InitialInspectionAssignedNotificationDelivery.cs`
- `src/HotelOps.Api/Services/Notifications/InitialInspectionAssignmentSource.cs`
- `tests/HotelOps.Dev1.RegressionTests/InitialInspectionAssignmentNotificationTests.cs`
