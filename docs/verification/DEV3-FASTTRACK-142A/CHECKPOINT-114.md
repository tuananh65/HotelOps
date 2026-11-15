# FASTTRACK-142A checkpoint114 — handover notifications

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=412fcca0f49e44ee4b3650f7c5e67224c0e1204a
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

Completed UC119 durable outbox delivery, hosted worker polling and canonical notification source navigation. Submission intents bind to the designated receiver and original sender assignment; acknowledgment intents bind to the decision event and sender/manager recipients. Delivery verifies current independent IR_VIEW, current branch/receiving department and existing handler eligibility or exact manager authority. Opening the source repeats those checks. Notification text is generic; no receipt-read scope expansion. Event/recipient locking and one outbox receipt preserve retry/concurrent delivery idempotency. Existing retention bounds apply. No schema changes or generated entity edits.

[Trial2](checkpoint114-trial2-runtime.json) directly verifies delivery fault rollback after notification creation, concurrent DELIVERED/ALREADY_DELIVERED, previous handler opening HTTPS source with independent read scope, IR_VIEW revocation denying both delivery and source, manager mapping revocation denying delivery, restored eligible manager delivery, and draining eligible sender/receiver/manager intents. Original handover browser and transactional assertions also pass.

[Trial4](checkpoint114-trial4-runtime.json) enables the real hosted worker. Real Edge performs Submit/Reject/Submit/Acknowledge against trusted HTTPS and disposable SQL, then opens a delivered handover notification through the canonical Open request button and reaches the current handover panel. Delivery receipt is persisted. No browser HTTPS bypass. This proves the existing-read-scope workflow, not newly proposed receipt-only access.

Trial1 failed because a trigger on an EF OUTPUT target prevented the intended injection; the test now injects failure on the raw outbox UPDATE after notification/recipient creation. Trial3 selected the default linked-work scenario because the new runner mode did not set the handover scenario flag; corrected to select the matching installed capabilities. Both failures are retained, not counted as PASS. All four cleanup receipts confirm owned clone and backup removal.

Validation: backend solution build PASS0 warnings/errors; solution test exits0 without test discovery; actual InternalRequest/Notification regression181 PASS. Frontend typecheck/build PASS, focused notification transport/source tests5 PASS. Initial frontend test invocation from repository root failed path resolution; rerun from frontend succeeded. ModelProbe has three existing EF1002 warnings; Vite retains use-client and large-bundle warnings. Dependencies unchanged; npm ci not rerun.

Changed implementation: HandoverDelivery, existing eligibility helper reuse, notification worker/DI/source resolver, NotificationCenter; verification adds hosted-notification browser, scenario and runner mode. No security guard removed. UC119 remains PARTIAL until limited receipt-read authority and full actor acceptance are settled. UC121 mutation permission and UC120 material closure mapping remain pending; sprint continues with those independent business gaps, not a whole-UC completion claim.
