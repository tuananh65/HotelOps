# Release limitations and deployment boundary

- Proof is private process memory only, with a reusable <=5 minute window. Multi-instance shared proof is not supported. Restart/instance switch/eviction fails closed and can require reauthentication again.
- Real network response-loss and provider commit-acknowledgement loss remain NOT VERIFIED. Post-SQL-commit exception/stale-retry tests do not prove wire delivery. No plaintext replay/idempotency cache or automatic retry rotation.
- Recovery is a separate explicitly authorized workflow/operation. Lost delivery requires authorized account inspection and a deliberately confirmed new reset with fresh token/proof as needed. Commit packaging does not approve recovery operations.
- This backend depends on the existing users.row_version schema patch and existing authentication/session schema. No new schema or migration is introduced by Task 34.
- Production deployment/rollout and migration approval remain separate. Git commits are NOT permission to execute production SQL, deploy binaries or change HotelOpsDB.
- Offline identity verification remains the administrator's attestation, not backend document verification.
- Proof expiry uses trusted backend time; production time synchronization and configuration must be operated correctly. User rowversion binding can require new proof after ordinary account writes.
- Authorization row/range locks fix the reviewed stale-read race but can increase contention. SQL timeout/deadlock victim failures must fail closed; retry requires fresh request state, never plaintext replay. No globally instantaneous cancellation of already-serialized operations is claimed.
- Production least-privilege SQL, TLS/DataProtection, credential-body telemetry exclusion, old-binary coexistence and broad DEV2 workflow certification remain separate.
- Legacy verification NU1900 remains environmental; vulnerability metadata was not declared clean. No guaranteed zeroization of managed credential strings.
- All runtime receipts, backups, WAV, ignored outputs/snapshots and existing local artifacts remain outside commits and are retained. No worktree deletion or AI-Loop mutation.
