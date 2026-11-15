# FASTTRACK-142A checkpoint112 — public closure and requester notification

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=db37b9d3703f0ac9ab0e1fbf750a503bfb85d0c0
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

## Implemented workflow

UC120 now has GET/POST `/api/internal-requests/{id}/completion` and a canonical history Complete panel. The read checks current session, independent IR_VIEW and exact receiving-department management authority; it returns request status/version, current capability and a generic blocker without exposing unauthorized target IDs/titles. Existing linked-work panels retain independent target read authorization. POST keeps the C111 transaction, current authority, CSRF, strict If-Match and required result validation. The form prevents duplicate submission, never automatically replays an uncertain write, clears stale capability, shows MSG08 and refreshes the history after success.

The existing internal-request hosted worker now delivers completion intents. Source validation binds outbox, COMPLETE event and closure receipt by request/event/time; recipient authorization requires current requester IR_VIEW. Receipt locking and per-event delivery identity prevent duplicate notifications. Discovery/delivery use the existing bounded retention window. Opening the notification independently revalidates current requester/source access. Notification Center now offers Open request for IR_CLOSED, returning to the persisted closure result.

## Direct evidence

[Final trial2](checkpoint112-trial2-runtime.json), success=true, proves real Edge -> trusted HTTPS -> disposable SQL -> hosted notification -> source opening:

- Required result, double click producing one COMPLETE event, persisted CLOSED/result, MSG08 and refreshed history.
- Actual worker delivery, Open request returning to the saved result, and no remaining Complete action on the closed request.
- Public CSRF400, absent If-Match428, blank result400, cross-branch read404/mutation403, revoked department mapping403, and IR_PROCESS without IR_VIEW404.
- An explicitly mandatory synthetic material link with physical COMPLETED remains blocked: read capability false, unresolved-material code, POST409. The fixture removes only its own negative-case link before the separate no-material closure scenario; this is not evidence of completing a material-linked workflow.
- Delivery replay returns ALREADY_DELIVERED. Revoked IR_VIEW blocks notification source404 and delivery; restored scope opens the source. Stale public completion returns412.

Trial1 passed the initial closure/delivery/browser sequence. Trial2 adds the independent read, branch and material-boundary cases. Both owned clones and backups were removed, as their cleanup receipts show. The top-level runtime scope label is inherited generic text; completionCheck/completionBrowser entries identify the actual public E2E coverage. C110 prerequisite cases and C111 closure fault rollback/concurrent200/412 are historical direct evidence, reused where their implementation is unchanged.

## Verification and remaining acceptance

- Backend solution build PASS, zero warnings/errors; InternalRequest/Notification regression179 PASS. Solution-level test exits0 without a test project and is not counted as coverage.
- Frontend typecheck/production build PASS after correcting TS18048 in the history-refresh callback. History suites33 PASS, notification transport/source suites5 PASS. Existing Vite use-client and >500kB bundle warnings remain.
- ModelProbe retains three existing EF1002 credential-fixture warnings. Dependencies unchanged; npm ci not repeated.
- No generated entity, shared database or production deployment changes. The runner reuses C111's guarded additive capability on disposable clones only.

WORKFLOWS_COMPLETED=[non-material request closure -> persisted result -> hosted requester notification -> authorized source opening]
UI_API_SQL_E2E_VERIFIED=[above sequence in trial2]
EXACT_REMAINING_GAPS=[UC120 mandatory-material terminal mapping and linked-material visibility; complete legacy classification workflow; full linked-work blocker acceptance; UC119 receipt read/delivery decisions; UC121 reopen]
NEW_BLOCKERS=[]
NEXT_BATCH=[UC121 reopening within approved window and current authority; approved material closure mapping when supplied; handover notification completion when receipt access decision arrives]

UC120 remains PARTIAL. A successful no-material workflow and a fail-closed material negative test do not prove the whole UC. No new whole-UC completion or production readiness is claimed. Sprint remains active under the original deadline.
