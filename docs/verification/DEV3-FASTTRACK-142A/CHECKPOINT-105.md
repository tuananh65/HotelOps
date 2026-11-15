# FASTTRACK-142A checkpoint105 — requester withdrawal

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=b1ca49203aef1d7a40ae2ee412372ebae18255f7
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

UC123/GBR-154 now provides canonical requester Withdraw. Current scoped IR_VIEW is required; requester identity and Pending Acceptance are checked independently of department processing authority. A mapped manager cannot withdraw someone else's request. HTTPS/session/CSRF and canonical strong If-Match remain required. Serializable transaction locks the request against Accept/Route, checks the loaded version, and commits Cancelled, cancellation_reason, WITHDRAW event, audit and notification intent together. No new status, generated EF change, inferred management relationship or permission was introduced.

The additive withdrawal outbox records eligible receiving-department manager recipients. Eligibility uses the approved explicit user–branch–department mapping and current DEPT_MANAGER/IR_PROCESS. Delivery independently checks current IR_VIEW and mapping, is idempotent and retains the existing bounded notification visibility/delivery window. Opening the notification rechecks source authority. The existing notification worker includes withdrawal scanning; this trial invokes the production delivery service directly, **not the background scheduler**. Physical outbox/event retention remains part of the pending retention work; no purge activation or indefinite-retention policy is approved here.

The canonical page requires a reason, prevents duplicate submission, clears uncertain state without replay, and reloads the history after success. The backend uses existing cancellation_reason/event_note storage; the guarded SQL installer verifies storage supports the 4000-character limit. The outbox is installed twice to prove idempotency only on independently verified disposable clones.

## Direct evidence

[Final trial3](checkpoint105-trial3-runtime.json) success=true: real Edge → trusted HTTPS → disposable SQL withdrawal, persisted cancellation reason/event/audit, refreshed Cancelled timeline, missing CSRF/If-Match, blank reason, wrong requester, Accepted-state rejection, revoked branch access, stale ETag, and injected outbox failure rolling back status/reason/event/audit/version. Concurrent public POSTs with one version yield one200 and one412 and one event. Delivery with revoked mapping is denied; restored mapping delivers once, repeat returns ALREADY_DELIVERED; source opens200 then returns404 after mapping revocation.

[Trial3 cleanup](checkpoint105-trial3-cleanup.json) proves DROPPED_AND_BACKUP_REMOVED. Runtime cleanup=false delegates whole-clone disposal to that receipt. [Trial2](checkpoint105-trial2-runtime.json) also passed before adding the concurrent POST case. [Trial1](checkpoint105-trial1-runtime.json) passed browser withdrawal but failed an overly broad audit-count assertion that included browser login/logout audits; the assertion was narrowed to the exact request/actor/WITHDRAW audit. All three cleanup receipts confirm clone and backup removal. No shared or production database was mutated.

REGRESSION_STATUS=solution build PASS0 warnings/errors; solution test exits0 without discovering a test project; actual FE13 backend60 PASS; existing history browser21 PASS plus new withdrawal browser2 PASS; frontend typecheck and production build PASS. Initial sandbox build could not read local NuGet.Config; authorized build succeeded. ModelProbe retains3 pre-existing EF1002 fixture warnings; Vite use-client/large-chunk warnings remain. Dependencies unchanged, npm ci not rerun.

WORKFLOWS_COMPLETED=[UC123 requester Pending Acceptance → Withdraw(reason) → Cancelled/history/audit; durable mapped-manager notification delivery and authorized source opening]
UI_API_SQL_E2E_VERIFIED=[withdrawal canonical page and refreshed Cancelled timeline; transaction rollback; concurrent200/412]
EXACT_REMAINING_GAPS=[UC123 sent/received filter and remaining linked-work adapters; scheduled withdrawal worker acceptance; UC117/118 incident/material targets; UC114 creation; UC119 handover and UC120 completion; JOB06 archive/retention]
NEW_BLOCKERS=[]
NEXT_BATCH=[finish UC123 direction and target visibility contracts; close linked-work dependencies for UC117/118; implement UC120 closure when mandatory work/handover predicates are authoritative]
SPRINT_TIME_REMAINING=approximately21h12 at2026-11-15T20:38Z; original deadline2026-11-16T17:50Z unchanged.

Files changed: new WithdrawalController, WithdrawalService, WithdrawalDelivery and canonical WithdrawalPanel; HistoryPage, Program, notification worker/source resolver; narrow guarded SQL installer; focused backend/frontend tests and real browser/SQL scenario; existing clone runner/probe routing; matrix and these execution receipts. UC123 remains PARTIAL. This batch closes an operable missing business transition, not an entire UC; sprint remains active.
