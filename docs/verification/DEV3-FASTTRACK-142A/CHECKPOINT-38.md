# FASTTRACK-142A checkpoint38 — UC046 committed assignee delivery

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=a0f75f169aa7db3994f3d15dabbb0e5b6414c641
NEWLY_COMPLETED_UCS=0
UC_COMPLETE_VERIFIED=2 (UC044/UC110, historical evidence)
UC045_UC046_UC047=PARTIAL

## Implemented business step

The protected FE05 issuer now persists a versioned REWORK_ASSIGNED intent for the actual assignee inside the same CREATE audit/receipt/proof transaction. Request Rework returns PENDING_DELIVERY, not a fabricated delivered receipt. Canonical UI explains that the notification is queued; it claims delivery only for a DELIVERED receipt.

Existing opt-in inspection worker discovery now includes these events. Each delivery uses a fresh clean context and independent SERIALIZABLE transaction, locks recipient eligibility before the authoritative audit/source, checks committed issuer/module/operation/audit binding and current rework assignment/inspection/cycle, and rechecks CLEANING_VIEW with existing principal scope and ownership. A shared helper preserves exactly the executor's existing permission evaluation; no role or permission seed changes.

Notification and recipient commit together. Receipt checks and locks deduplicate concurrent delivery and retries. Revoked access remains pending for later reevaluation. Generic notification text contains no defect/evidence details. ActionUrl stays absent until canonical source navigation is accepted; notifications do not authorize executor or inspection access. Existing executor authority, shift/workday and provisional runtime gates remain intact.

## Direct evidence

- checkpoint38-rework-delivery.json: real browser -> trusted HTTPS -> exact disposable SQL, normal and blocked rooms. No-shift assignee creation returns Pending Acceptance/PENDING_DELIVERY. Existing CSRF/ETag/invalid-input/duplicate checks PASS. Real SQL delivery probe confirms durable discovery, recipient-save fault rolls back notification, branch/account/role revocation denies delivery, event remains retryable, two concurrent contexts plus replay produce exactly one notification/recipient. Authority-reader and prior Reject fanout checks also PASS.
- checkpoint38-final-browser.json: final rebuilt UI and the same browser/HTTPS/SQL delivery/security cases PASS for both room variants. checkpoint38-final-cleanup.txt independently confirms cleanup afterward; owned HTTPS host stopped.
- checkpoint38-build.txt: backend build PASS, zero warnings/errors. ModelProbe build PASS after correcting a verifier C# operator-spacing typo; the initial verifier build failure was not an application failure.
- checkpoint38-regression.txt: 507 PASS, 11 SKIP. checkpoint38-solution-test.txt succeeds without a test project in the solution; not coverage.
- checkpoint38-inspection-client.txt: 24 PASS. checkpoint38-typecheck.txt and checkpoint38-frontend-build.txt PASS. Initial `npm` invocation was blocked by PowerShell script policy; `npm.cmd` ran without policy changes. Existing React Router use-client and bundle-size warnings remain.
- checkpoint38-cleanup.txt: exact disposable identity matches; synthetic users/reworks/audits/notifications/recipients/operations zero, proof/procedure/reader absent after trial. Parent setup rehearses rollback cleanup; no shared database mutation or lasting trial schema deployment.

Delivery was invoked through the same dispatcher used by the worker, using real SQL; this is not a claim that the hosted scheduler or canonical notification inbox/source-opening flow was newly browser-verified. Worker remains opt-in and was disabled in the deterministic fault/revocation trial. Existing worker lifecycle unit tests are included in backend regression.

## Remaining acceptance and next implementation

No new complete UC or complete Reject -> Rework -> Reinspect workflow. Newly verified sequence closes creation -> durable event -> authorized, deduplicated delivery. Source opening/inbox acceptance, business creation rollback/concurrency coverage, canonical executor runtime/schema composition, and reinspection remain. The trial installer is removed during cleanup; an approved installed issuer is a prerequisite for runtime creation. Notification source validation is not a substitute for the full executor authority reader.

Next: canonical source read/navigation with current authorization; integrate Accept/Start/Complete using committed authority and unchanged workday guards; close reinspection with effective readiness and rollback/concurrent-block acceptance. FE11/13, UC006/010 and other sprint modules remain in scope.

## Changes

Application: new ReworkAssignedNotificationDelivery.cs and ReworkRecipientReadAccess.cs; InspectionPassedNotificationDelivery.cs, HousekeepingAssignmentsService.cs, ReworkLifecycleService.cs, RequestReworkForm.tsx.

Verification: issuer trial SQL, SprintApprovalBrowserProbe.cs, SprintRejectionDeliveryProbe.cs, SprintReworkFixture.cs, new SprintReworkDeliveryProbe.cs; matrix and checkpoint38 evidence. Source commits 395715f and 4a21e97. Final evidence/push identity is reported in delivery receipt.
