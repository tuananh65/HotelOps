# Commit safety evidence

COMMIT_FAULT_RESULTS.json: actual SQL producer/BatchAsync, simulated provider DbException or cancellation before/after commit. Before variants return unavailable and delta [0,0,0,0,0]; after variants resolve durable success with [1,1,1,1,1]. Fresh-context retry retains original key/body/If-Match and ends exactly once. Count order Assignment/Audit/Operation/Publication/Intent. Auth adapters in this component are doubles, never HTTP authorization evidence. Actual network partition and durable-recovery DB outage not proven.

PHYSICAL_GUARD_RESULTS.json: valid async, sync denial, missing persisted intent/audit, ExecuteUpdate source revision, mutation after prior validation, savepoint restore, canceled precommit, and unsupported raw provider canary. Identity memory never serves as persistence proof; current proof is rerun at commit.

BOUNDARY_RESULTS.json adds actual new assignment mutation through external connection, retained without corresponding audit/publication; this is a reproduced limitation, not PASS for complete C01. Wrong physical transaction remains rejected in full-suite seven-case guard-proof. Actor and recipient role/branch/source-read authorization tested through actual API separately from component doubles.

Mandatory stage failure rolls back; durable result conflict409, stale412, same-key races and competing commands, explicit delivery retry/claim/lease restart stay in live suite. Default-off and actual API/gateway restart have separate receipts. No result inferred from timeout, memory-only flag or aggregated focused reruns.
