# Legacy cleaning initial allocation and UI

Implemented source; runtime/build not run (integration owner controls these).

- Existing supervisor reconciliation now initializes missing supplies only when the actual WORKING HK attendance, cleaning actual start and active assignment all predate the persisted P0_ROLE_MIGRATION/261124-v1/APPLIED audit. Unknown cutover fails closed. Closed or previously initialized attendance cannot be initialized again.
- Reuses Start's extracted Allocate helper: creates immutable current configuration snapshot, posts genuine INITIAL issue using existing stock ledger and supply triggers, and then reserves for the room within the existing serializable transaction and supply lock. Any shortage or subsequent reservation failure rolls back everything. Does not infer historical consumption, change timestamps, invent stock or change database schema.
- Supervisor must be on duty; old employee attendance may remain open after scheduled end to finish existing work. Explicit LEGACY_INITIAL_ISSUED audit links actor, reason, assignment, work, cutoff and current-snapshot policy. Unique attendance allocation and initial document constraints plus work operation key protect retries.
- Added capability-gated drawer button and modal with required reason, physical receipt confirmation guidance, no success on failure, stable retry key and Vietnamese error messages.
- Added six policy tests for strict cutoff boundary and missing evidence. Existing API scenario syntax validated with node --check (exit 0); actual legacy initial issue scenario still requires a genuine pre-migration TEST fixture and runtime verification.

Files: StaffSupplyService.cs, new StaffSupplyService.Legacy.cs, WorkService.CleaningReconciliation.cs, new frontend/src/compact/cleaning-supply-reconciliation.tsx, work.tsx, p0-error-messages.ts, CleaningReconciliationTests.cs.

Integration owner must build/test, test no-allocation legacy success, insufficient stock rollback including allocation absence, reservation shortage rollback, concurrent retries, foreign branch denial, post-cutover refusal, closed allocation refusal, completing and returning remaining supplies. No DB writes/process changes/deploy performed.

No unresolved business question. Capability currently reflects original broad reconciliation eligibility; exact missing-allocation cutoff is validated server-side on submit. A later UI refinement may expose detailed eligibility if desired.
