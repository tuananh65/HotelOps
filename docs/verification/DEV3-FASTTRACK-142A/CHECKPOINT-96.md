# FASTTRACK-142A checkpoint96 — UC117 transactional progress

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=a67957df4c292576df56391305662db126d9dc31
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121
SHARED_DB_MUTATION=NO

## Implemented

UC117 now has a concrete progress transaction service, current-handler/manager authorization and narrowly additive disposable storage. GBR149 permits the current handler or exact mapped receiving Department Head to update In Progress requests. Current eligible IR_PROCESS grant remains required; eligible handler actor codes come from UC117. IR_VIEW, unrelated role grants, former assignments or a user's department alone do not confer processing authority. Department membership is checked only for handler eligibility; management still requires explicit mapping. Only the mapped manager can change deadline, with a nonblank reason. No planned-start field is invented and RequiredAt/submission time is not silently reinterpreted as one.

Progress snapshot, request row_version/deadline, event, audit and notification intent commit together under Serializable with existing user/request lock order. Progress0–100 never closes the request. Reaching100 from another percentage creates a durable requester intent; editing an already100 snapshot does not create another readiness intent. Snapshot retains result/note, and audit retains before/after data. Text is limited to the actual new nvarchar(4000) persistence bounds. Percent uses integer representation consistent with existing task progress.

This service is not registered as a public API yet: delivery/source authorization, read DTO/UI, linked-work projection and attachments must be integrated next. No public endpoint is claimed tested or complete.

## Schema and migration boundary

`InternalRequestProgressIsolated.sql` checks exact server/database/GUID/run identity and the existing request row_version capability. It creates only `internal_request_progress` and `internal_request_progress_outbox`, with trusted no-cascade FKs and0–100 CHECK. Idempotent installation rejects unexpected pre-existing objects. No generated EF entity, EF migration, shared DB or original request state constraint changes. The existing request row_version remains authoritative; no second workflow state or concurrency token is introduced.

The SQL script is a disposable trial migration, not a production migration. Failed installation rolls back with XACT_ABORT. Synthetic trial cleanup deletes only owned progress/intents before owned events and restores the fixture; schema remains for subsequent trials. Rollback of a deployed schema would require proving it empty or explicitly migrating its data first; do not drop tables with records. Archive/retention is still pending: delivery should reuse the configured finite notification window, and this checkpoint does not authorize indefinite storage or production deployment. No shared/production seed.

## Evidence and remaining work

Execution artifacts: [final SQL trial](checkpoint96-trial3-runtime.json), [backend build](checkpoint96-backend-build.txt), [40 tests](checkpoint96-backend-tests.txt), [probe build and warnings](checkpoint96-probe-build.txt).

Final trial3: service→SQL PASS, cleanup=true. Idempotent schema, mapped manager update, current handler update, unmapped non-handler denial, handler deadline403, revoked role, wrong department, former handler denial, mandatory deadline reason, stale412, injected outbox rollback preserving snapshot/event/audit/version,100 staying In Progress, single readiness intent, two concurrent updates producing200/412, closed request denial. Surrounding authority/session/CSRF regression also runs, but it is not UC117 public API proof.

Solution build PASS0 warnings/errors. Required solution test exits0 without discovered tests. Focused backend40 PASS. ModelProbe build PASS with three existing EF1002 warnings in credential verification. An initial probe build failed CS4007 in a new assertion; separating the awaited value fixed it before any trial. No frontend changes or new frontend execution in this checkpoint.

WORKFLOWS_COMPLETED=[]
UI_API_SQL_E2E_VERIFIED=NO_NEW_UC117_PUBLIC_FLOW
EXACT_REMAINING_GAPS=[UC117 delivery/source authorization; API/read DTO/canonical UI; attachments and linked work; UC120 closure guards; UC118 linked work; JOB06 archive]
NEW_BLOCKERS=[]
NEXT_BATCH=[complete UC117 public progress and readiness notification; attach existing secure evidence/linked-work projection; UC120 completion]
SPRINT_TIME_REMAINING=approximately22h41m at2026-11-15T19:09Z; original deadline2026-11-16T17:50Z unchanged.

CHANGED_FILES=InternalRequestProgressAuthority.cs; InternalRequestProgressService.cs; InternalRequestProgressTests.cs; InternalRequestProgressIsolated.sql; SprintInternalRequestProgressScenario.cs; SprintDepartmentAuthorityScenario.cs; UC matrix and checkpoint evidence. Sprint remains active.
