# FASTTRACK-142A checkpoint93 — Accept to Assign and scheduled delivery

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=31e52dc0203e0821cb3eb0fcf0ae81765871fda5
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[UC116]
TOTAL_COMPLETE_VERIFIED=22/144
PARTIAL=122
SHARED_DB_MUTATION=NO

UC116 now offers and executes assignment after acceptance and has verified automatic requester notification. [Acceptance map](UC116_ACCEPTANCE.md) links each criterion to actual execution. UC115 remains PARTIAL: first assignment and SQL reassignment are implemented, while Route and full public reassignment acceptance remain.

## Implementation

AssignmentController exposes authenticated HTTPS GET/POST `/api/internal-requests/{id}/assignment`, strong ETag/If-Match and existing global CSRF. AssignmentService uses ordered actor/handler locks, serializable request/current-department authority, current session and permission checks, active handler membership plus current branch access. It uses existing assignment ACTIVE/REASSIGNED states and filtered unique index. Replacement ends the previous assignment, retains history, updates request row version/status, writes event/audit and deduplicated notification intents atomically. Pending requests must be accepted first; unchanged handler, stale versions and inconsistent assignment history are rejected.

Canonical AssignmentPanel appears in the accepted request's decision view, lists eligible handlers and open request counts, supports assignment/reassignment, removes editable state after errors and requires reload. Cancellation/session generation and a synchronous submit guard protect stale responses/double clicks. A missing accessible name found in browser verification was corrected before the passing final trials.

The additive `internal_request_assignment_outbox` preserves existing entities and mappings; migration has exact server/database/GUID/run guards and inspected assignment CHECK/unique-index prerequisites. No generated EF entity or EF migration changes. AssignmentDelivery validates committed event/assignment provenance, finite configured notification visibility window, current recipient IR_VIEW and source access, and retries idempotently. Hosted worker scans both decision and assignment intents; source opening revalidates permission. Physical outbox/notification retention still needs integration with the approved archive/retention design; no production seed/deployment is authorized by this checkpoint.

## Evidence and limits

WORKFLOWS_COMPLETED=[UC116 Accept with adjusted deadline/reason -> offer/select handler -> In Progress; UC116 Reject with reason; committed automatic requester/assignment notifications]
UI_API_SQL_E2E_VERIFIED=[trial4 canonical Accept-to-Assign and Reject; trial5 same browser flows with actual hosted worker enabled]

[Trial4](checkpoint93-trial4-runtime.json) success=true, cleanup=true: public browser CSRF/ETag/stale-version checks, committed state/event/audit/assignment, transaction rollback, duplicate handling, concurrent replacement, historical assignment preservation, wrong department/inactive/revoked authority, idempotent notification and authorized source resolution. [Trial5](checkpoint93-trial5-runtime.json) success=true, cleanup=true: no direct delivery calls; real worker automatically delivers Accept+Assign (two notices) and Reject (one notice), with no duplicates after another scan. Trusted HTTPS certificate validation stays enabled. No owned host remains after runner cleanup.

REGRESSION_STATUS=Backend solution build PASS zero warnings/errors; required solution test exits0 without discovered tests; focused FE13 regression23 PASS; frontend13 PASS; TypeScript and production build PASS. Existing Vite use-client/chunk warnings remain. ModelProbe clean compile reports the three pre-existing credential-scenario EF1002 warnings; no new assignment warning. Initial probe span/await compile error, fixture row-version mismatch and selector timeout were fixed and rerun; failed trials2/3 are not represented as PASS. Dependency installation and broad unrelated regression were not repeated.

CHANGED_FILES=AssignmentController/Service/Delivery; AssignmentPanel and DecisionPanel integration; notification worker/source resolver/DI/error mapping; disposable outbox SQL; assignment/worker/decision probes and browser script; focused backend/frontend tests; matrix and evidence receipts. No unrelated source deletion.
EXACT_REMAINING_GAPS=[UC115 pending Route with target DH/requester notifications and authority handover; public reassignment alternatives; UC114 creation; UC117 progress and UC120 completion; physical outbox retention/JOB06 archive]
NEW_BLOCKERS=[]
NEXT_BATCH=[complete UC115 Route and public reassignment acceptance; UC117-to-UC120 progress/completion workflow; UC118 linked operational work]
SPRINT_TIME_REMAINING=approximately23h10m at2026-11-15T18:40Z; deadline2026-11-16T17:50Z unchanged.

Sprint remains active. Git commit/push details are reported after delivery verification.
