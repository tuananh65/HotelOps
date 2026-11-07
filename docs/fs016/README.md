# FS-016 — Cleaning Rework & Monitoring

Status: **PARTIAL / isolated candidate implemented and verified**. UC035/UC036 **PROVISIONAL_NOT_ACCEPTED**; UC037 read gaps implemented, cancellation alternative remains blocked. No shared integration acceptance or additional UC credit. FE-04 **NOT_READY_FOR_MERGE**. Baseline trước FS016 `095fb3ee7738411897e41218deafa6b927c79c2e`, branch `feature/dev1-room-ops`, clean at preflight. FS016 source commit `2afbb696ef8b60920be9a25ef16b163d4e6ea29b` (`feat(housekeeping): implement provisional rework lifecycle and monitoring`), 30 source/test/script files. Project Leader directly approved FS016 commit/push in this Codex session; README/inventory are committed separately.

## Confirmed candidate contract and source boundary

Project Leader explicitly approved isolated FS016 candidate, per-defect remediation schema, current assignee/CLEANING_EXECUTE, source-read checks, synthetic Inspection fixtures only in automated tests, and mandatory REWORK_SUBMITTED staging through the existing Notification ledger. This approval is not shared Inspection/Notification acceptance.

Adapter reads persisted Inspection FAILED, its cleaning cycle, failed criterion results and exact linked defects. It requires matching room/branch/current-cycle pointer, completed failed source with timestamp, complete mandatory criteria, latest inspection, one open rework, eligible current assignee and a stored source proof. Proof binds inspection rowversion and SHA256 of source identity, assignment/due/reason and linked defect content. In this trial, origin must be **SYNTHETIC_TEST_ONLY**. Only test fixture code inserts these proofs. Production request handlers do not create or conclude Inspection, insert proofs, mark PASS, resolve defects as verified or set READY.

HK Staff does not receive general INSPECTION_VIEW. The approved candidate binding uses eligible CLEANING_VIEW/CLEANING_EXECUTE grant paths and current rework ownership for the linked defect subset only, including evidence downloads. No role/permission seed or shared resource resolver change. Missing/mutated proof, foreign branch/owner, revoked scope or noncurrent source fails closed. No accepted Inspection conclusion writer/UC046 handover exists in this baseline.

## UC035 / UC036 implementation

UC035 Accept: PENDING_ACCEPTANCE → ACCEPTED. Start: ACCEPTED → IN_PROGRESS; requires Working workday, no conflicting open interval, current role/branch/HK shift eligibility; opens REWORK interval. No skipping acceptance. Same actor/branch/source authorization applies to reads and writes.

UC036: IN_PROGRESS → PENDING_REINSPECTION, exact one nonblank 1–500 character remediation per linked defect; optional JPG/PNG/PDF reuse private evidence validator/storage (10 MiB/file, five total). Close exactly the current REWORK interval. Reject unresolved/unreconciled/linked blocker without confirmed source resolution. Persist remediation actor/time, links, completion audit/result, readiness PENDING_INSPECTION + effective block, operation receipt and mandatory supervisor publication/intents in the same transaction. Cleaning cycle remains REWORK_REQUIRED awaiting reinspection; completed_at is not falsely finalized. No Inspection PASS or READY.

Uses existing BatchAsync/HotelOpsDbContext and WorkTime participant with canonical user locks → WorkTime locks → branch/catalog lock and final permission checks. Strong rework If-Match, explicit GUID Idempotency-Key for every action. Link/evidence triggers bump rework rowversion. Fingerprint binds source type, actor/branch/rework/action, original ETag, note and per-defect remediation/photo digests. Replay reauthorizes current source/owner and validates durable receipt/audit/event proof; same key/different body is 409, stale ETag is 412. Ambiguous commit recovery reads only durable receipt, never repeats mutation. File rollback cleanup checked; after ambiguous commit keep bytes until reconciliation to avoid deleting committed evidence.

Routes on existing CleaningExecutionController:

- GET `/api/branches/{branch}/reworks/{id}/execution`
- GET `/api/branches/{branch}/reworks/{id}/evidence/{attachmentId}`
- POST `/api/branches/{branch}/reworks/{id}/accept`, `/start`, `/complete`

Cookie authentication, HTTPS, existing CSRF/session transport, missing If-Match 428, invalid input 400, forbidden 403, missing linked evidence 404, state/key conflict 409, stale 412, contract/dependency unavailable 503. DTOs reject client-supplied source/result/actor fields. Controllers only dispatch/map HTTP/file responses.

Frontend uses the existing routes/client/session/CSRF and a rework execution panel. Rework list reuses Monitor instead of prototype data/KPIs. UI shows server defect/inspection/cycle/assignee/state, scoped source/remediation evidence, Accept/Start/per-defect Complete forms. Retains exact key/tag/body on explicit retry after uncertain failure; no automatic repost. Stale result reloads source and clears old form. Monitor links to execution; supervisor Monitor access does not imply current assignee execution permission. Staff can open an authorized detail link; a separate OWN-scoped rework list is not implemented.

## Notification and SQL

**PROVISIONAL_NOT_ACCEPTED**, same ProvisionalCleaningNotification/ProvisionalCleaningDelivery instances and operation/publication/intent tables. No additional dispatcher/publisher/host registration. REWORK_SUBMITTED schema 1 uses CLEANING_REWORK source, rework/inspection/task-cycle/room/branch/actor identity, source ETag, operation/publication IDs, occurredAtUtc and occurrence `REWORK:{id}:SUBMITTED`. Existing publication gets nullable rework FK and source CHECK, not a fake cleaning assignment ID. Recipients are active HK_SUPERVISOR in branch with explicit branch access and eligible CLEANING_VIEW branch/global path. Exact staged recipient count/audit/receipt proof verified. Explicit delivery revalidates source branch and recipient; idempotent inbox linkage in the same ledger. Accept/Start have durable operation receipts/audit without an invented mandatory event.

Only new `HotelOps_Canonical_FS016_Rework_V1_Fresh`, owner `HotelOps.FS016.Owner=DEV1_FS016`, marker `HotelOps.FS016.Rework=PROVISIONAL_NOT_ACCEPTED`, Development and explicit `CleaningRework:Candidate=FS016_PROVISIONAL` allow rework. Shared/production flags remain **OFF**; Production still rejects even with candidate flag. Closure/Resume exact-target gates support this new trial for regression, preserving earlier isolated targets.

New trial assembled from frozen foundation manifest (18 normalized source hashes), existing FS014 sidecar and Notification candidate; no old trial/shared/production database writes. Initial final DDL batch failed binding newly added columns; transaction rolled back, corrected dynamic DDL installed forward-only. Evidence test exposed unsupported attachment entity type: reused existing CLEANING_TASK + cycle ID and dedicated rework/defect links, aligned candidate trigger; did not widen enum CHECK. No drop/reset. Corrected installer plan-only hash verification PASS; pristine rerun of full create is NOT_RUN because exact target already exists and overwrite is refused.

## Validation and evidence

| Check | Actual |
| --- | --- |
| Backend build | PASS, `dotnet build HotelOps.slnx`, 0 warnings/errors |
| Solution test | `dotnet test HotelOps.slnx --no-build --no-restore`, exit 0; API-only solution, not the regression count |
| Final focused checkpoint | **195 PASS / 0 FAIL / 0 SKIP = 129 Unit + 65 SQL component + 1 live HTTPS/API/SQL**; `TestResults/fs016/fs016-checkpoint-final.trx` |
| SQL component breakdown | 27 rework + 14 Resume regression + 23 cleaning closure regression on new FS016 trial; 1 Monitor read-only on existing synthetic FS015 trial. Session validation double in service/SQL tests; real permission/source queries. No business writes to FS015. |
| Frontend Unit | **20 PASS / 0 FAIL**: rework 4, Monitor 5, cleaning execution regression 11 |
| Frontend production build | PASS; existing React Router use-client and >500KB chunk warnings |
| Browser interception | **20/20 PASS in final run**: Monitor 12 + rework 8, `playwright.fs016.config.ts`; local `frontend/test-results/fs016/.last-run.json`, session stdout. Not real API acceptance. |
| Live HTTPS/API/SQL | **1 PASS** in final checkpoint: owned real API, trusted localhost TLS without bypass, real login/cookie/CSRF, missing CSRF 400 / tag 428, Accept/replay, Start, stale 412, per-defect Complete with PNG/replay/download and SQL intervals/publication assertions. Input Inspection is synthetic test fixture, never accepted writer evidence. Owned process stopped, random password memory-only, no runtime credential logs. |

Focused command and individual test source/method/objective/UC/expected/actual/evidence are in FE-04 inventory. Coverage NOT_MEASURED. TRX under ignored TestResults are local evidence, not committed or Tester-packaged. Browser/Node/build evidence also includes this session's tool stdout.

Preserved failed attempts: initial Monitor 12 Unit PASS / 1 SQL login timeout, retest 13 PASS after new harness timeout 5→30 sec (`fs016-focused-attempt1.trx`, `fs016-focused-retest.trx`); rework initial 1 PASS/24 FAIL due harness timestamp-to-hex ETag, fixed binary(8) conversion without relaxing strong-tag check (`fs016-rework-attempt1.trx`); next 24 PASS/1 FAIL evidence mapping (`fs016-rework-retest1.trx`); corrected run 46 PASS (`fs016-rework-final.trx`) before final expanded checkpoint. Initial C# interpolation error corrected; initial sandbox generated-file denials resolved via approved elevated execution. Earlier frontend command at repository root returned ENOENT; corrected frontend directory passed. Test restore NU1900 (NuGet vulnerability feed unavailable), browser NO_COLOR/FORCE_COLOR remain.

NOT_RUN: accepted authoritative Inspection writer/handover, shared integration, production least privilege/deployment, live browser against real API, public Workday UC139, coverage, UC037 cancellation end-to-end. Synthetic source/API success cannot satisfy these.

## Files / remaining work / FS017

Added: ReworkLifecycleDtos.cs, ReworkLifecycleRules.cs, ReworkLifecycleService.cs, ProvisionalReworkNotification.cs; reworkExecution.ts, ReworkExecutionPanel.tsx; ReworkLifecycleTests.cs, ReworkWorkflowSqlTests.cs, ReworkLiveTests.cs, CleaningMonitorFs016Tests.cs; rework-execution.test.mjs/spec.ts, playwright.fs016.config.ts; fs016-candidate/01_Rework.sql, FS016/New-ReworkTrial.ps1; this README.

Modified: CleaningExecutionController.cs, CleaningMonitorDtos.cs, CleaningMonitorService.cs, CleaningBlockerResolution.cs; candidate ProvisionalCleaningClosure.cs/ProvisionalCleaningDelivery.cs; ReworkTaskListScreen.tsx/ReworkTaskDetailScreen.tsx, CleaningTaskListScreen.tsx/CleaningMonitorDetail.tsx, cleaningMonitor.ts and its tests; Resume/closure SQL harness exact-target opt-in; FE-04 Unit Test inventory. No generated entities/mappings, global DI/configuration, unrelated module implementation or integration notes change.

UC037 read gaps: effective workflow status, unambiguous assignee, persisted remediation progress, unresolved blocker, current cycle, branch scope, existing date/floor/status/assignee/priority/overdue/search filters and pagination/navigation. Rework detail labels cleaning-cycle timeline explicitly to avoid presenting cleaning assignments/progress as rework history. Cancellation remains unavailable pending accepted cancel/Notification contract; no mandatory notification bypass. Reassignment remains separate existing candidate scope.

Recommended FS017: confirm authoritative Inspection conclusion/UC046 source handover and Notification contract, then address UC037 cancellation and UC038 turnover-history acceptance in explicitly agreed scope. Continue to keep FE-04 NOT_READY_FOR_MERGE until shared contracts/acceptance are resolved. No FE14/FE15 or UC038 implementation, Tester ZIP/request, PR, merge, force-push or unapproved Git operation in FS016.

Local evidence retained outside Git: `D:\SEP490\Projects\HotelOps-DEV1\TestResults\fs016\fs016-checkpoint-final.trx` and `D:\SEP490\Projects\HotelOps-DEV1\frontend\test-results\fs016\.last-run.json`; ignored TRX/test artifacts are not staged. Historical attempts remain under the same local TestResults/fs016 directory.
