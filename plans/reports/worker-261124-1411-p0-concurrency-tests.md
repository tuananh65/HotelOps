# P0 concurrency and migration scenarios

Implemented, syntax verified; runtime NOT_RUN to avoid concurrent mutation with the integration owner.

Files:
- `scripts/verification/p0-runtime-concurrency-scenarios.mjs`
- `scripts/verification/p0-runtime-migration-audit.ps1`

Integration: import `concurrencyScenarios` and merge `concurrencyScenarios({probe,branch,fields,allocations})` into the parent's adapter scenarios. Adapter was not edited.

Cases:
- 01: real migration audit before snapshot, retained assignments/memberships/schedules/users, revoked old sessions, eight active actors, branch access, authenticated role reads. Password preservation still requires the owner's pre-migration baseline comparison; helper never reads password hashes.
- 07: consumes real cleaning kits using completion, paper inspection and reception checkout until one capacity-two kit remains; races assignments to P0_102 and P0_103, requires one success and one supply-specific conflict; cancels winner and restores spent staff quantities via authorized refill.
- 11: requests actual current stock + 1, proves issue conflict leaves inventory and balances unchanged, then reasoned rejection.
- 12: two different allocations/requests, two storekeepers, each requests more than half remaining inventory; requires one success and one STOCK_INSUFFICIENT conflict, exact stock/balance delta, rejects loser. Uses approved synthetic replenishment/receipt to restore warehouse quantity.
- 15: sets threshold 50, moves actual stock to 50 then 49 through approved requests, checks episode and recipient deduplication via read-only SQL, receives one and checks resolution. Restores original config; synthetic receipts restore original stock when originally above 50.
- 25: role-specific type lists, request ownership/cross-branch denial, approve/reject race, claim ownership, history persistence, correlation, notifications and actual linked child request evidence.

Prerequisites: E2E03/04 established active DVBV.hk / DVBV.hkPeer allocations; no pending refill; synthetic TEST receipt authorization; fixture within scheduled workday. Case07 needs P0_102 and P0_103 available and no unrelated reservations. Run room remediation scenarios before25 so parent-child history exists. All SQL probes are read-only and fixed to exact TEST database; per-case probes reject non-synthetic branches/work.

Checks: Node `--check` passed; PowerShell AST parse passed. No API/database mutation or runtime test was executed by this worker.

Potential acceptance gap: case25 deliberately fails if no `request_workflows.parent_request_work_id` evidence exists. Do not remove the assertion or claim parent-child coverage from unrelated root history. Migration password/user count before/after must be supplied by parent baseline validation.

Unresolved questions: none; remaining points are runtime verification and evidence gaps.
