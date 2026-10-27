# Atomic provisioning implementation

UserAccountCreationService owns the ReadCommitted SQL transaction. It resolves the real actor and both grants inside the transaction; reads branch, department and role with UPDLOCK/HOLDLOCK in a fixed order; validates active status, relationship and approved pair; checks lossless varchar storage; inserts User; saves/detaches it to obtain the real identity; saves/detaches initial membership, branch-bound role and CREATE audit; then prepares/stages the credential. No user/assignment commit occurs before credential success.

Only after CommitAsync completes successfully does trusted orchestration call PendingInitialCredential.ReleaseAfterCommit. The controller takes the IssuedTemporaryCredential password once into the dedicated success-only handoff DTO. On any failure/uncertain commit the pending object is disposed and no plaintext is released. No network delivery happens while locks/transaction are held, and no automatic retry is performed.

## Minimal shared change

TemporaryCredentialService.ApplyAsync keeps its original interface and guards: explicit standalone SQL transaction, no ambient/caller transaction, clean context, consumed preparation, own commit before IssuedTemporaryCredential. Existing callers and hashing/generation/24h policy remain unchanged. Its write/audit logic is extracted once into ApplyCoreAsync; no duplicated hash/reset policy.

New IInitialCredentialTransactionParticipant.StageInitialAsync requires an explicit same-context SQL transaction and clean tracker. It stages only InitialProvisioning, not AdminReset; it never starts/commits a transaction and cannot overwrite an existing credential. The same existing revoker and audit logic participate in the caller transaction. Caller must roll back the whole workflow on any exception; it must not commit after a failed stage.

PendingInitialCredential has no public plaintext/release accessor, redacts ToString and clears its reference on disposal. Its internal release method is a trusted orchestration API, not an independent database commit certificate. The actual creation caller invokes it only after awaiting successful commit. Prepared, pending and issued objects enforce one-time consumption with Interlocked. Strings are managed in memory; no claim of guaranteed memory zeroization.

Program.cs adds exactly two registrations: alias the participant to the same scoped ITemporaryCredentialService instance and register UserAccountCreationService. No cookie/middleware/DEV2/authorization/credential-hasher registration changes; no schema/migrations.

## Atomicity/audit and duplicates

CREATE audit whitelists identity/profile and selected role/branch/department/status. Credential UPDATE audit contains event/expiry/lockout/session-count metadata, never password/hash values. Actual server actor and target IDs are recorded. Failure rolls back user, hash/expiry, membership, role and both audits. SQL identity/rowversion allocation need not roll back; gaps are not partial accounts.

User INSERT uniqueness errors 2601/2627 are caught only around that insert, not audit writes. Roll back/dispose before checking the confirmed duplicate field names with SQL equality; return409 without provider/constraint details. No generated-code allocator, precheck-only race control, credential reset or replay store is added.

Locks protect master deactivation but serialize creation within a branch/role and may limit throughput. No stronger claim is made for concurrent grant revocation than the authorization foundation supports.
