# FASTTRACK-142A checkpoint113 — reopen closure provenance

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=4d667f3c12427a0683ec77d5cc9192316a0544c1
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

UC121/GBR153 requires reopening within SYS-PAR-18 with the last handler and preserved results/handovers. Added a transaction-bound closure source reader: it locks the current request, requires Closed, matching completed/closed timestamps, one matching closure/event/assignment/audit chain, and no active assignment. The last handler comes from the closure receipt, never the newest assignment. Caller-supplied resolved window days are checked against SQL UTC; missing/nonpositive policy fails closed. This is a prerequisite, not authorization or an exposed reopen operation. No schema or generated entity changed.

[Runtime trial1](checkpoint113-trial1-runtime.json) passed against a verified disposable clone. It verifies exact handler/result, a later ended assignment not replacing closure provenance, rejection without Serializable transaction, and rejection of mismatched event/result, state, active assignment and closing timestamp. Negative SQL changes roll back. Existing closure fault rollback and concurrent200/412 assertions also ran successfully. [Cleanup](checkpoint113-trial1-cleanup.json) confirms clone and backup removed.

Backend solution build PASS0 warnings/errors. Solution test exits0 without test discovery; actual InternalRequest regression74 PASS, including exact seven-day deadline, following tick, future timestamp, invalid window and extreme date tests. ModelProbe build PASS with three pre-existing EF1002 warnings in SprintCredentialScenario. Frontend unchanged; frontend tests/build/npm ci not rerun. No reopen API/browser claim.

UC121 remains PARTIAL. Permission mapping for requester/requesting Department Head was asked explicitly; no invented IR_REOPEN permission, grant or endpoint. Pending work: resolve SYS-PAR-18 deployment configuration; current actor/read/last-handler eligibility; atomic reopen event/audit/new assignment/outbox; notifications to last handler and authorized receiving managers; public UI/API concurrency and rollback acceptance. Historical closure receipts must stay immutable when creating the new assignment. Whole workflow completion is not established by this prerequisite.

Next highest-impact work is the UC121 authorized transaction once its permission contract is settled, plus remaining UC120 material and UC119 receipt-access decisions when supplied. Sprint remains active; deadline unchanged.
