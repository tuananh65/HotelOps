# FASTTRACK-142A checkpoint97 — public progress and readiness notification

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=5a6d4e7a9aea164dd4de14783e594882ab20076d
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121
SHARED_DB_MUTATION=NO

## Implemented business behavior

UC117 now exposes HTTPS GET/POST `/api/internal-requests/{id}/progress` and a canonical progress/result panel within request history. GET checks current IR_VIEW independently of mutation authority, returns a scoped DTO and current version, and revalidates session/read scope before commit. POST preserves current session, global CSRF, strong If-Match and transaction authority. Percent is explicitly required in JSON, preventing an omitted field from silently resetting progress to0. The manager-only optional UTC deadline/reason fields remain server-authorized.

UI loads a validated snapshot, displays persisted percent/note/result/deadline, and shows edit/deadline controls only from server capabilities. It uses fresh CSRF and loaded version, excludes double-submit, cancels on authority changes and removes editable data after success or error until reload. Explicit textarea accessibility labels fix the discovered failure when editing prefilled note/result.100% is presented as readiness for review, never closure.

Readiness intents now participate in the existing hosted worker. Delivery uses a Serializable transaction, current recipient IR_VIEW, finite configured notification retention, durable receipt validation and retry deduplication. Notice text refers to the recorded100% event, not current closure. Notification source resolution rechecks the event/outbox provenance, actual recipient and current scoped read access, then constructs the canonical path. No permission broadening, shared schema change or worker-default change.

## Executed evidence

Final [trial5](checkpoint97-trial5-runtime.json) success=true and cleanup=true. Real Edge → trusted HTTPS → exact disposable SQL: canonical50% then100% updates and persisted result; state remains IN_PROGRESS; missing CSRF400, missing If-Match428, out-of-range400, omitted-percent400 and stale412. Actual worker delivers exactly two readiness transitions across service/browser portions and no duplicates after another scan. Public requester source resolves to the canonical request.

The same probe verifies delivery retry ALREADY_DELIVERED, revoked IR_VIEW denial for delivery/source/progress read, plus transaction rollback and concurrent200/412 from C96. It runs with real SQL sessions. The receipt's generic top-level scope label is inherited from the authority runner; embedded `progressBrowser` checks are the direct public-browser evidence. This is not full UC117 acceptance.

Trial1 aborted on SQL timeout before browser and did not produce a runtime JSON receipt. An independent DMV read also timed out. Subsequent read-only checks verified server `meita`, disposable DB ONLINE, and zero users/branches/requests/progress intents. No SQL service restart or shared mutation. Trial2 and trial3 failed at editing a prefilled textarea; both cleanup=true. Explicit labels fixed the issue; trial4 passed before the required-percent addition, and final trial5 includes that guard. Transient SQL timeout cause remains unproven.

REGRESSION_STATUS=backend build PASS0 warnings/errors; required solution test exits0 without discovered tests; focused backend41 PASS; frontend18 PASS; TypeScript/production build PASS. Existing Vite use-client/chunk and three credential-probe EF1002 warnings remain. PowerShell initially blocked npm.ps1; npm.cmd ran successfully without changing execution policy.

WORKFLOWS_COMPLETED=[UC117 progress/result update and requester readiness notification slice]
UI_API_SQL_E2E_VERIFIED=[canonical50→100 update; requester result reload; hosted readiness delivery and source; current read revocation guards]
EXACT_REMAINING_GAPS=[UC117 attachments and linked-work display; public handler/deadline-specific E2E; UC118 linked-work metadata; UC120 mandatory-work/handover/escalation closure; JOB06 archive/outbox retention]
NEW_BLOCKERS=[]
NEXT_BATCH=[UC117 secure attachments and linked-work projection; complete actor/deadline acceptance; UC120 completion]
SPRINT_TIME_REMAINING=approximately22h30m at2026-11-15T19:20Z; original deadline2026-11-16T17:50Z unchanged.

Changed files: ProgressController/Service/Delivery, worker/DI/source resolver, ProgressPanel and HistoryPage, frontend/backend regression tests, progress browser/probe integration, matrix and receipts. No generated entities or database migration changed. UC117 remains PARTIAL; sprint remains active.
