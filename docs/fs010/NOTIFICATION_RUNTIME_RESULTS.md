# Notification runtime

Provisional only; no accepted shared dispatcher. Live suite verifies exact persisted assignee, duplicate-key and competing requests, stage failure total rollback, inbox failure after successful commit with durable retry, exact-publication claim and expired-lease restart, no duplicate inbox, recipient role revocation, inactive/out-of-branch and supersession terminal refusal, OWN source reads and Monitor denial.

Evidence: per-run LIVE_RESULTS JSON checks, COMMIT_FAULT_RESULTS.json, PHYSICAL_GUARD_RESULTS.json. MODEL_SQL parity is asserted inside full live terminal-policy case. Base B01 component result NOTIFICATION_SQL_RESULTS.json checks six FK/unique/check scenarios, all transaction-rolled-back with retained baseline rows. Candidate schema/constraints remain local proposal B01/B02, no production migration.

FS010 scripts are retargeted copies with exact database/local-instance/owner interlocks. Forward lifecycle/proposal installer is repeatable; proposal refuses partial tables; populated recovery refusal was executed (51917), with no destructive recovery/drop. No claim of tested recovery/drop or accepted shared schema compatibility.

SCHEMA_SAFETY_RESULTS.json: proposal rerun twice PASS, partial-package refusal51891 verified using transaction-rolled-back temporary rename, original table restored, populated recovery refusal51917, retained rows75 / delta0. No destructive recovery/drop.
