# FASTTRACK-142A checkpoint 34 — durable retry and approval concurrency

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=51f9eb8e4f8cc326dbff5586ee02fbf732e8b9cb
NEWLY_COMPLETED_UCS=0
UC_COMPLETE_VERIFIED=1 (UC110; historical checkpoint31)
UC_PARTIAL=143

## Business work

Added InspectionNotificationWorker and its delivery interface around the existing source/recipient-authorized delivery implementation. The audit event remains the durable retry source. Every discovery and delivery attempt has a fresh scoped context; delivery retains its own SERIALIZABLE transaction and revalidates original-cleaner proof and current access. A failed event does not poison or starve later events. The bounded 100-event cursor wraps after each scan and resets after discovery failure; no event is acknowledged merely by advancing the cursor. Cancellation stops the loop.

Activation is explicit: InspectionNotifications:Enabled defaults false; PollSeconds defaults 30 and validates 1–3600. The test host alone used Enabled=true and PollSeconds=1 against the owned disposable database. No default shared/production background writes, deployment, permission seeds, generated-entity edits or EF migrations. The host was stopped after execution.

## Direct execution evidence

- [Hosted retry via browser/HTTPS/SQL](checkpoint34-approval-retry.json): both normal and blocked approval persist despite a synthetic notification CHECK failure; browser reports PENDING_DELIVERY. The durable event remains and no partial inbox survives. Revoke original recipient branch access, remove the fault, and the worker still does not deliver. Restore access and the actual hosted worker delivers one notification, without replaying approval. Subsequent polling and SQL counts confirm one approval audit/recipient. Cleanup verified.
- [Concurrent block changes](checkpoint34-block-race.json): a test-only trigger marks and delays the actual approval write while its room/block locks are retained. A separate connection races block insertion or release and waits 4283ms/4200ms respectively. Approval returns the state from its own transaction; subsequent HTTPS/browser reload reflects the new facts: inserted block -> BLOCKED, released final block with passed cycle -> READY. Test trigger and synthetic records removed. These SQL writers are deliberate test injections, not acceptance of the separate FE06 release API/audit workflow.
- [Concurrent approval and multiple blocks](checkpoint34-approval-race.json): two authenticated HTTPS requests share one ETag; exactly one returns 200 and one 412, for both normal and two-effective-block fixtures. SQL confirms one approval audit, the expected physical history and one original-cleaner notification. Multiple blocks keep Passed effectively BLOCKED. The previously approved guarded incident/room uniqueness script is trialled only in the disposable database; cleanup restores the original room-only index.
- [Independent cleanup](checkpoint34-cleanup.json): users, branches, inspections, cleaning tasks, room history, blocks, incidents, audits, notifications, recipients and sessions all zero; test attribution/trigger/constraint objects absent; originalIndex=1, trialIndex=0.

## Validation and runtime observations

- [Final solution build](checkpoint34-build-final.txt): PASS, zero warnings/errors. [Backend tests](checkpoint34-tests.txt): 506 PASS, 11 SKIP; four new worker tests cover context disposal/fault isolation, scan continuation/wrap, disabled startup and cancellation.
- [Probe builds](checkpoint34-approval-race-build.txt): PASS. [Solution test](checkpoint34-solution-test.txt) exits successfully with no test project in the solution; not counted as test coverage.
- Existing EF unmapped-index and Take-without-OrderBy warnings remain. The injected notification CHECK errors are intentional failure tests. During the final concurrency run, the live host also logged SQL 1205 in notification discovery; the worker caught it, retained durable events and scheduled a rescan. All recorded approval/notification assertions passed. A deadlock graph was not captured; this is a runtime contention observation, not an assertion that all deadlocks are eliminated.
- Frontend application source and dependency lock were unchanged in this checkpoint; only the browser verification driver changed. Checkpoint33 frontend build and AuthRouter results are historical, not newly executed acceptance here.

## UC044 acceptance decision and remaining workflows

Newly COMPLETE_VERIFIED UCs: **0**. Approval success, blocked alternative, audit/history atomicity, delivery recovery and concurrency now have direct evidence. However UC044's mandatory-criterion FAIL exception explicitly requires offering Reject (UC045). The current canonical UI disables approval on failed mandatory criteria but has no functional Reject handoff. That requirement is still missing, so UC044 remains PARTIAL even though its successful approval paths pass. Checkpoint33 source/security negative tests and rollback tests remain supporting historical evidence, not rerun here.

Next highest-impact implementation is Reject -> Request Rework, including failure notification and correct effective readiness before/after a rework request, followed by Reinspection. Full block-release history and remaining room-usability consumers also need integration. FE11 lifecycle, FE13 read authorization and UC006/010 remain in the sprint; no broad checkpoint audit or permission relaxation was performed.

## Changed files

Application: Program.cs; Services/Notifications/InspectionPassedNotificationDelivery.cs; new InspectionNotificationWorker.cs.

Tests: new InspectionNotificationWorkerTests.cs; ModelProbe/Program.cs and SprintApprovalBrowserProbe.cs; frontend/scripts/sprintApprovalBrowser.mjs. Matrix and checkpoint34 execution artifacts included. Final local/remote commit identity is reported after push verification.
