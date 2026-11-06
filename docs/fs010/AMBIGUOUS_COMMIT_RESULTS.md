# Commit ambiguity and cancellation

COMMIT_FAULT_RESULTS.json: COMPONENT_REAL_SQL, actual SQL Server transaction and real producer/BatchAsync. Test-only transaction interceptor throws a DbException before physical commit or after successful provider commit, or cancels at those points. Authentication/session/permission adapters are doubles in this component test; no authorization acceptance is inferred.

Before-commit and cancel-before: result Unavailable; Assignment/Audit/Operation/Publication/Intent delta [0,0,0,0,0]. After-commit and cancel-after: durable recovery Success; deltas [1,1,1,1,1]. Fresh-context retry uses original key/body/If-Match; final deltas [1,1,1,1,1] for every case.

Recovery runs only after BatchAsync transaction disposal, uses an independent bounded 10s token, revalidates session/branch/permission and source, locks/finds original operation, compares original fingerprint, validates durable proof and result. Recovery-only branch cannot insert a missing operation. Missing, incomplete, unreachable or inconsistent evidence remains unavailable/unresolved; timeout/memory state never establishes success. A later explicit retry may create the command if rollback left no operation. Existing completed operations remain immutable.

Only UC029 opts into preserveCommitErrors in BatchAsync, so a commit-time SQL timeout is not silently converted before recovery. Other modules retain the old default. No schema/role/permission changes.

Limitations: simulated provider acknowledgement loss, not an actual network partition/SQL service termination. Fresh DbContext is not itself process restart; separate ACTUAL_RESTART_RESULTS.json records that test if executed. HTTP client disconnect may prevent delivery of an otherwise durable recovered response. Recovery database-unavailable branch has no successful outcome claim.

Actual application restart proof: ACTUAL_RESTART_RESULTS.json, LIVE_API_SQL1/1 PASS, original command replay after stopping/restarting owned API/gateway/Vite; full source+notification snapshots unchanged. This supplements, not relabels, provider-fault component tests.
