# FS-018 — Cleaning Cancellation & Inspection integration

**PARTIAL.** UC037 Cancellation **PROVISIONAL V1 / NOT SHARED ACCEPTED**. UC035/036 remain **PROVISIONAL_NOT_ACCEPTED**. Inspection and Notification are not shared accepted; FE-04 **NOT_READY_FOR_MERGE**. No additional UC credit, Tester acceptance, package, PR or merge.

Branch `feature/dev1-room-ops`; baseline `e098a285e0c2a302086155c5965be07929f93b57`. Source commit `960a4efaa65db7948d19484b149d67d4721d716a`. Project Leader directly approved FS018 source/tests and documentation commit/push in this Codex session. Documentation/inventory are committed separately. This Git approval does not change provisional contracts or shared acceptance.

## UC037 Cancellation V1

The approved trial uses an active `HK_SUPERVISOR` in the requested branch, explicit active branch access and an eligible existing `CLEANING_ASSIGN` BRANCH/GLOBAL grant. OWN does not authorize supervision. There are no permission seeds, hard-coded role/branch IDs or shared RBAC changes.

`GET /api/branches/{branch}/cleaning-tasks/{id}/cancellation` returns scoped capability and strong ETag. `POST .../cancel` requires real cookie/session, HTTPS, CSRF, confirmation, reason (trimmed 1–500 characters, no controls), one strong If-Match and a GUID Idempotency-Key. The controller delegates business rules to the existing partial service.

Only current `PENDING_ASSIGNMENT` without active assignment or `ASSIGNED` with exactly one current assignment, legacy status PENDING and no actual start/end can proceed. Branch/floor/area ownership and active scope are checked. Future assignments, any current-task execution interval/progress history, associated Inspection/Rework, unreleased/future-release RoomBlock, inconsistent room state, missing/future/blocked readiness, and ambiguous previous history fail closed.

The supported readiness case has **no previous non-cancelled cycle**, persisted room status DIRTY and a current-cycle latest PENDING_CLEANING event without effective block. Earlier cancelled cycles must have no contradictory execution history. The cycle pointer remains on the cancelled cycle so history is preserved and subsequent task creation can use the existing explicit pointer. No task/cycle/assignment history is deleted. Cancellation ends an active assignment with CANCELLED while preserving its assignee, assignment time, assigner and original reason. It does not change `rooms.operational_status`, create READY, cancel Inspection/Rework or create Inspection PASS.

**Previous non-cancelled cycle readiness remains BLOCKED** (`PREVIOUS_CYCLE_READINESS_CONTRACT_REQUIRED`). There is no accepted terminal Inspection/cycle readiness authority to justify selecting/restoring an older state. V1 does not silently use an old event as that authority. This limitation also applies to an imported COMPLETED cycle lacking accepted source proof.

One physical SERIALIZABLE transaction owns the task/assignment mutation, new readiness event, cancellation audit with actor/reason/operation, durable operation receipt, and (when assigned) publication plus exactly one IN_APP intent to the current assignee. The existing user eligibility locks, WorkTime user locks and catalog transaction lock are reused. Recipient read eligibility and the preflight assignment are revalidated under the transaction. No new Workday or interval is created. Mandatory staging/proof failure rolls everything back. Final session/permission checks run before commit.

Fingerprints bind action, actor, branch, task, original ETag and reason. Exact-key replay reauthorizes the supervisor and verifies persisted task/assignment, audit, readiness, publication and intent proof; a changed payload returns conflict. Separate concurrent keys yield one success and one stale response; the same key returns the same durable receipt. A lost commit acknowledgement performs bounded receipt recovery without repeating the business write. UI retries preserve the original key, body and ETag, refreshing CSRF; stale/denied requests require a reload.

## Trial and Notification boundary

Only Development + `CleaningCancellation:Candidate=FS018_PROVISIONAL` + exact database `HotelOps_Canonical_FS018_Cancellation_V1_Fresh` + local machine/owner/schema markers enable the writer. No opt-in was added to shared configuration. Shared/production flags remain **OFF**; Production is denied even with candidate configuration.

`scripts/verification/FS018/New-CancellationTrial.ps1` verifies the 18 frozen foundation source hashes, refuses an existing database, creates a new synthetic local trial and installs the existing foundations plus the FS018 event constraint. No DROP/reset or existing shared/production database write. Executed with Project Leader's direct approval:

```powershell
& scripts/verification/FS018/New-CancellationTrial.ps1 -ExecuteApprovedCreate -AllowLocalSqlCertificateException
```

Actual: CREATED_NEW_DISPOSABLE, correct FS018 owner, foundation hashes matched, existingDatabaseWritten=false. The certificate exception is local SQL only; live HTTPS uses normal trusted localhost TLS without a bypass. A later plan-only check also matched hashes. Re-running creation against this database is intentionally refused.

`CLEANING_TASK_CANCELLED` schemaVersion 1 is **PROVISIONAL_NOT_ACCEPTED**, staged through the existing `ProvisionalCleaningNotification` and existing operation/publication/intent ledger. Source identity includes task/cycle, assignment, assignee, room/branch, actor, operation, source revision, occurrence key and timestamp. Unassigned cancellation persists an operation receipt and audit/readiness without a fabricated recipient/publication. Assigned cancellation cannot commit without its publication/intent. No second publisher, dispatcher, hosted worker or FE14 implementation was added.

**Staging is verified; cancellation inbox delivery is NOT_RUN / not implemented by an accepted dispatcher contract.** The existing explicit delivery paths are not broadened into an FS018 dispatcher. Shared acceptance still requires the FE14 owner's accepted event, recipient source-read after assignment closure, dedup/retry/revocation and delivery contract/evidence. A pending intent is not evidence of inbox delivery.

## UC035/036 Inspection integration

No new Inspection commit or handover was supplied since FS017; the starting tree was clean at the exact baseline. No remote branch sweep or old FS001–016 source rediscovery was performed. The existing UC046 authoritative conclusion gap remains. No FE05 writer or production Inspection result/proof was added or changed.

Preserved candidate: UC035 validates persisted FAILED/FAIL Inspection, row version, source digest, linked defects, branch/current cycle, current rework assignee/source-read, Workday and interval. UC036 requires remediation per linked defect, closes the interval, writes audit/readiness and stages REWORK_SUBMITTED atomically, ending in PENDING_REINSPECTION without final completion/PASS/READY. Its existing `SYNTHETIC_TEST_ONLY` proof remains restricted to tests/trial; it is not authoritative Inspection integration evidence.

Exact missing contract: accepted UC046 writer/handover identity and version; committed FAILED conclusion + linked defect/attachment set and source revision identity; source-read authorization and invalidation/reassignment/current-cycle rules; atomic creation/linking of rework/proof; UC036 reinspection receiver and PENDING_REINSPECTION ownership; accepted REWORK_SUBMITTED Notification contract. No synthetic fixture substitutes for these dependencies.

## Validation and evidence

Final backend `dotnet build HotelOps.slnx`: **PASS, 0 warnings, 0 errors**. `dotnet test HotelOps.slnx --no-build`: exit 0; solution contains only API, so no test count is inferred from it. Frontend `npm.cmd run build`: **PASS**; existing React Router use-client and >500KB chunk warnings remain. Initial sandbox build could not write obj; the authorized elevated build passed. Test project retains NU1900 because the NuGet vulnerability feed is unavailable; this is not reported as a clean vulnerability scan.

Checkpoint command:

```powershell
$env:FS018_SQL_TRIAL='APPROVED_LOCAL_ONLY'
$env:FS018_LIVE='APPROVED_LOCAL_HTTPS'
$env:FS016_SQL_TRIAL='APPROVED_LOCAL_ONLY'
dotnet test tests/HotelOps.Dev1.RegressionTests --no-restore --filter 'FullyQualifiedName~CleaningCancellation|FullyQualifiedName~CancellationLive|FullyQualifiedName~ReworkWorkflowSqlTests|FullyQualifiedName~ReworkLifecycleTests|FullyQualifiedName~CleaningResumeSqlTests|FullyQualifiedName~CleaningLifecycleContractTests|FullyQualifiedName~CleaningMonitorRouteTests|FullyQualifiedName~CleaningMonitorFs016Tests' --logger 'trx;LogFileName=checkpoint.trx' --results-directory TestResults/fs018
```

Actual **130 PASS / 0 FAIL / 1 SKIP**: 56 Unit, 73 SQL component (32 Cancellation on new FS018, 27 Rework and 14 Resume on the previously approved isolated FS016), 1 live Cancellation HTTPS/API/SQL. One read-only Monitor SQL case SKIP because `FS016_MONITOR_SQL=READ_ONLY_FS015` was not enabled; it was not rerun on another trial merely to remove SKIP. Synthetic source checks are not shared acceptance.

SQL covers assigned/unassigned cancellation, valid cancelled history and inconsistent prior source, states, authorization/branch, revoked actor/recipient, current pointer, previous-cycle gap, room/readiness/Inspection/Rework/RoomBlock conflicts, open interval, default/production OFF, stale ETag, same/different-key concurrency, payload conflict, mandatory audit/readiness/publication/intent rollback and lost commit acknowledgement. Live verifies trusted TLS, real login/session/cookie/CSRF, 400/403/428/412 boundaries, successful cancellation, replay/conflict and SQL persisted staging with room DIRTY. Random synthetic credentials remain in memory and hash in the trial; they are not printed or committed.

Frontend Node checkpoint: `node --test tests/cleaning-cancellation.test.mjs tests/cleaning-execution.test.mjs tests/cleaning-monitor.test.mjs tests/rework-execution.test.mjs` — **23/23 PASS**, including 3 Cancellation tests. Browser interception: `npx.cmd playwright test --config playwright.fs018.config.ts` — **15/15 PASS**, 3 Cancellation + 12 Monitor. This is not a real browser against the live API. NO_COLOR/FORCE_COLOR warning remains; Monitor capability fixtures now intercept the independent cancellation GET instead of reaching a local proxy.

Earlier focused runs failed on fixture status/phase/identifier-length constraints and an audit fault expectation (EF wraps the injected write failure); failed evidence is retained. They are superseded by the checkpoint, not added to final counts. No business constraint/trigger was disabled to obtain PASS. See the Unit Test inventory for each actual case and historical run counts.

Local ignored evidence: `D:\SEP490\Projects\HotelOps-DEV1\TestResults\fs018\checkpoint.trx` and earlier `*.trx`; browser `D:\SEP490\Projects\HotelOps-DEV1\frontend\test-results\fs018\` and `.last-run.json`; Node bundles `.test-output/fs018/`; build/Node/browser stdout in the Codex session. No TRX, credential, SQL backup/data dump, compiled binary or browser artifact belongs in this change.

NOT_RUN: authoritative Inspection integration, accepted notification inbox delivery, previous non-cancelled readiness restoration, shared/production execution/deployment, production least-privilege SQL identity, browser against live API, coverage measurement and Tester/shared acceptance.

## Changed source and remaining acceptance

Backend: `CleaningExecutionController.cs`; new `CleaningCancellationDtos.cs`, `CleaningCancellationRules.cs`, `CleaningCancellationService.cs`, `ProvisionalCleaningCancellation.cs`. No generated entity, DbContext, migration or shared appsettings change.

Frontend: `CleaningMonitorDetail.tsx`; new `cleaningCancellation.ts`, `CleaningCancellationPanel.tsx`. Tests: `CleaningCancellationTests.cs`, `CleaningCancellationSqlTests.cs`, `CancellationLiveTests.cs`, `cleaning-cancellation.test.mjs`, `cleaning-cancellation.spec.ts`, scoped `cleaning-monitor.spec.ts` fixture update and `playwright.fs018.config.ts`. SQL/provisioning: `fs018-candidate/01_Cancellation.sql`, `FS018/New-CancellationTrial.ps1`. Documentation: this README and FE04 Unit Test inventory only.

FS019 proposal: obtain the precise UC046/Inspection and FE14 event/receiver handovers, agree previous-cycle readiness authority and post-cancellation source-read/delivery, then verify those integrations with focused SQL/live tests. Keep FE-04 NOT_READY_FOR_MERGE until dependencies and shared acceptance are resolved. No UC credit is added by FS018.
