# FASTTRACK-142A-ACCELERATOR — Checkpoint85

NEW_COMPLETE_VERIFIED=[]; TOTAL_COMPLETE_VERIFIED=19/144; PARTIAL=125/144.

Implemented FE11 notification retention consistency: submission, verification, decline, assignment, plan/cancel, series-stop and reopen dispatchers use the same configurable UTC retention window as inbox. Discovery filters event timestamps before pagination. Delivery rechecks time after recipient locks, before any inbox write, returning EVENT_OUTSIDE_RETENTION for expired/future events. Defaults remain365 days. Receipt/audit/source authorization, transaction and idempotency guards remain intact. No schema, generated entity, permission, FK or trigger changes; no JOB06 deletion worker enabled.

Direct verification:

- Backend/ModelProbe builds PASS,0 warnings/errors. Solution test exits0 without a test project. Regression652 PASS/12 SKIP, including7 boundary/configuration tests. Product frontend unchanged; unit/typecheck/build not rerun. Existing canonical frontend used by real FE11 browser trial.
- [Full FE11 trial2](checkpoint85-trial2-runtime.json) executes existing create/assign/accept/start/progress/pause/resume/submit/verify/reopen and recurring-task coverage, then deletes only synthetic inbox records. Across30/365-day settings,14 dispatcher discovery checks return no expired events;32 direct expired-event calls are denied. All7 event families covered; source task/audit/submission/verification/reopen fingerprints unchanged and no deleted notification recreated.
- [Read-only discovery probe](checkpoint85-discovery.json):28 checks, all7 dispatchers × two retention settings × current/future cases. Current pending sources remain discoverable; future sources are excluded. This prevents an always-empty discovery query from passing only negative tests.
- Trial1 passed but direct-call coverage omitted submission/verification because those receipt families had no inbox records in that fixture. Retain [trial1 evidence](checkpoint85-trial1-runtime.json); trial2 adds immutable source receipts and requires all7 families. Do not count trial1's zero-call fields as verified direct delivery.

Both identity-verified clones and backups removed; source schema restored after cloning; owned host/private evidence cleanup verified. Cleanup initially waited for a transient SQL session to disappear; no session was killed and no shared DB touched. Clone GUIDs were re-read with matching run ownership before runtime, rather than using initial provisional receipt values. Runtime reports intentionally show cleanupRequiresDatabaseDrop; separate clone cleanup receipts prove final disposal.

Remaining: FE05/provisional-cleaning delivery needs equivalent retention review before notification purge can be enabled. Archive-before-purge remains design-only; finite archive policy and receipt-linked provenance storage migration remain necessary. UC129 branch/department inbox scope/filter and remaining source routes were inspected but not implemented in this batch. No whole UC completion is claimed from retention regression.

Next: finish source-backed notification scope/routing and remaining retention producers; continue FE11/FE13 lifecycle gaps without waiting on archive policy. Keep archive and JOB06 activation fail-closed until their separate acceptance gates pass.
