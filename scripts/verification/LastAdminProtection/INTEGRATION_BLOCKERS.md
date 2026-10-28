# Remaining integration blockers - Task 43

Current application coverage is recorded in MUTATION_PATH_MATRIX.md and verified in
INTEGRATION_VERIFICATION.md. Task 41's zero-writer and Task 42's one-writer counts are historical.
HTTP admin reset, raw reset, legacy Apply AdminReset, Create and Profile now participate.

- Raw credential APIs remain trusted primitives: invariant protection does not validate actor
  IDs, reauthentication or identity confirmation. Future orchestration must supply and retain
  authorization; it must not bypass the existing fully gated HTTP workflow.
- External catalog/release/direct SQL/jobs/import writer contracts are not established. They
  may remove eligibility after guarded commit. No production schema or catalog is changed.
- FUTURE EXPIRY NOT GUARANTEED. Time-driven grant loss requires separately approved operations.
- Load, least-privilege SQL permissions and all external inverse-lock combinations remain
  unverified. Users TABLOCKX increases blocking. Negative acquisition/deadlock/cancellation
  never authorizes a write; transaction rollback and fresh retry are required.
- Deactivate and Role Revoke stay unopened. DEV1/DEV2 open-work, responsibility handover and
  lifecycle contracts/evidence remain prerequisites outside this release. Missing handoff
  material is not represented as read, approved or implemented.
- No permanent-lockout sentinel exists in current Auth/schema. No new state or policy invented.

One controlled Task 41-43 commit is authorized only after scoped verification passes. Receipts,
WAV, binaries, secrets, temporary database resources and unrelated untracked source stay outside
the exact allowlist. No push, merge or worktree changes. HotelOpsDB is not accessed.
