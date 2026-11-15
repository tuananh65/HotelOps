# FASTTRACK-142A checkpoint109 — handover transaction and approved direction contract

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=b54f87a4c2c01c10cf50d7676ca693338a7230ff
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

## Business behavior and direct evidence

UC119 now supports canonical history -> Submit handover -> receiver Reject with reason -> sender resubmit -> receiver Acknowledge. Pending acknowledgment retains the sender as sole current handler; rejection changes no assignment; acknowledgment ends the original assignment and creates exactly one active receiver assignment. Request status remains IN_PROGRESS. Event, audit, durable notification intent and request version commit atomically. Manager reassignment cannot bypass a pending handover. Current session, IR_VIEW, handler/receiver eligibility, branch/department, CSRF and If-Match remain mandatory.

[Trial5](checkpoint109-trial5-runtime.json) proves this browser -> trusted HTTPS -> disposable SQL sequence, including current-handler-only submission, revoked receiver role/branch, invalid department, required summary/rejection reason, duplicate pending rejection, changed receiving department, rollback after injected submit/ack outbox failure, stale decision and concurrent acknowledgments (one200/one412). The rollback assertions include original assignment, event, audit and request version. The final reverse handover also leaves exactly one active handler. Durable intent deduplicates a sender who is also an eligible manager. This proves persisted intents, not delivered notifications.

The additive schema preserves generated entities and current request/assignment state. Eleven restrictive FKs, three CHECK constraints and the pending-handover unique index are verified. Exact instance/database/GUID/ownership guards precede mutation; second installation verifies idempotency. No shared or production schema was changed. Missing or incompatible handover capability fails closed.

Trial1 failed on filtered-index SET options, now explicitly configured. Trials2–4 failed the browser selector for New handler; trial4 confirmed a valid authorized response. Adding an explicit accessible label fixed the selector. All failures are retained. All five cleanup receipts confirm owned clone and backup removal. Runtime cleanup=false delegates to those separate cleanup receipts. The inherited top-level scope label is generic; handoverCheck and handoverBrowser entries identify the actual public HTTP/browser evidence.

## UC123 decision reconciliation

The latest approved Sent/Received decision matches the existing implementation and [C107 evidence](CHECKPOINT-107.md): Sent uses RequesterId; Received uses the unique current active handler OR current DEPT_MANAGER with IR_PROCESS and an explicit mapping to the current receiving department. Results are filtered through independent IR_VIEW, current session and branch scope before counting/pagination. No authority is inferred from User.DepartmentId or Department.ManagerUserId. C107 trial3 directly verified cross-branch exclusion, mapping/grant revocation, rerouting and handler replacement. Those are historical runtime results, not rerun in C109. A focused regression now additionally makes one request simultaneously requester-owned, currently handled and manager-mapped, verifying one row in each qualifying list.

## Verification and limits

- Backend solution build PASS, zero warnings/errors. Solution-level test exits0 without a test project; not counted as coverage.
- Actual InternalRequest regression suite72 PASS; after extending overlap coverage, Direction suite4 PASS.
- Frontend typecheck and production build PASS. Existing React Router use-client and >500kB chunk warnings remain. Dependencies unchanged; npm ci not repeated.
- New handover browser regression2 PASS: required input, accessible receiver selection, CSRF/ETag, double click, stale write no replay, and revoked read removing private receipt/actions. These use mock HTTP and are separate from trial5 real SQL evidence.
- Existing frontend history suites29 PASS with the handover panel mounted.
- A test-append command initially used an incorrect relative path; it changed no file. The corrected append and focused new tests succeeded.

UC119 remains PARTIAL: designated non-handler receipt read scope and former-sender receipt read scope still require their pending decisions; notification delivery/source authorization is not yet enabled. Existing request IR_VIEW is not expanded. UC123 remains PARTIAL for material/legacy linked-work visibility. No new whole UC is claimed complete, and no production-readiness claim is made.

WORKFLOWS_VERIFIED=[handover submit -> reject -> resubmit -> acknowledge with sole-handler persistence, under existing authorized request-read scope]
EXACT_REMAINING_GAPS=[UC119 receipt-only access and notification delivery; UC120 completion predicate; UC117/118/123 material and legacy linked-work visibility]
NEW_BLOCKERS=[]
SPRINT_TIME_REMAINING=approximately20h08 at2026-11-15T21:42Z; deadline2026-11-16T17:50Z unchanged.
NEXT_BATCH=[UC120 completion guards and result persistence; UC119 notification delivery after receipt access decision; approved material visibility adapter]

This checkpoint continues the existing sprint and does not reset its deadline or claim the full mission complete.
