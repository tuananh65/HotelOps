# P0 Project 2 — Role consolidation and workflow restructuring

## Contract

### Production authorization and deployment — 24 November 2026, 16:04

The user's subsequent explicit request “cập nhật cả production” authorizes the local Production deployment and supersedes the earlier TEST-only deployment restriction below. Production P0 migration committed at 16:02 after fresh verified backup and restored-clone rehearsal. API 5769 PID43404 and frontend HTTPS5080 PID7680 run the new Release. TEST 5774/5174 remains separate. Rehearsal API5775 stopped. See `docs/requirements/p0-261124/production-deployment.md` for evidence and recovery. Full A+B acceptance remains PARTIAL; the two unchecked scope items below are unchanged.

- [x] Fresh Production backup, actual restore and nine migrations on isolated clone.
- [x] Release startup against clone and controlled Production migration rehearsal.
- [x] Final backup with writers stopped, exact-target migration and preservation checks.
- [x] Production restart, trusted HTTPS browser smoke, database role/account verification and process cleanup.

Sources: `docs/requirements/p0-261124/om-consolidation-request.md` (A) and `p0-workflow-request.md` (B). B takes precedence for conflicting role/workflow requirements; A remains applicable for cross-branch transfer, OM operational self-review, assets, reports and action center. Preserve accepted compact UX except explicit new requirements. Paper checklist remains the existing physical checklist workflow.

Only Project 2 Compact. Schema/data/permissions changes authorized in TEST by B §23; backup plus actual restore verification before migration, independent clone first. Production5080/API5769 and its HotelOpsCompactDB must remain unchanged. No commits/push/deploy. No Project1 edits. Keep user IDs/passwords/history. No drop/truncate/reset or fabricated stock.

## Phases

Reopened after user completion audit (24 November 2026 15:49): cumulative 28 B scenarios do not establish complete A+B acceptance. Overall remains PARTIAL. Close the unified creation form/type coverage gap and map every A TEST01–15 criterion to actual evidence; do not infer coverage from test totals.

- [ ] Complete B §17 authorized unified creation/dynamic forms beyond refill/replenishment, preserving domain authorization and accepted UX.
- [ ] Complete criterion-level A TEST01–15 and B acceptance traceability, execute missing regression/exception cases, and only then sign off both documents.

- [x] Read attachments and reconcile scope; archive complete received text (B ends mid-sentence at §24).
- [x] Read-only role/auth and inventory contract investigation.
- [x] Database/runtime preflight, source backup, verified isolated restore, baseline snapshot.
- [x] Impact matrix, target permission matrix, migration/recovery plan, acceptance/UC mapping.
- [x] Role migration and effective authorization, sessions, future schedules/open-work reconciliation.
- [x] Branch supply config, allocation/ledger/reservation/usage schema and safe inventory writers.
- [x] Shift start/end supply, assignment/reassignment/cancel and cleaning completion integration.
- [x] Structured unified request routing, refill/replenishment, durable/equipment remediation/reinspection and readiness.
- [x] Cross-branch transfer workflow and negative authorization.
- [x] Role UI, supply screens, request workspace, dashboard/action center, notifications and reports.
- [x] Isolated database/API/concurrency/migration tests; frontend real-API E2E and regression; builds.
- [x] Integration review, defects fixed, docs and scoped completion report; TEST ready. Independent agent review disabled at user request; test limitations documented.

## Confirmed user decisions — 24 November 2026

1. HK shift stays open for unfinished assigned work after scheduled end. End button only when work is complete; manual end returns unused supplies. New assignments/refill require the scheduled window.
2. Storekeeper processes all material requests remaining within the hotel; OM is not an intermediate approver.
3. Missing durable objects: only objects already assigned to the room.
4. Exactly one active warehouse per branch. TEST audit permits deactivating only an entirely unused extra warehouse; retain all historical rows and stock.
5. User supplied the remainder of sections 24–32, including 28 runtime E2E workflows and full TEST implementation authorization. See continuation acceptance record.

No business approval is pending. No Production migration or deployment is authorized for P0.

## Integration checkpoint — 24 November 2026, afternoon

- Implemented role consolidation, supply schema/services, shift allocation/reservation/consumption/return, structured requests, remediation/readiness, transfer and low-stock paths. Runtime verification remains in progress; implementation is not a PASS claim.
- TEST migrations 1–9 applied and validated: schema 3.0.0, 49 tables, 205 foreign keys, 97 checks, 38 triggers; repeated rollback validation passed. Production unchanged.
- Backend build: passed, zero warnings/errors. Unit tests: 74 passed, 3 existing integration tests skipped. Frontend final integration build needs recollection.
- Dedicated Development API PID 46736 at port 5774 uses a copied runtime and TEST database. Production PID 40444/30516 remains needed. Dedicated frontend is not yet started.
- User explicitly approved new synthetic TEST credentials. Twenty random credentials generated only in ACL-restricted ignored private artifacts; no existing password/account changed.
- Fresh TEST backup verified before fixture. Initial room inserts were rolled back by readiness trigger; corrected fixture starts all rooms DIRTY and keeps genuine inspection requirements. Fixture metadata now committed; API bootstrap in progress.
- Fixture target verification checks listener PID and actual SQL session database. Health alone does not query SQL; invalid opaque-token session lookup safely warms the DB pool.
- Runtime adapter and room-remediation scenarios delegated; UI gap review delegated. Full 28 E2E, concurrency and inventory reconciliation remain NOT_TESTED until executed.
- Documentation aligns current contracts and records missing runtime evidence. Remaining A-scope UI/report/action-center gaps are under review.

## Investigation results

### Runtime checkpoint 24 November, 14:50

- TEST API port 5774 (PID 32420) and TEST frontend 5174 (PID 48136) remain needed; production processes unchanged. A consolidated backend rebuild is in progress before the next TEST-only restart.
- Fixture bootstrap completed with 20 new TEST-only accounts. Credentials remain ACL-restricted; approval was received explicitly. Existing credentials unchanged.
- Distinct runtime groups verified so far: 01, 02, 03, 04, 05, 06, 08, 09, 10, 11, 12, 13, 14, 15, 16, 26, 27, 28. Evidence: runtime-start-retry, runtime-integration-2, runtime-migration, runtime-requests-3 JSON artifacts. Individual coverage limitations remain; this is not full acceptance.
- Remaining runtime groups: 07 and 17–25. Room document fulfillment guard corrected; parent/child requests implemented and awaiting integrated runtime validation. Existing interrupted TEST room requests must recover through genuine APIs, preserving history.
- Real browser testing found mobile overflow and transient API restart errors. Dedicated UI worker is fixing and rerunning; browser acceptance is not yet passed.
- Build sandbox process 32552 stopped after prolonged no-output stall; exact same sequential build retried outside sandbox. No unrelated process stopped.
- Final independent security review, migration password/history baseline comparison, legacy open cleaning reconciliation and full A-scope verification remain outstanding.

- Compact Access rechecks DB grants; opaque sessions can be revoked without changing passwords. Future-dated HasRole filter needs alignment. Role migration must preserve old role assignments/history and template history.
- SchemaGate hardcodes 2.1.0 / 40 tables,160 FKs,82 checks,27 triggers. Extend version contract deliberately, keep legacy supported for regression; never weaken schema validation indiscriminately.
- Existing inventory supports transfer request/dispatch/partial receipts and Serializable transactions, but same-branch guards, approval permission and notification FK need scoped redesign.
- Existing remediation requires Head and internal technicians; replace workflow, not global role-string substitution.

## Ownership

Parent: integration, source audit/DB backup/clone, plan/contracts, shared schema/mapping coordination and tests. Investigation agents completed read-only. Implementation ownership to be assigned after database preflight and contract lock.

## Execution evidence

- COPY_ONLY / CHECKSUM backup, RESTORE VERIFYONLY and actual restore succeeded: `HotelOpsCompactWorkdayTestP0261124`; metadata `artifacts/workday/backup-HotelOpsCompactDB.json`.
- Read-only clone preflight: `artifacts/p0-workflow/preflight.json` (2.1.0, 40 tables, 160 FKs, 82 checks, 27 triggers).
- Process reconciliation: Production frontend PID30516 port5080 and API PID40444 are still needed and untouched. Other repo/IDE processes were not started for this task and left alone. No new test server yet.
- Role worker completed code/matrix/static contracts; SQL validation failed SSPI then login timeout, must retry on clone before declaring tested.
- Workers: p0_cross_branch_transfer owns DocumentService + transfer paths; p0_supply_domain owns new supply schema/entities/services; p0_work_authorization owns generic WorkService/maintenance/IR guards. Parent owns RoomIssue*, readiness, Workday integration, schema registration, UI/integration tests.

## Integration checkpoint — 24 November 2026, 15:45 Asia/Saigon

- User disabled subagents; continued integration in the primary agent only. No approval pending.
- Backend final build passed, zero warnings/errors. Tests: 98 passed, 3 existing integration cases skipped. Frontend build passed; final text-label rebuild collected separately.
- TEST API PID 15136/5774, frontend PID 48136/5174. Production PID 40444/5769 and 30516/5080 unchanged. Finite build/test commands finish; no new background helpers remain.
- All 28 E2E groups now have VERIFIED cumulative evidence, 1,349 selected assertions. Five supplemental groups cover stale handler, legacy supplies, assets/OM tasks, populated reports and config/assignment race. Cross-branch transfer eight groups passed.
- Browser 100/100 route/layout checks passed; six Action Center/report checks passed with actual TEST transfer/stocktake rows. This is not interactive coverage of every form.
- Fresh verified TEST backup before synthetic legacy attendance/task creation. Legacy fixture adds only clearly marked simulation rows; no original history or stock rewritten. Runtime confirms shortage rollback, issue/reserve, preserved timestamps, after-hours completion and one-time return.
- Preservation baseline remains READ_ONLY; all original business/history primary keys retained, original credentials unchanged. Role grants intentionally replaced and archived by migration.
- Reports/docs: docs/requirements/p0-261124/implementation-report.md and validation-evidence.md contain current results, contracts, scope and remaining validation limitations. Earlier NOT_TESTED checkpoints are historical.
- No Production migration/deploy, commits, push or merge. Source implemented and scoped TEST acceptance collected; three skipped integration cases, exhaustive UI interactions, long-running load/soak and fresh-clone single-run acceptance are not claimed.
