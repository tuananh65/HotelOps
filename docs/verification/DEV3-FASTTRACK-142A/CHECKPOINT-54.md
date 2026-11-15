# FASTTRACK-142A checkpoint 54: Reject notification to authorized history

New COMPLETE_VERIFIED UCs: **0**. Total remains **7 COMPLETE_VERIFIED / 137 PARTIAL**. UC045's immediate notification/source handoff is implemented; its dedicated no-defect exception and Reject audit-fault rollback acceptance remain. UC048 is not complete merely because its detail workspace is now reachable.

## Business implementation

The recipient can select **Open inspection** from an INSPECTION_FAILED inbox item. The resolver verifies the current session, retained delivered inbox membership, unique Reject audit, producer/time, Failed source and official cleaner attribution. It then rechecks current branch INSPECTION_VIEW and, for supervisor recipients, active eligible HK_SUPERVISOR membership. It returns a locally constructed `/inspection-history/{id}?branchId={branch}` destination; stored ActionUrl is never trusted. No permission is added and the cleaner is not inferred from the current assignment.

The destination admits the exact history detail through its existing authorized API before mounting the existing read-only workspace. Private evidence remains behind the history endpoint's current authorization. Invalid/ambiguous IDs do not start a read, denied admission does not mount the workspace, and authority changes remount/clear the subtree. The inbox client and login return registry allow only validated canonical paths and safe integer IDs.

Delivery and opening share the same official-source verifier. It rejects malformed/non-object audit payloads and requires a serializable transaction. Held read locks retain the source snapshot without taking an unnecessary inspection update lock after reading an inbox row; delivery retains its existing ordered eligibility, audit and notification locks and atomic deduplication. The final probe races two source readers and repeated delivery using separate connections/contexts.

## Direct validation and retained failures

- Backend solution and ModelProbe builds: PASS, 0 compiler warnings/errors. `dotnet test HotelOps.slnx` succeeds but includes no test project. Actual Dev1 regression: **532 PASS / 11 SKIP / 0 FAIL**. Skips are not presented as runtime coverage.
- Frontend typecheck/build: PASS. Final history/client/auth-router tests: **200 PASS / 0 FAIL/SKIP**. Full frontend suite and npm ci were not rerun; dependency lockfile unchanged. Existing React Router `use client`, 676.17 kB Vite chunk, EF unmapped indexes and unordered row-limit warnings remain.
- `checkpoint54-rejection-source.json`: browser opened history and photos, then the Node APIRequestContext anonymous request failed certificate trust. Cleanup PASS. Switched that assertion to a separate trusted browser context with no cookies; TLS verification remains enabled.
- `checkpoint54-rejection-source-browser.json`: PASS for normal and blocked cases, including supervisor and actual-cleaner inbox → history → reload/photos, anonymous/foreign evidence denial, revoked branch source/history denial, role revocation, undelivered receipt denial, audit tamper and forged producer denial, restored positive source, and the existing complete rework fixture checks.
- `checkpoint54-rejection-source-final.json`: the added concurrency probe exposed a harness error: its session lookup used the parent DbContext concurrently. Cleanup PASS. Each reader now loads its real browser-created session through its own fresh context. This was a harness failure, not evidence of a product concurrency PASS.
- `checkpoint54-rejection-source-verified.json`: final corrected run PASS for normal and blocked rooms. Both supervisor and cleaner can open/reload the correct history/photos; negative source/permission/tamper cases and three concurrent reader/delivery rounds per case pass. The existing full rework fixture also completes its acceptance checks. This proves successful overlapping calls, not an exhaustive SQL deadlock stress test.
- `checkpoint54-cleanup-final.txt`: independent exact server/database/GUID/run verification; zero users/branches/reworks/inspections/checklist versions/items/attachments/intervals/notifications/blocks/incidents/private files. Trial bindings/writers/principals/fault constraints absent; original projection triggers enabled and workflow CHECK enabled/trusted. Both source cases and cleanups PASS. Owned HTTPS host stopped; no shared database mutation.

The initial sandboxed build could not read the existing local NuGet configuration; authorized escalation succeeded. The first ModelProbe compile used `InsightException.StatusCode` instead of its actual `Status` property; corrected and rebuilt. No dependency downgrade, authentication bypass, disabled TLS or shared database mutation was used.

## Scope and next work

Product changes: `InspectionFailedNotificationSource.cs`, `InspectionFailedNotificationDelivery.cs`, `NotificationCenterService.cs`; frontend `InspectionHistoryAuthRoute.tsx`, shared host/return registry, inbox API/UI and history host documentation. Verification changes: source browser/SQL probe, parent fixture hook and focused tests. No generated entity or database schema changes are introduced by C54.

Next: verify Reject with no recorded defect and an injected audit failure; exercise notification source after later cycle transitions; then close UC041 pre-assignment checklist compatibility and the composed next rework round through Passed. Keep FE11/FE13/UC006/010 priorities active. Hosted worker scheduling and production deployment are not claimed by manually invoked delivery tests.

TASK_ID=DEV3-FASTTRACK-142A. Canonical branch `integration/dev1-dev2-dev3-unblock-20261114`; initial local/remote SHA `51fefd58c22fa7e930922a8a92922a778bf4eb22`. Pre-delivery fetch confirmed 0 ahead/0 behind. Commit/push receipt follows after final verification.
