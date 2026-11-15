# FASTTRACK-142A checkpoint108 — complete internal-request intake

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=915aa417bbbe7cf27a3ba2e1e7f538ecea9cb227
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[UC114]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

## Newly verified workflow

UC114 now creates non-material Internal Requests through canonical UI, trusted HTTPS and disposable SQL. The independently IR_CREATE-authorized form collects branch, classified active request type, receiving department, title, description, priority, desired deadline in UTC+7, optional room/area and private attachments. The backend validates current actor/branch grants, active references, storage limits and attachment content. Desired deadline is stored in required_at; the receiving manager determines due_at during the existing decision workflow. The request enters PENDING_ACCEPTANCE with a CREATE event, audit and durable notification intents in one Serializable transaction. Success displays MSG09. A material type is rejected server-side with MATERIAL_REQUEST_REQUIRED and the UI directs the user to UC091 (MSG03), without inventing material-request mutation authority.

The existing unique request number incorporates a client submission UUID. Concurrent or uncertain same-key submissions cannot create duplicate requests; duplicate attempts return409 and the UI never automatically replays a write. Existing session and CSRF guards remain mandatory. Private attachment downloads intersect current IR_VIEW; IR_CREATE cannot substitute for read access. File rollback uses the existing commit-boundary-aware lifetime mechanism.

The existing notification worker now delivers intake outbox events. Delivery and source opening independently require current IR_VIEW and exact receiving-department DEPT_MANAGER/IR_PROCESS mapping. Per-event/recipient receipts prevent duplicates, and existing bounded notification retention applies. A newly added Open request action completes the inbox-to-source path.

## Acceptance evidence

[Final trial4](checkpoint108-trial4-runtime.json), success=true, proves:

- Browser login/return to canonical create form; material warning; required form fields and PDF attachment; double-submit creates one pending request; CREATE timeline and persisted audit.
- Foreign department, unclassified/inactive type, invalid priority, inactive receiving department, past deadline and MIME mismatch rejection; missing CSRF, revoked branch access and missing IR_CREATE rejection.
- Injected outbox failure rolls back request, event, audit, attachment row and owned file.
- Evidence download succeeds with source read access and returns404 after IR_VIEW revocation.
- Revoked department mapping denies delivery; restored mapping delivers once; repeated delivery returns ALREADY_DELIVERED; notification source rechecks mapping after delivery.
- Concurrent public POSTs with one submission ID return201/409 and persist one request.
- The running hosted worker automatically delivers the second committed intake event.
- A second real browser opens the creation notification, opens the authorized request, submits Accept with a deadline and persists ACCEPTED plus its decision event. This connects UC114 to already-verified UC115 rather than ending at an isolated form.

The top-level runtime scope label is inherited generic department-authority text. The intakeCheck, intakeBrowser and intakeNoticeBrowser entries identify the actual public E2E coverage. Runtime cleanup=false delegates whole-clone cleanup to the separate receipt; it is not an unremoved database.

[Trial1](checkpoint108-trial1-runtime.json) failed because the empty synthetic source had no priority catalog. The fixture now inserts a synthetic NORMAL priority only if absent. [Trial2](checkpoint108-trial2-runtime.json) exposed an EF projection/order translation defect in options; ordering before DTO projection fixed it. [Trial3](checkpoint108-trial3-runtime.json) passed intake, rollback, delivery and concurrency. Trial4 adds the actual hosted-worker and notification-to-Accept path plus further negative cases. All four clone/backup cleanup receipts are retained; trials3/4 also record owned evidence directory removal. No shared/prod SQL was mutated.

## Verification and limits

- `dotnet build HotelOps.slnx`: PASS, zero warnings/errors.
- `dotnet test HotelOps.slnx --no-restore`: exit0 without a test project; not counted as coverage.
- Actual regression project, InternalRequest/Notification/IncidentReadAccess filter:176 PASS.
- Frontend auth/router/history suites:206 PASS; notification transport/source suites:5 PASS. These are mock-HTTP regressions, separate from real SQL trial4.
- Frontend typecheck and production build:PASS. Existing React Router use-client and >500kB bundle warnings remain. ModelProbe retains3 existing EF1002 credential-fixture warnings. An initial probe compile CS4007 was corrected before trials; no failed execution is counted as PASS.
- Dependencies unchanged; npm ci was not repeated.

The additive schema trial creates a type-classification table and intake outbox with restrictive FKs, and extends the verified attachment discriminator CHECK without disabling FK/trigger protection. Exact server/database/GUID/run guards, baseline checks and idempotent second application were exercised on each clone. Generated EF entities and shared mappings were not changed. Existing real types are never automatically classified; shared/production rollout requires reviewed catalog classification and deployment. Missing capability fails closed. This is isolated acceptance, not production readiness.

WORKFLOWS_VERIFIED=[Create with attachment -> pending history -> durable/automatic notification -> source authorization -> Accept]
REMAINING_LIFECYCLE_GAPS=[UC119 handover; UC120 completion/closure; UC117/118/123 linked-material visibility and target authorization; other existing matrix gaps]
NEXT_THREE_CODING_TASKS=[UC119 explicit handover lifecycle; UC120 linked-work completion predicate; approved material-read adapter for UC117/118/123]

The prior UC123 follow-up records earlier results accurately; its statement that UC114 was unfinished is historical and is superseded by this checkpoint. No other whole UC is upgraded by these tests.
